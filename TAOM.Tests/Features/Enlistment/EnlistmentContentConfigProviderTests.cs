using System.Linq;
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Semantic-validation tests per the config-provider rule: one test per validation rule,
/// covering parseable-but-invalid values — not just missing/malformed files.
/// </summary>
[TestClass]
public class EnlistmentContentConfigProviderTests
{
    private string _dir = null!;
    private IPathService _paths = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "taom_enlist_cfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_dir, "enlistment"));
        _paths = Substitute.For<IPathService>();
        _paths.ModuleDataPath.Returns(_dir);
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private EnlistmentContentConfigProvider Provider() => new EnlistmentContentConfigProvider(_paths, _logger);

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_dir, "enlistment", "enlistment_config.json"), json);

    private void WriteDuties(string json) =>
        File.WriteAllText(Path.Combine(_dir, "enlistment", "enlistment_duties.json"), json);

    [TestMethod]
    public void GetConfig_MissingFile_DefaultsWithWarning()
    {
        var config = Provider().GetConfig();

        Assert.AreEqual(10, config.Progression.DailyLeadershipXp);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_DefaultsWithError()
    {
        WriteConfig("{ not json !!!");

        var config = Provider().GetConfig();

        Assert.AreEqual(7, config.Progression.DailyWageByRank.Count);
        _logger.Received().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Is<string>(s => s.Contains("Rank names changed")));
    }

    [TestMethod]
    public void GetConfig_NaNOfferChance_RevertsWithWarning()
    {
        WriteConfig("{\"scheduler\":{\"baseOfferChance\":\"NaN\"}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(0.06f, config.Scheduler.BaseOfferChance);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("baseOfferChance")));
    }

    [TestMethod]
    public void GetConfig_WageTableWrongLength_Reverts()
    {
        WriteConfig("{\"progression\":{\"dailyWageByRank\":[5,8]}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(7, config.Progression.DailyWageByRank.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("dailyWageByRank")));
    }

    [TestMethod]
    public void GetConfig_NegativeWage_Reverts()
    {
        WriteConfig("{\"progression\":{\"dailyWageByRank\":[5,8,-14,22,28,34,40]}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(14, config.Progression.DailyWageByRank[2]);
    }

    [TestMethod]
    public void GetConfig_LegacyMaxDeferredWagesKey_WarnsAboutTheRename()
    {
        // Migration hazard: the key was renamed AND changed unit (flat gold -> days of the current
        // rank wage). Newtonsoft binds the old name to nothing, so without this warning a player's
        // retuned install silently takes the new default and never learns why.
        WriteConfig("{\"wagePolicy\":{\"maxDeferredWages\":60}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(14, config.WagePolicy.MaxDeferredWageDays, "the old key is ignored, not converted");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxDeferredWages") && s.Contains("renamed")));
    }

    [TestMethod]
    public void GetConfig_NewMaxDeferredWageDaysKey_NoRenameWarning()
    {
        WriteConfig("{\"wagePolicy\":{\"maxDeferredWageDays\":30}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(30, config.WagePolicy.MaxDeferredWageDays);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("renamed")));
    }

    [TestMethod]
    public void GetConfig_NegativeMaxDeferredWageDays_Reverts()
    {
        WriteConfig("{\"wagePolicy\":{\"maxDeferredWageDays\":-1}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(14, config.WagePolicy.MaxDeferredWageDays);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxDeferredWageDays")));
    }

    [TestMethod]
    public void GetConfig_MaxDeferredWageDaysAboveContractLength_Reverts()
    {
        // Unbounded day counts turn arrears into a discharge-time gold bomb and push the
        // days x wage multiply toward overflow. One contract (365 days) is the ceiling.
        WriteConfig("{\"wagePolicy\":{\"maxDeferredWageDays\":4000}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(14, config.WagePolicy.MaxDeferredWageDays);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxDeferredWageDays")));
    }

    [TestMethod]
    public void GetConfig_NonMonotonicPromotionDays_RevertsToDefaultLadder()
    {
        WriteConfig("{\"promotions\":[" +
            "{\"toRank\":\"Specialist\",\"minDaysServed\":25,\"minServiceXp\":100}," +
            "{\"toRank\":\"Sergeant\",\"minDaysServed\":7,\"minServiceXp\":350}," +
            "{\"toRank\":\"StaffSergeant\",\"minDaysServed\":60,\"minServiceXp\":800}," +
            "{\"toRank\":\"SergeantFirstClass\",\"minDaysServed\":120,\"minServiceXp\":1800}," +
            "{\"toRank\":\"MasterSergeant\",\"minDaysServed\":200,\"minServiceXp\":3200}," +
            "{\"toRank\":\"SergeantMajor\",\"minDaysServed\":300,\"minServiceXp\":5000}]}");

        var config = Provider().GetConfig();

        Assert.AreEqual(7, config.Promotions[0].MinDaysServed, "default ladder restored");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("promotions")));
    }

    [TestMethod]
    public void GetConfig_WrongPromotionCount_Reverts()
    {
        WriteConfig("{\"promotions\":[{\"toRank\":\"Specialist\",\"minDaysServed\":7,\"minServiceXp\":100}]}");

        var config = Provider().GetConfig();

        Assert.AreEqual(6, config.Promotions.Count);
    }

    [TestMethod]
    public void GetConfig_MeritBandsNotDescending_Reverts()
    {
        WriteConfig("{\"meritBands\":[" +
            "{\"minScore\":40,\"serviceXp\":12},{\"minScore\":80,\"serviceXp\":30}]}");

        var config = Provider().GetConfig();

        Assert.AreEqual(4, config.MeritBands.Count, "default bands restored");
        Assert.IsTrue(config.MeritBands[0].MinScore > config.MeritBands[1].MinScore);
    }

    [TestMethod]
    public void GetConfig_MeritBandsNegativeRenown_Reverts()
    {
        // Renown joined the non-negative set when it became live config (2026-08-11). It is
        // directional: BattleRenownPolicy adds the band figure to the flat win/loss base, so a
        // negative band does not merely shrink the reward — it eats the base, and a distinguished
        // fight ends up paying LESS than a rough one. Clamped at the policy, reverted here.
        WriteConfig("{\"meritBands\":[" +
            "{\"minScore\":80,\"serviceXp\":30,\"renown\":-5},{\"minScore\":0,\"serviceXp\":4,\"renown\":0}]}");

        var config = Provider().GetConfig();

        Assert.AreEqual(4, config.MeritBands.Count, "default bands restored");
        Assert.IsTrue(config.MeritBands.All(b => b.Renown >= 0));
    }

    /// <summary>
    /// The regression for the defect BattleRenownPolicy's own doc comment described but the config
    /// never delivered: <c>MeritBand.Renown</c> existed, the policy added it, and NO default band
    /// and NO shipped config key ever set it. <c>bandRenown</c> was 0 on every path, so every
    /// battle paid the identical flat base while the comment claimed the band did the
    /// differentiating.
    ///
    /// BattleRenownPolicyTests could not catch it — every one of those cases passes
    /// <c>bandRenown</c> in as a literal, so the pure function was fully covered and completely
    /// disconnected from the numbers that actually reach it. This asserts the SUPPLY side.
    /// </summary>
    [TestMethod]
    public void DefaultMeritBands_ActuallyDifferentiateOnRenown()
    {
        var bands = EnlistmentContentConfigProvider.BuildDefaults().MeritBands;

        Assert.IsTrue(bands.Any(b => b.Renown > 0),
            "no default band awards renown — bandRenown is always 0 and every battle pays the same flat base");

        // Descending minScore must mean non-increasing renown, or a worse fight outearns a better
        // one. Strictly greater at the ends, so the ladder is not merely flat-but-nonzero.
        for (var i = 1; i < bands.Count; i++)
            Assert.IsTrue(bands[i].Renown <= bands[i - 1].Renown,
                $"band '{bands[i].GradeKey}' pays more renown than the better band above it");

        Assert.IsTrue(bands[0].Renown > bands[bands.Count - 1].Renown,
            "the best and worst bands pay the same renown — the grade buys nothing");
    }

    /// <summary>
    /// The defaults are only half the fix: an install reads the SHIPPED json, and a key absent
    /// there silently takes the compiled default rather than the tuner's intent. This pins that the
    /// renown chain is actually exposed in the file players edit — both the per-band figure and the
    /// flat win/loss base it is added to.
    /// </summary>
    [TestMethod]
    public void ShippedConfig_ExposesTheWholeRenownChain()
    {
        var provider = new EnlistmentContentConfigProvider(ShippedPaths(), _logger);
        var config = provider.GetConfig();

        Assert.IsTrue(config.MeritBands.Any(b => b.Renown > 0),
            "the shipped enlistment_config.json has no band awarding renown");
        Assert.IsTrue(config.Progression.BattleWinRenown > 0,
            "the shipped enlistment_config.json does not set battleWinRenown");

        var raw = File.ReadAllText(Path.Combine(
            ShippedModuleDataPath(), "enlistment", "enlistment_config.json"));
        StringAssert.Contains(raw, "\"renown\"",
            "the band renown key is missing from the shipped file, so a retune has nothing to edit");
        StringAssert.Contains(raw, "\"battleWinRenown\"",
            "the flat renown base is missing from the shipped file");

        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("meritBands")));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    /// <summary>
    /// Standing has exactly two earners: a duty success, and a merit band that pays trust. The band
    /// half has to survive both surfaces or it does not exist for half the installs — an
    /// <c>enlistment_config.json</c> that omits the key silently takes the compiled default, and a
    /// compiled default that disagrees with the shipped file means two players on the same build
    /// progress at different rates with nothing in the log.
    ///
    /// This is the same class of gap as the renown chain above, on the field that gates promotion:
    /// Sergeant (then Veteran) requires <c>minTrust: 0</c>, so a soldier who fights steadily and takes no camp work
    /// is stuck at Specialist (then Soldier) forever if no band he can reach pays trust. That shipped, and a live
    /// 73-day service reached "badly thought of" on 2903 XP because of it.
    /// </summary>
    [TestMethod]
    public void MeritBandTrust_MatchesBetweenTheShippedFileAndTheCompiledDefaults()
    {
        // Read the FILE, not the provider's output. Going through GetConfig() looks stronger and is
        // strictly weaker: four paths inside LoadConfig/ValidateConfig replace MeritBands wholesale
        // with DefaultMeritBands() — file missing, unparseable, a null-or-empty meritBands array
        // (which logs NOTHING at all), and IsValidBandLadder rejecting a reordered or partially
        // edited ladder. On any of them this test would compare the defaults against themselves and
        // report green, which is precisely the divergence it exists to catch.
        var raw = JObject.Parse(File.ReadAllText(Path.Combine(
            ShippedModuleDataPath(), "enlistment", "enlistment_config.json")));
        var shipped = ((JArray)raw["meritBands"])
            .ToDictionary(b => (string)b["gradeKey"], b => (int?)b["trust"] ?? 0);
        var compiled = EnlistmentContentConfigProvider.BuildDefaults().MeritBands;

        // Keyed by gradeKey, not by index: a reordered file must report the band that actually
        // diverged rather than whichever one happened to land in that slot.
        foreach (var band in compiled)
        {
            Assert.IsTrue(shipped.ContainsKey(band.GradeKey),
                $"band '{band.GradeKey}' exists in DefaultMeritBands() but not in enlistment_config.json");
            Assert.AreEqual(band.Trust, shipped[band.GradeKey],
                $"band '{band.GradeKey}' pays {shipped[band.GradeKey]} trust in enlistment_config.json "
                + $"but {band.Trust} in DefaultMeritBands() — an install without the file would earn "
                + "standing at a different rate");
        }

        Assert.AreEqual(compiled.Count, shipped.Count, "band count diverged between file and defaults");

        // The earner has to exist at all; WHICH band carries it is MeritTrustFloorTests' call, and
        // it is not a free choice — the bands below `strong` are reachable without fighting.
        Assert.IsTrue(compiled.Any(b => b.Trust > 0),
            "no merit band pays trust, so fighting well cannot raise standing at all");

        for (var i = 1; i < compiled.Count; i++)
            Assert.IsTrue(compiled[i].Trust <= compiled[i - 1].Trust,
                $"band '{compiled[i].GradeKey}' pays more trust than the better band above it");

        // The round trip must also be clean, or a future edit could satisfy every assertion above
        // while the provider silently discarded the file at load.
        new EnlistmentContentConfigProvider(ShippedPaths(), _logger).GetConfig();
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    /// <summary>
    /// The duty half of the same gap. `ShippedPaths()` was reached only by `GetConfig()` callers, so
    /// no test loaded the real `enlistment_duties.json` through the provider's own validation —
    /// `FieldDutyReachabilityTests` parses the file directly and every other duty test stubs the
    /// provider. `LoadDuties` SKIPS a bad row with nothing but a log warning, so a typo in one of
    /// the 11 rows that gained a second support skill (or in any retuned difficulty) would drop that
    /// duty at runtime while every other test kept counting thirteen.
    /// </summary>
    [TestMethod]
    public void ShippedDuties_LoadThroughTheProviderWithoutBeingSkipped()
    {
        var duties = new EnlistmentContentConfigProvider(ShippedPaths(), _logger).GetDuties();

        Assert.AreEqual(13, duties.FieldDuties.Count, "a field duty row was skipped at load");
        Assert.AreEqual(11, duties.InteractiveDuties.Count, "an interactive duty row was skipped at load");
        Assert.AreEqual(3, duties.Incidents.Count, "an incident row was skipped at load");

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    /// <summary>
    /// #936: the ladder is seven ranks, so the promotion list is six rows and the wage and service-XP
    /// tables carry seven entries. The shipped file and the compiled defaults must agree, or an
    /// install without the file would promote on different thresholds.
    /// </summary>
    [TestMethod]
    public void ShippedConfig_PromotionsAndTables_EqualTheCompiledDefaults()
    {
        var rankCount = Enum.GetValues(typeof(ServiceRank)).Length;
        Assert.AreEqual(7, rankCount);

        var defaults = EnlistmentContentConfigProvider.BuildDefaults();
        Assert.AreEqual(rankCount - 1, defaults.Promotions.Count);
        for (var i = 0; i < defaults.Promotions.Count; i++)
            Assert.AreEqual((ServiceRank)(i + 1), defaults.Promotions[i].ToRank, $"default row {i} promotes to the wrong rank");
        CollectionAssert.AreEqual(new[] { 5, 8, 14, 22, 28, 34, 40 }, defaults.Progression.DailyWageByRank.ToArray());
        CollectionAssert.AreEqual(new[] { 10, 12, 16, 20, 22, 24, 26 }, defaults.Progression.DailyServiceXpByRank.ToArray());

        var raw = JObject.Parse(File.ReadAllText(Path.Combine(
            ShippedModuleDataPath(), "enlistment", "enlistment_config.json")));
        CollectionAssert.AreEqual(defaults.Progression.DailyWageByRank.ToArray(),
            ((JArray)raw["progression"]["dailyWageByRank"]).Select(t => (int)t).ToArray());
        CollectionAssert.AreEqual(defaults.Progression.DailyServiceXpByRank.ToArray(),
            ((JArray)raw["progression"]["dailyServiceXpByRank"]).Select(t => (int)t).ToArray());
        Assert.AreEqual(defaults.Progression.DailyLeadershipXp, (int)raw["progression"]["dailyLeadershipXp"],
            "the reachability test reads the shipped dailyLeadershipXp; the compiled default must match it");

        var shipped = (JArray)raw["promotions"];
        Assert.AreEqual(defaults.Promotions.Count, shipped.Count, "promotion row count diverged between file and defaults");
        for (var i = 0; i < shipped.Count; i++)
        {
            var d = defaults.Promotions[i];
            var row = shipped[i];
            Assert.AreEqual(d.ToRank.ToString(), (string)row["toRank"], $"row {i} toRank");
            Assert.AreEqual(d.MinDaysServed, (int)row["minDaysServed"], $"row {i} days");
            Assert.AreEqual(d.MinServiceXp, (int)row["minServiceXp"], $"row {i} xp");
            Assert.AreEqual(d.MinLeadershipSkill, (int)row["minLeadershipSkill"], $"row {i} leadership");
            Assert.AreEqual(d.MinDutySuccesses, (int)row["minDutySuccesses"], $"row {i} duties");
            Assert.AreEqual(d.MinTrust, (int)row["minTrust"], $"row {i} trust");
        }

        new EnlistmentContentConfigProvider(ShippedPaths(), _logger).GetConfig();
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void SitOutDefaults_AreTheApprovedValues()
    {
        var sitOut = EnlistmentContentConfigProvider.BuildDefaults().SitOut;

        Assert.AreEqual(0.85f, sitOut.OverwhelmingShare);
        Assert.AreEqual(1, sitOut.TrustCost);
        Assert.AreEqual(0.5f, sitOut.BaseXpFactor);
    }

    [TestMethod]
    public void ShippedConfig_SitOut_EqualsTheCompiledDefaults()
    {
        var defaults = EnlistmentContentConfigProvider.BuildDefaults().SitOut;
        var raw = JObject.Parse(File.ReadAllText(Path.Combine(
            ShippedModuleDataPath(), "enlistment", "enlistment_config.json")));

        var row = raw["sitOut"];
        Assert.IsNotNull(row, "enlistment_config.json lost its sitOut section");
        Assert.AreEqual(defaults.OverwhelmingShare, (float)row["overwhelmingShare"]);
        Assert.AreEqual(defaults.TrustCost, (int)row["trustCost"]);
        Assert.AreEqual(defaults.BaseXpFactor, (float)row["baseXpFactor"]);

        var loaded = new EnlistmentContentConfigProvider(ShippedPaths(), _logger).GetConfig().SitOut;
        Assert.AreEqual(defaults.OverwhelmingShare, loaded.OverwhelmingShare);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("sitOut")));
    }

    [TestMethod]
    public void GetConfig_NoSitOutSection_UsesTheDefaults()
    {
        WriteConfig("{\"progression\":{\"dailyLeadershipXp\":10}}");

        var sitOut = Provider().GetConfig().SitOut;

        Assert.AreEqual(0.85f, sitOut.OverwhelmingShare);
        Assert.AreEqual(1, sitOut.TrustCost);
        Assert.AreEqual(0.5f, sitOut.BaseXpFactor);
    }

    [TestMethod]
    public void GetConfig_SitOutValidValues_AreKept()
    {
        WriteConfig("{\"sitOut\":{\"overwhelmingShare\":0.7,\"trustCost\":3,\"baseXpFactor\":0.25}}");

        var sitOut = Provider().GetConfig().SitOut;

        Assert.AreEqual(0.7f, sitOut.OverwhelmingShare);
        Assert.AreEqual(3, sitOut.TrustCost);
        Assert.AreEqual(0.25f, sitOut.BaseXpFactor);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("sitOut")));
    }

    [DataTestMethod]
    [DataRow("\"NaN\"")]
    [DataRow("\"Infinity\"")]
    [DataRow("0.5")]
    [DataRow("0.2")]
    [DataRow("0")]
    [DataRow("-0.9")]
    [DataRow("1.01")]
    public void GetConfig_SitOutShareNotFiniteOrOutOfRange_RevertsWithWarning(string value)
    {
        // The share must be a finite number in (0.5, 1]: at 0.5 or below the player's side would not
        // be a clear favourite, and above 1 no battle could ever qualify.
        WriteConfig("{\"sitOut\":{\"overwhelmingShare\":" + value + "}}");

        var sitOut = Provider().GetConfig().SitOut;

        Assert.AreEqual(0.85f, sitOut.OverwhelmingShare);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("sitOut.overwhelmingShare")));
    }

    [TestMethod]
    public void GetConfig_SitOutShareExactlyOne_IsKept()
    {
        WriteConfig("{\"sitOut\":{\"overwhelmingShare\":1}}");

        Assert.AreEqual(1f, Provider().GetConfig().SitOut.OverwhelmingShare);
    }

    [TestMethod]
    public void GetConfig_SitOutNegativeTrustCost_Reverts()
    {
        WriteConfig("{\"sitOut\":{\"trustCost\":-2}}");

        var sitOut = Provider().GetConfig().SitOut;

        Assert.AreEqual(1, sitOut.TrustCost);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("sitOut.trustCost")));
    }

    [TestMethod]
    public void GetConfig_SitOutZeroTrustCost_IsKept()
    {
        WriteConfig("{\"sitOut\":{\"trustCost\":0}}");

        Assert.AreEqual(0, Provider().GetConfig().SitOut.TrustCost);
    }

    [DataTestMethod]
    [DataRow("\"NaN\"")]
    [DataRow("\"-Infinity\"")]
    [DataRow("-0.1")]
    [DataRow("1.5")]
    public void GetConfig_SitOutBaseXpFactorNotFiniteOrOutOfRange_RevertsWithWarning(string value)
    {
        WriteConfig("{\"sitOut\":{\"baseXpFactor\":" + value + "}}");

        var sitOut = Provider().GetConfig().SitOut;

        Assert.AreEqual(0.5f, sitOut.BaseXpFactor);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("sitOut.baseXpFactor")));
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("1")]
    public void GetConfig_SitOutBaseXpFactorAtTheEnds_IsKept(string value)
    {
        WriteConfig("{\"sitOut\":{\"baseXpFactor\":" + value + "}}");

        Assert.AreEqual(float.Parse(value), Provider().GetConfig().SitOut.BaseXpFactor);
    }

    [TestMethod]
    public void DefaultLadder_DaysDutiesAndTrust_ArePinned()
    {
        var promotions = EnlistmentContentConfigProvider.BuildDefaults().Promotions;
        CollectionAssert.AreEqual(new[] { 7, 25, 60, 120, 200, 300 },
            promotions.Select(p => p.MinDaysServed).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 2, 5, 8, 12, 16 },
            promotions.Select(p => p.MinDutySuccesses).ToArray());
        CollectionAssert.AreEqual(new[] { -10, 0, 6, 10, 14, 18 },
            promotions.Select(p => p.MinTrust).ToArray());
    }

    /// <summary>
    /// #936 D2 / #521: every Leadership gate must open by its own day threshold for a new character.
    /// The test reproduces the engine instead of trusting a nominal rate, from the installed v1.5.5
    /// sources:
    /// <list type="bullet">
    /// <item>Skill curve, DefaultCharacterDevelopmentModel.cs:95-103 (InitializeXpRequiredForSkillLevel):
    /// first requirement 30, then each step adds an increment that grows by 10 + i. Level L needs
    /// table[L - 1] (GetXpRequiredForSkillLevel, :127-137).</item>
    /// <item>Learning limit, :206-221: max(0, (attribute - 1) * 10) + 30 * focus. Leadership is keyed
    /// to Social alone (DefaultSkills.cs:101).</item>
    /// <item>Learning rate, :223-244: 1.25 * (1 + 0.4 * attribute + focus), plus the factor
    /// -1 - 0.1 * (skill - limit) once the skill is above the rounded limit, floored at 0.
    /// ExplainedNumber is base + base * sum of factors (ExplainedNumber.cs:149).</item>
    /// <item>HeroDeveloper.cs:198-216 (AddSkillXp) multiplies the raw grant by the rate at the current
    /// skill; GenericXpModel's multiplier is 1 for the player (DefaultGenericXpModel.cs:8-15).</item>
    /// </list>
    /// The hero is a new character: Social 2, no Leadership focus. Reads the SHIPPED file, so a retune
    /// of the gates or of dailyLeadershipXp fails here rather than in a player's hands.
    /// </summary>
    [TestMethod]
    public void ShippedLeadershipGates_AreReachedByEachStepsDayThreshold()
    {
        var raw = JObject.Parse(File.ReadAllText(Path.Combine(
            ShippedModuleDataPath(), "enlistment", "enlistment_config.json")));
        var dailyXp = (int)raw["progression"]["dailyLeadershipXp"];
        var rows = ((JArray)raw["promotions"]).ToList();
        var gates = rows.Select(r => (int)r["minLeadershipSkill"]).ToArray();
        var days = rows.Select(r => (int)r["minDaysServed"]).ToArray();

        for (var i = 1; i < gates.Length; i++)
            Assert.IsTrue(gates[i] >= gates[i - 1], $"Leadership gate {i} falls below gate {i - 1}");

        const int social = 2;
        const int focus = 0;
        var xpTable = new long[200];
        long increment = 30;
        xpTable[0] = increment;
        for (var i = 1; i < xpTable.Length; i++)
        {
            increment += 10 + i;
            xpTable[i] = xpTable[i - 1] + increment;
        }

        var limit = (int)Math.Round(Math.Max(0f, (social - 1) * 10f) + focus * 30);
        float xp = 0;
        var level = 0;
        var dayReached = new int?[gates.Length];
        for (var day = 1; day <= 365; day++)
        {
            var factors = 0.4f * social + focus;
            if (level > limit)
                factors += -1f - 0.1f * (level - limit);
            var rate = Math.Max(0f, 1.25f * (1f + factors));
            xp += dailyXp * rate;
            while (level < 199 && xp >= xpTable[level])
                level++;
            for (var i = 0; i < gates.Length; i++)
                if (dayReached[i] == null && level >= gates[i])
                    dayReached[i] = day;
        }

        for (var i = 0; i < gates.Length; i++)
        {
            Assert.IsNotNull(dayReached[i], $"Leadership {gates[i]} (step {i + 1}) is never reached by a Social {social} hero");
            Assert.IsTrue(dayReached[i] <= days[i],
                $"Leadership {gates[i]} (step {i + 1}) is reached on day {dayReached[i]}, after its day threshold {days[i]}");
        }
    }

    [TestMethod]
    public void GetDuties_LegacyRankName_LogsTheRenameHint()
    {
        WriteDuties("{\"fieldDuties\":[{\"id\":\"old_row\",\"gates\":{\"minRank\":\"Recruit\"}}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(0, duties.FieldDuties.Count);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Rank names changed in v2.0.35 (#936)")));
    }

    [TestMethod]
    public void GetConfig_LegacyRankName_LogsTheRenameHint()
    {
        WriteConfig("{\"promotions\":[{\"toRank\":\"Soldier\",\"minDaysServed\":7,\"minServiceXp\":100}]}");

        var config = Provider().GetConfig();

        Assert.AreEqual(6, config.Promotions.Count, "defaults in force");
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Rank names changed in v2.0.35 (#936)")));
    }

    private IPathService ShippedPaths()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ShippedModuleDataPath());
        return paths;
    }

    private static string ShippedModuleDataPath()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "Main", "_Module", "ModuleData");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.Fail("Could not locate Main/_Module/ModuleData from the test working directory.");
        return null;
    }

    [TestMethod]
    public void GetConfig_ValidFile_LoadsAndKeepsValues()
    {
        WriteConfig("{\"progression\":{\"dailyLeadershipXp\":15}}");

        var config = Provider().GetConfig();

        Assert.AreEqual(15, config.Progression.DailyLeadershipXp);
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_ValidRankTables_AppliedNotAppendedToDefaults()
    {
        // Regression (in-game 2026-08-07): the model initializes these lists, and Newtonsoft's
        // default ObjectCreationHandling.Auto APPENDS file entries onto that existing collection.
        // A valid 4-entry table deserialized to 8, failed the "exactly 4" check, and silently
        // reverted to the compiled defaults — so every retune was discarded with only a warning.
        // Values here deliberately differ from the defaults so a revert cannot pass this test.
        WriteConfig("{\"progression\":{\"dailyWageByRank\":[9,11,13,17,19,21,23],\"dailyServiceXpByRank\":[1,2,3,4,5,6,7]}}");

        var config = Provider().GetConfig();

        CollectionAssert.AreEqual(new[] { 9, 11, 13, 17, 19, 21, 23 }, config.Progression.DailyWageByRank.ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7 }, config.Progression.DailyServiceXpByRank.ToArray());
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("dailyWageByRank")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("dailyServiceXpByRank")));
    }

    [TestMethod]
    public void GetDuties_MissingFile_EmptyTablesWithWarning()
    {
        var duties = Provider().GetDuties();

        Assert.AreEqual(0, duties.FieldDuties.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetDuties_UnknownSkillId_SkipsRowWithWarning()
    {
        // The M1 trap: a typo'd string the consumer branches on must SKIP+warn, never
        // silently take a default path.
        WriteDuties("{\"interactiveDuties\":[{\"id\":\"night_patrol\",\"optionA\":{\"key\":\"patrol\",\"skillId\":\"Scoutting\",\"difficulty\":60},\"optionB\":{\"key\":\"decline\",\"skillId\":\"Athletics\",\"difficulty\":50}}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(0, duties.InteractiveDuties.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("Scoutting")));
    }

    [TestMethod]
    public void GetDuties_MoreThanTwoSupportSkills_SkipsRowWithWarning()
    {
        // Only [0] and [1] are ever read, and EffectiveSkill returns the better of the two. A third
        // entry looks like a third chance to the author and is dead data at runtime — the exact
        // shape of silence this provider exists to refuse.
        WriteDuties("{\"fieldDuties\":[{\"id\":\"too_many\",\"difficulty\":50,\"durationHours\":6,"
            + "\"supportSkills\":[\"Scouting\",\"Riding\",\"Athletics\"]}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(0, duties.FieldDuties.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("only the first two")));
    }

    [TestMethod]
    public void GetDuties_TwoSupportSkills_IsAccepted()
    {
        WriteDuties("{\"fieldDuties\":[{\"id\":\"pairing\",\"difficulty\":50,\"durationHours\":6,"
            + "\"supportSkills\":[\"Scouting\",\"Riding\"]}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(1, duties.FieldDuties.Count, "the two-skill pairing is the shipped shape");
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("supportSkills")));
    }

    [TestMethod]
    public void GetDuties_UnknownContext_SkipsRowWithWarning()
    {
        WriteDuties("{\"fieldDuties\":[{\"id\":\"bad_ctx\",\"difficulty\":55,\"durationHours\":6,\"supportSkills\":[\"Scouting\"],\"gates\":{\"requiredContexts\":[\"siegee\"]}}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(0, duties.FieldDuties.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("siegee")));
    }

    [TestMethod]
    public void GetDuties_ZeroDurationHours_SkipsRow()
    {
        // Every OTHER field is deliberately valid, so this pins the durationHours rule specifically.
        // The version this replaced wrote the retired keys (mechanic / targetKind / deadlineDays:0)
        // and omitted supportSkills — so the row was skipped by the support-skill rule and the test
        // passed while testing nothing about duration. A zero-hour duty resolves the instant it is
        // assigned, which is exactly what this must keep out of the data.
        WriteDuties("{\"fieldDuties\":[{\"id\":\"zero_hours\",\"difficulty\":55,\"durationHours\":0,"
            + "\"supportSkills\":[\"Scouting\"]}]}");

        Assert.AreEqual(0, Provider().GetDuties().FieldDuties.Count);
    }

    [TestMethod]
    public void GetDuties_NoSupportSkills_SkipsRow()
    {
        // A row with no skills rolls against skill 0 forever — silently unwinnable at any rank.
        WriteDuties("{\"fieldDuties\":[{\"id\":\"no_skills\",\"difficulty\":55,\"durationHours\":6}]}");

        Assert.AreEqual(0, Provider().GetDuties().FieldDuties.Count);
    }

    [TestMethod]
    public void GetDuties_ValidRow_IsKept()
    {
        // The positive control the three skip-tests need: proves they fail for their stated reason
        // and not because the fixture shape is wrong.
        WriteDuties("{\"fieldDuties\":[{\"id\":\"ok\",\"difficulty\":55,\"durationHours\":6,"
            + "\"supportSkills\":[\"Scouting\"]}]}");

        var duties = Provider().GetDuties().FieldDuties;
        Assert.AreEqual(1, duties.Count);
        Assert.AreEqual(6, duties[0].DurationHours);
        Assert.AreEqual(55, duties[0].Difficulty);
    }

    [TestMethod]
    public void GetDuties_DuplicateId_SkipsLaterRow()
    {
        WriteDuties("{\"fieldDuties\":[" +
            "{\"id\":\"road_patrol\",\"difficulty\":55,\"durationHours\":6,\"supportSkills\":[\"Scouting\"]}," +
            "{\"id\":\"road_patrol\",\"difficulty\":48,\"durationHours\":4,\"supportSkills\":[\"Steward\"]}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(1, duties.FieldDuties.Count);
        Assert.AreEqual(55, duties.FieldDuties[0].Difficulty, "the FIRST row wins a duplicate id");
    }

    [TestMethod]
    public void GetDuties_NegativeReward_SkipsRow()
    {
        WriteDuties("{\"fieldDuties\":[{\"id\":\"neg\",\"difficulty\":55,\"durationHours\":6,\"supportSkills\":[\"Scouting\"],\"reportReward\":{\"gold\":-5}}]}");

        Assert.AreEqual(0, Provider().GetDuties().FieldDuties.Count);
    }

    [TestMethod]
    public void GetDuties_ValidRows_Load()
    {
        WriteDuties("{\"fieldDuties\":[{\"id\":\"road_patrol\",\"difficulty\":55,\"durationHours\":6,\"supportSkills\":[\"Scouting\"],\"reportReward\":{\"serviceXp\":48,\"gold\":55}}]," +
            "\"incidents\":[{\"id\":\"pay_delay\",\"chance\":0.22,\"effect\":\"ReleaseDeferredPay\",\"optionA\":{\"key\":\"press\",\"skillId\":\"Charm\",\"difficulty\":65},\"optionB\":{\"key\":\"wait\",\"skillId\":\"Steward\",\"difficulty\":50}}]}");

        var duties = Provider().GetDuties();

        Assert.AreEqual(1, duties.FieldDuties.Count);
        Assert.AreEqual(1, duties.Incidents.Count);
        Assert.AreEqual("ReleaseDeferredPay", duties.Incidents[0].Effect);
    }

    [TestMethod]
    public void GetDuties_NaNIncidentChance_SkipsRow()
    {
        WriteDuties("{\"incidents\":[{\"id\":\"nan_chance\",\"chance\":\"NaN\",\"optionA\":{\"key\":\"a\",\"skillId\":\"Charm\",\"difficulty\":65},\"optionB\":{\"key\":\"b\",\"skillId\":\"Steward\",\"difficulty\":50}}]}");

        Assert.AreEqual(0, Provider().GetDuties().Incidents.Count);
    }

    // ---- desertion cost (#833) ----

    [TestMethod]
    public void DesertionDefaults_AreTheApprovedValues()
    {
        var d = EnlistmentContentConfigProvider.BuildDefaults();
        Assert.AreEqual(-15, d.DesertionRelationPenalty);
        Assert.AreEqual(-100, d.DesertionHonorXpPenalty);
    }

    [TestMethod]
    public void ShippedConfig_Desertion_EqualsTheCompiledDefaults()
    {
        var d = EnlistmentContentConfigProvider.BuildDefaults();
        var loaded = new EnlistmentContentConfigProvider(ShippedPaths(), _logger).GetConfig();
        Assert.AreEqual(d.DesertionRelationPenalty, loaded.DesertionRelationPenalty);
        Assert.AreEqual(d.DesertionHonorXpPenalty, loaded.DesertionHonorXpPenalty);
        var raw = JObject.Parse(File.ReadAllText(Path.Combine(ShippedModuleDataPath(), "enlistment", "enlistment_config.json")));
        Assert.IsNotNull(raw["desertionRelationPenalty"], "the key must be in the shipped file");
        Assert.IsNotNull(raw["desertionHonorXpPenalty"], "the key must be in the shipped file");
    }

    [DataTestMethod]
    [DataRow("{\"desertionRelationPenalty\":1}", "desertionRelationPenalty")]
    [DataRow("{\"desertionRelationPenalty\":-101}", "desertionRelationPenalty")]
    [DataRow("{\"desertionHonorXpPenalty\":1}", "desertionHonorXpPenalty")]
    [DataRow("{\"desertionHonorXpPenalty\":-1001}", "desertionHonorXpPenalty")]
    public void GetConfig_DesertionOutOfRange_RevertsWithWarning(string json, string key)
    {
        WriteConfig(json);

        var config = Provider().GetConfig();

        Assert.AreEqual(-15, config.DesertionRelationPenalty);
        Assert.AreEqual(-100, config.DesertionHonorXpPenalty);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(key)));
    }

    [TestMethod]
    public void GetConfig_DesertionRangeBoundaries_AreKept()
    {
        WriteConfig("{\"desertionRelationPenalty\":-100,\"desertionHonorXpPenalty\":-1000}");
        var config = Provider().GetConfig();
        Assert.AreEqual(-100, config.DesertionRelationPenalty);
        Assert.AreEqual(-1000, config.DesertionHonorXpPenalty);

        WriteConfig("{\"desertionRelationPenalty\":0,\"desertionHonorXpPenalty\":0}");
        config = new EnlistmentContentConfigProvider(_paths, _logger).GetConfig();
        Assert.AreEqual(0, config.DesertionRelationPenalty);
        Assert.AreEqual(0, config.DesertionHonorXpPenalty);
    }
}
