using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Equipment;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #936: the career ladder is the seven US Army enlisted ranks. <c>ServiceRank</c> is saved as an int
/// inside the content record, so the first four ordinals keep their position (old saves extend
/// upward) and the kit tier, which stays four-valued, is reached through one mapping helper.
/// </summary>
[TestClass]
public class ServiceRankLadderTests
{
    private const string VocabularyPath = "Main/Features/Enlistment/Presentation/ServiceVocabulary.cs";
    private const string StringsPath = "Main/_Module/ModuleData/taom_enlistment_strings.xml";

    private static readonly string[] ExpectedOrder =
    {
        "Private", "Specialist", "Sergeant", "StaffSergeant", "SergeantFirstClass", "MasterSergeant", "SergeantMajor",
    };

    [TestMethod]
    public void ServiceRank_HasSevenValues_WithOrdinalsZeroToSixInOrder()
    {
        var values = Enum.GetValues(typeof(ServiceRank)).Cast<ServiceRank>().OrderBy(v => (int)v).ToArray();
        Assert.AreEqual(7, values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            Assert.AreEqual(i, (int)values[i], "ordinals must be 0..6 with no gap");
            Assert.AreEqual(ExpectedOrder[i], values[i].ToString(), $"ordinal {i}");
        }
    }

    [TestMethod]
    public void ContentRecord_Rank3_ParsesAsStaffSergeant_OldSergeantSavesExtendUpward()
    {
        Assert.IsTrue(ServiceContentRecord.TryParse("rank=3;xp=800", out var parsed));
        Assert.AreEqual(ServiceRank.StaffSergeant, parsed.Rank);
    }

    [TestMethod]
    public void ContentRecord_Rank6_Parses_Rank7_IsRejected()
    {
        Assert.IsTrue(ServiceContentRecord.TryParse("rank=6", out var top));
        Assert.AreEqual(ServiceRank.SergeantMajor, top.Rank);
        Assert.IsFalse(ServiceContentRecord.TryParse("rank=7", out _));
        Assert.IsFalse(ServiceContentRecord.TryParse("rank=-1", out _));
    }

    [TestMethod]
    public void ContentRecord_KitRank3_RoundTripsAlongsideAHigherCareerRank()
    {
        var record = new ServiceContentRecord { Rank = ServiceRank.MasterSergeant, HighestIssuedEquipmentRank = 3 };

        Assert.IsTrue(ServiceContentRecord.TryParse(record.Serialize(), out var parsed));
        Assert.AreEqual(ServiceRank.MasterSergeant, parsed.Rank);
        Assert.AreEqual(3, parsed.HighestIssuedEquipmentRank);
    }

    [TestMethod]
    public void KitTier_IsTheIdentityForTheFirstFourRanks()
    {
        Assert.AreEqual(EnlistmentKitTier.Recruit, EnlistmentKitTiers.For(ServiceRank.Private));
        Assert.AreEqual(EnlistmentKitTier.Soldier, EnlistmentKitTiers.For(ServiceRank.Specialist));
        Assert.AreEqual(EnlistmentKitTier.Veteran, EnlistmentKitTiers.For(ServiceRank.Sergeant));
        Assert.AreEqual(EnlistmentKitTier.Sergeant, EnlistmentKitTiers.For(ServiceRank.StaffSergeant));
    }

    [TestMethod]
    public void KitTier_TopRanksShareTheBestTier()
    {
        Assert.AreEqual(EnlistmentKitTier.Sergeant, EnlistmentKitTiers.For(ServiceRank.SergeantFirstClass));
        Assert.AreEqual(EnlistmentKitTier.Sergeant, EnlistmentKitTiers.For(ServiceRank.MasterSergeant));
        Assert.AreEqual(EnlistmentKitTier.Sergeant, EnlistmentKitTiers.For(ServiceRank.SergeantMajor));
    }

    [TestMethod]
    public void KitTier_BestIsTheHighestKitTierMember()
    {
        var top = Enum.GetValues(typeof(EnlistmentKitTier)).Cast<EnlistmentKitTier>().Max();
        Assert.AreEqual(top, EnlistmentKitTiers.Best, "a kit tier added above Best must move the constant");
    }

    [TestMethod]
    public void RankBonus_IsFourPerRankCappedAtTwelve()
    {
        var expected = new[] { 0, 4, 8, 12, 12, 12, 12 };
        for (var i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], SkillCheckService.RankBonus((ServiceRank)i), $"rank {i}");
    }

    [TestMethod]
    public void RankName_HasOneDistinctRankKeyPerRank()
    {
        var src = RepoPaths.ReadSource(VocabularyPath, stripComments: true);
        var start = src.IndexOf("TextObject RankName(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "RankName is gone");
        var end = src.IndexOf("\n    }\n", start, StringComparison.Ordinal);
        var body = src.Substring(start, end - start);

        var expected = new Dictionary<string, string>
        {
            ["Private"] = "taom_enlist_rank_private",
            ["Specialist"] = "taom_enlist_rank_specialist",
            ["Sergeant"] = "taom_enlist_rank_sergeant",
            ["StaffSergeant"] = "taom_enlist_rank_staff_sergeant",
            ["SergeantFirstClass"] = "taom_enlist_rank_sergeant_first_class",
            ["MasterSergeant"] = "taom_enlist_rank_master_sergeant",
            ["SergeantMajor"] = "taom_enlist_rank_sergeant_major",
        };
        foreach (var kv in expected)
        {
            var m = Regex.Match(body, @"case ServiceRank\." + kv.Key + @":\s*return new TextObject\(""\{=" + kv.Value + @"\}");
            Assert.IsTrue(m.Success, $"{kv.Key} must return key {kv.Value}");
        }

        var keys = Regex.Matches(body, @"\{=(taom_enlist_rank_\w+)\}").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        Assert.AreEqual(7, keys.Count, "exactly one rank key per rank, none on the default arm");
        Assert.AreEqual(7, keys.Distinct().Count());

        var strings = RepoPaths.ReadSource(StringsPath);
        foreach (var key in expected.Values)
            StringAssert.Contains(strings, "id=\"" + key + "\"", $"{key} missing from taom_enlistment_strings.xml");
        foreach (var gone in new[] { "taom_enlist_rank_recruit", "taom_enlist_rank_soldier", "taom_enlist_rank_veteran" })
        {
            Assert.IsFalse(src.Contains(gone), $"{gone} is retired");
            Assert.IsFalse(strings.Contains(gone), $"{gone} is orphaned in taom_enlistment_strings.xml");
        }

        var languageFiles = Directory.GetFiles(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"),
            "std_taom_enlistment_strings_*.xml", SearchOption.AllDirectories);
        Assert.AreEqual(12, languageFiles.Length, "expected one enlistment strings file per language");
        foreach (var file in languageFiles)
        {
            var text = File.ReadAllText(file);
            foreach (var gone in new[] { "taom_enlist_rank_recruit", "taom_enlist_rank_soldier", "taom_enlist_rank_veteran" })
                Assert.IsFalse(text.Contains("id=\"" + gone + "\""), $"{gone} is orphaned in {Path.GetFileName(file)}");
        }
    }

    [TestMethod]
    public void ShippedDuties_EveryMinRank_PinnedByOrdinal()
    {
        // strategic_planning is the silent trap: "Sergeant" still parses after the rename but would
        // mean ordinal 2, one rank lower than the row was tuned for.
        var expected = new Dictionary<string, int>
        {
            ["recon_sweep"] = 0, ["mounted_pursuit"] = 1, ["bandit_hunt"] = 0, ["deserter_sweep"] = 1,
            ["road_patrol"] = 0, ["scout_route"] = 0, ["recruitment_errand"] = 0, ["trusted_dispatch"] = 2,
            ["relief_dispatch"] = 2, ["supply_delivery"] = 0, ["forage"] = 0, ["hideout_strike"] = 2,
            ["service_shift"] = 0, ["night_patrol"] = 0, ["treat_wounded"] = 0, ["train_recruits"] = 1,
            ["equipment_check"] = 0, ["inspect_defenses"] = 0, ["gate_watch"] = 0, ["quartermaster_shortage"] = 1,
            ["lead_patrol"] = 2, ["coordinate_supply"] = 2, ["command_squad"] = 2, ["strategic_planning"] = 3,
            ["pay_delay"] = 0, ["short_rations"] = 0, ["camp_discipline"] = 0,
        };

        var root = JObject.Parse(File.ReadAllText(RepoPaths.RepoPath(
            "Main", "_Module", "ModuleData", "enlistment", "enlistment_duties.json")));
        var actual = new Dictionary<string, int>();
        foreach (var section in new[] { "fieldDuties", "interactiveDuties", "incidents" })
        {
            foreach (var row in (JArray)root[section] ?? new JArray())
            {
                var name = (string)row["gates"]?["minRank"];
                if (name == null)
                    continue;
                Assert.IsTrue(Enum.IsDefined(typeof(ServiceRank), name), $"{row["id"]} gates on unknown rank '{name}'");
                actual[(string)row["id"]] = (int)Enum.Parse(typeof(ServiceRank), name);
            }
        }

        foreach (var kv in expected)
        {
            Assert.IsTrue(actual.ContainsKey(kv.Key), $"{kv.Key} lost its minRank or was removed");
            Assert.AreEqual(kv.Value, actual[kv.Key], $"{kv.Key} minRank ordinal");
        }
        CollectionAssert.AreEquivalent(expected.Keys.ToList(), actual.Keys.ToList(),
            "a duty gained or lost a minRank: add it to the pinned table");
    }
}
