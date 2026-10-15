using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The enlistment quartermaster puts a kit in the party INVENTORY, and the player equips it by hand,
/// so <c>CharacterHelper.CanUseItem</c> applies: a bow whose <c>difficulty</c> is above the player's
/// Bow skill cannot be equipped. Every Harad bow troop carries a <c>ladder_harad_bow_*</c> at
/// difficulty 100 (inherited from the donor bow), and the 2026-10-15 generated Harad-sphere archer
/// kits handed those to new enlistees. The kits now use the vanilla steppe bows; this pins that no
/// launcher in those chains needs more than Bow 50 and that no rank issues a weaker bow than the
/// rank below it.
///
/// Scoped to the Harad-sphere cultures that change authored kits for. Other cultures' kits that
/// carry launchers above difficulty 50 (60 to 100) predate it and belong to the generator follow-up.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class EnlistmentArcherLauncherDataTests
{
    private static readonly string[] Cultures = { "aserai", "shaghana", "abanissa" };
    private static readonly string[] Ranks = { "recruit", "soldier", "veteran", "sergeant" };

    /// <summary>The steppe sergeant bow (steppe_war_bow) is the hardest the file issues to these chains.</summary>
    private const int MaxLauncherDifficulty = 50;

    private static string RosterPath => Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
        @"..\..\..\..\Main\_Module\ModuleData\equipmentsets\taom_enlistment_equipment.xml"));

    [TestMethod]
    public void HaradSphereArcherKits_IssueOnlyBowsAnEnlisteeCanEquip_AndNeverAWeakerOneAtAHigherRank()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game dir unresolved: " + string.Join("; ", GameAssemblies.Diagnostics));

        var launchers = IndexLaunchers(GameAssemblies.GameDir);
        var rosters = XDocument.Load(RosterPath).Descendants("EquipmentRoster")
            .ToDictionary(r => (string)r.Attribute("id")!, r => r, StringComparer.Ordinal);

        var failures = new List<string>();
        var checkedKits = 0;
        foreach (var culture in Cultures)
        {
            var previous = (Rank: "", Damage: 0);
            foreach (var rank in Ranks)
            {
                var id = $"enlist_{culture}_archer_{rank}";
                if (!rosters.TryGetValue(id, out var roster))
                    continue; // an absent cell descends to a lower rank's kit at runtime

                var bows = roster.Descendants("Equipment")
                    .Select(e => ((string)e.Attribute("id") ?? "").Replace("Item.", ""))
                    .Where(launchers.ContainsKey)
                    .ToList();
                if (bows.Count != 1)
                {
                    failures.Add($"{id}: expected exactly one launcher, found {bows.Count}");
                    continue;
                }

                checkedKits++;
                var bow = launchers[bows[0]];
                if (bow.Difficulty > MaxLauncherDifficulty)
                    failures.Add($"{id}: {bows[0]} needs Bow {bow.Difficulty}, above {MaxLauncherDifficulty}; an enlistee cannot equip it");
                if (bow.Damage < previous.Damage)
                    failures.Add($"{id}: {bows[0]} hits {bow.Damage}, below the {previous.Rank} bow's {previous.Damage}");
                previous = (rank, bow.Damage);
            }
        }

        Assert.IsTrue(checkedKits >= 8, $"only {checkedKits} Harad-sphere archer kits found; the roster parse is broken");
        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    private static Dictionary<string, (int Difficulty, int Damage)> IndexLaunchers(string gameDir)
    {
        var sources = new List<string>();
        foreach (var dir in new[]
        {
            Path.Combine(gameDir, "Modules", "LOTRLOME_Armory", "ModuleData", "LOTRLOME_items"),
            Path.Combine(gameDir, "Modules", "SandBoxCore", "ModuleData", "items"),
            Path.Combine(gameDir, "Modules", "Native", "ModuleData", "items"),
        })
        {
            if (Directory.Exists(dir))
                sources.AddRange(Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories));
        }
        Assert.AreNotEqual(0, sources.Count, $"no item XML sources found under '{gameDir}'; is the Armory installed?");

        var map = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        foreach (var file in sources)
        {
            foreach (Match item in Regex.Matches(File.ReadAllText(file), "<Item\\b([^>]*)>(.*?)</Item>", RegexOptions.Singleline))
            {
                var attrs = item.Groups[1].Value;
                var type = Regex.Match(attrs, "\\bType\\s*=\\s*\"([^\"]+)\"").Groups[1].Value;
                if (type != "Bow" && type != "Crossbow") continue;
                var id = Regex.Match(attrs, "\\bid\\s*=\\s*\"([^\"]+)\"").Groups[1].Value;
                if (id.Length == 0 || map.ContainsKey(id)) continue;
                var difficulty = Regex.Match(attrs, "\\bdifficulty\\s*=\\s*\"(\\d+)\"");
                var damage = Regex.Match(item.Groups[2].Value, "\\bthrust_damage\\s*=\\s*\"(\\d+)\"");
                map[id] = (difficulty.Success ? int.Parse(difficulty.Groups[1].Value) : 0,
                           damage.Success ? int.Parse(damage.Groups[1].Value) : 0);
            }
        }
        return map;
    }
}
