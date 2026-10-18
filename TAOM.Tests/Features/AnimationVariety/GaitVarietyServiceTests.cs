using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AnimationVariety;

namespace TAOM.Tests.Features.AnimationVariety;

[TestClass]
public class GaitVarietyServiceTests
{
    private const string Orc = "as_orc_warrior";
    private const string Goblin = "as_goblin_warrior";
    private const string GoblinF = "as_goblin_female_warrior";

    private sealed class RecordingLogger : IModLogger
    {
        public readonly List<string> Info = new();
        public readonly List<string> Warnings = new();
        public string? LogFilePath => null;
        public void LogInfo(string message) => Info.Add(message);
        public void LogDebug(string message) { }
        public void LogWarning(string message) => Warnings.Add(message);
        public void LogError(string message) => Warnings.Add(message);
        public void Dispose() { }
        public int Total => Info.Count + Warnings.Count;
    }

    private sealed class FakeAgent : IGaitVarietyAgent
    {
        public bool IsHuman { get; set; } = true;
        public bool IsMount { get; set; }
        public bool IsPlayerControlled { get; set; }
        public bool IsMounted { get; set; }
        public string ActionSetName { get; set; } = Orc;
        public string CharacterId { get; set; } = "orc_soldier";
        public int BodyPropertiesSeed { get; set; } = 1;
        public float VanillaStepSize { get; set; } = 0.9f;
        public Exception? ApplyThrows { get; set; }
        public readonly List<(string Set, float Step)> Applied = new();

        public void ApplyGait(string actionSetId, float stepSize)
        {
            if (ApplyThrows != null) throw ApplyThrows;
            Applied.Add((actionSetId, stepSize));
        }
    }

    private sealed class Rig
    {
        public readonly IGaitVarietyAdapter Engine = Substitute.For<IGaitVarietyAdapter>();
        public readonly IGaitVarietySettingsProvider Settings = Substitute.For<IGaitVarietySettingsProvider>();
        public readonly RecordingLogger Log = new();
        public readonly GaitVarietyService Service;

        public Rig(bool stepOn = true, float minFactor = 0.92f, bool gaitOn = true, params string[] existing)
        {
            Engine.LoadedActionSetNames().Returns(new List<string>(existing) { "as_human_warrior", Orc });
            Settings.RandomStepSize.Returns(stepOn);
            Settings.StepSizeMinFactor.Returns(minFactor);
            Settings.RandomGaitSet.Returns(gaitOn);
            Service = new GaitVarietyService(new GaitVariantCatalog(Engine), Settings, Log);
        }

        public FakeAgent Build(Action<FakeAgent>? configure = null)
        {
            var agent = new FakeAgent();
            configure?.Invoke(agent);
            Service.OnAgentBuild(agent);
            return agent;
        }
    }

    private static string[] AllVariants() => new[]
    {
        Orc + "_b", Orc + "_c", Goblin + "_b", Goblin + "_c", GoblinF + "_b", GoblinF + "_c",
    };

    // ------------------------------------------------------------------ eligibility

    [TestMethod]
    public void OnAgentBuild_NullAgent_DoesNothing()
    {
        var rig = new Rig();

        rig.Service.OnAgentBuild(null!);
        rig.Service.End();

        Assert.AreEqual(0, rig.Log.Total);
    }

    [DataTestMethod]
    [DataRow("as_human_warrior")]
    [DataRow("as_orc_female_warrior")]
    [DataRow("as_orc_villager")]
    [DataRow("as_orc_tavern")]
    [DataRow("as_goblin_hideout")]
    [DataRow("as_troll_warrior")]
    [DataRow("as_dwarf_warrior")]
    [DataRow("as_orc_warrior_b")]
    [DataRow("")]
    [DataRow(null)]
    public void OnAgentBuild_ASetOutsideTheThreeBaseSets_IsLeftAloneAndSilent(string? set)
    {
        var rig = new Rig(existing: AllVariants());

        var agent = rig.Build(a => a.ActionSetName = set!);
        rig.Service.End();

        Assert.AreEqual(0, agent.Applied.Count);
        Assert.AreEqual(0, rig.Log.Total, "no orc or goblin warrior spawned: nothing is logged at all");
    }

    [TestMethod]
    public void OnAgentBuild_NonHuman_IsSkipped()
    {
        var rig = new Rig(existing: AllVariants());

        var agent = rig.Build(a => a.IsHuman = false);
        rig.Service.End();

        Assert.AreEqual(0, agent.Applied.Count);
        Assert.AreEqual(0, rig.Log.Total);
    }

    [TestMethod]
    public void OnAgentBuild_Mount_IsSkipped()
    {
        var rig = new Rig(existing: AllVariants());

        var agent = rig.Build(a => a.IsMount = true);
        rig.Service.End();

        Assert.AreEqual(0, agent.Applied.Count);
        Assert.AreEqual(0, rig.Log.Total);
    }

    [TestMethod]
    public void OnAgentBuild_PlayerControlledAgent_IsSkipped()
    {
        var rig = new Rig(existing: AllVariants());

        var agent = rig.Build(a => a.IsPlayerControlled = true);
        rig.Service.End();

        Assert.AreEqual(0, agent.Applied.Count);
        Assert.AreEqual(0, rig.Log.Total);
    }

    [TestMethod]
    public void OnAgentBuild_RiderOnAMount_IsSkipped()
    {
        var rig = new Rig(existing: AllVariants());

        var agent = rig.Build(a => a.IsMounted = true);
        rig.Service.End();

        Assert.AreEqual(0, agent.Applied.Count);
        Assert.AreEqual(0, rig.Log.Total);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(0f)]
    [DataRow(-0.5f)]
    [DataRow(0.009f)]
    public void OnAgentBuild_VanillaStepSizeNotFiniteAndPositive_SkipsTheAgentAndWarnsOnce(float vanilla)
    {
        var rig = new Rig(existing: AllVariants());

        var first = rig.Build(a => a.VanillaStepSize = vanilla);
        var second = rig.Build(a => a.VanillaStepSize = vanilla);

        Assert.AreEqual(0, first.Applied.Count);
        Assert.AreEqual(0, second.Applied.Count);
        Assert.AreEqual(1, rig.Log.Warnings.Count(w => w.Contains("step size")), "one warning per mission");
    }

    [TestMethod]
    public void OnAgentBuild_VanillaStepSizeBadWithBothLeversOff_StillSkipsWithoutCallingTheEngine()
    {
        var rig = new Rig(stepOn: false, gaitOn: false);

        var agent = rig.Build(a => a.VanillaStepSize = float.NaN);

        Assert.AreEqual(0, agent.Applied.Count);
    }

    [TestMethod]
    public void OnAgentBuild_AFollowingGoodAgent_IsStillHandledAfterABadOne()
    {
        var rig = new Rig();

        rig.Build(a => a.VanillaStepSize = float.NaN);
        var good = rig.Build();

        Assert.AreEqual(1, good.Applied.Count);
    }

    // ------------------------------------------------------------------ lever 1: random step size

    [TestMethod]
    public void OnAgentBuild_StepLeverAlone_KeepsTheSetAndScalesTheVanillaStep()
    {
        var rig = new Rig(gaitOn: false, existing: AllVariants());

        var agent = rig.Build(a => a.VanillaStepSize = 0.9f);

        Assert.AreEqual(1, agent.Applied.Count, "ONE SetActionSet call");
        Assert.AreEqual(Orc, agent.Applied[0].Set);
        Assert.IsTrue(agent.Applied[0].Step >= 0.9f * 0.92f - 1e-6f && agent.Applied[0].Step <= 0.9f + 1e-6f,
            "step " + agent.Applied[0].Step);
    }

    [TestMethod]
    public void OnAgentBuild_StepFactor_StaysInsideMinAndOneForManySoldiers()
    {
        foreach (var min in new[] { 0.80f, 0.92f, 0.97f })
        {
            var rig = new Rig(gaitOn: false, minFactor: min);
            for (int seed = 0; seed < 600; seed++)
            {
                var agent = rig.Build(a => { a.BodyPropertiesSeed = seed * 7919; a.VanillaStepSize = 1f; });
                var step = agent.Applied[0].Step;
                Assert.IsTrue(step >= min && step <= 1f, $"min {min} seed {seed} step {step}");
            }
        }
    }

    [TestMethod]
    public void OnAgentBuild_StepFactor_SpreadsAcrossTheWholeRange()
    {
        var rig = new Rig(gaitOn: false, minFactor: 0.80f);
        var steps = new List<float>();
        for (int seed = 0; seed < 2000; seed++)
            steps.Add(rig.Build(a => { a.BodyPropertiesSeed = seed; a.VanillaStepSize = 1f; }).Applied[0].Step);

        Assert.IsTrue(steps.Min() < 0.83f, "low end reached: " + steps.Min());
        Assert.IsTrue(steps.Max() > 0.98f, "high end reached: " + steps.Max());
        Assert.IsTrue(Math.Abs(steps.Average() - 0.90) < 0.02, "mean near the middle: " + steps.Average());
    }

    [TestMethod]
    public void OnAgentBuild_MinFactorOne_LeavesTheVanillaStepUnchanged()
    {
        var rig = new Rig(gaitOn: false, minFactor: 1.0f);

        var agent = rig.Build(a => a.VanillaStepSize = 0.85f);

        Assert.AreEqual(0.85f, agent.Applied[0].Step);
        Assert.AreEqual(0, rig.Log.Warnings.Count, "1.00 is a valid minimum");
    }

    [TestMethod]
    public void OnAgentBuild_MinFactorAtTheFloor_IsAcceptedWithoutAWarning()
    {
        var rig = new Rig(gaitOn: false, minFactor: 0.80f);

        rig.Build();

        Assert.AreEqual(0, rig.Log.Warnings.Count);
    }

    [TestMethod]
    public void OnAgentBuild_SameSoldier_GetsTheSameStepEveryTime()
    {
        var a = new Rig(gaitOn: false).Build(x => { x.CharacterId = "orc_a"; x.BodyPropertiesSeed = 42; });
        var b = new Rig(gaitOn: false).Build(x => { x.CharacterId = "orc_a"; x.BodyPropertiesSeed = 42; });

        Assert.AreEqual(a.Applied[0].Step, b.Applied[0].Step);
    }

    [TestMethod]
    public void OnAgentBuild_DifferentSeedsOrCharacters_GetDifferentSteps()
    {
        var rig = new Rig(gaitOn: false);
        var bySeed = Enumerable.Range(0, 50)
            .Select(s => rig.Build(a => a.BodyPropertiesSeed = s).Applied[0].Step).Distinct().Count();
        var byCharacter = Enumerable.Range(0, 50)
            .Select(s => rig.Build(a => a.CharacterId = "orc_" + s).Applied[0].Step).Distinct().Count();

        Assert.IsTrue(bySeed > 25, "distinct steps by seed: " + bySeed);
        Assert.IsTrue(byCharacter > 25, "distinct steps by character: " + byCharacter);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(0.79f)]
    [DataRow(1.01f)]
    [DataRow(0f)]
    [DataRow(-1f)]
    [DataRow(5f)]
    public void OnAgentBuild_MinFactorOutsideItsRange_FallsBackToTheDefaultAndWarnsOnce(float bad)
    {
        var rig = new Rig(gaitOn: false, minFactor: bad);

        for (int seed = 0; seed < 300; seed++)
        {
            var step = rig.Build(a => { a.BodyPropertiesSeed = seed; a.VanillaStepSize = 1f; }).Applied[0].Step;
            Assert.IsTrue(step >= GaitVarietyService.DefaultMinFactor && step <= 1f, "step " + step);
        }
        var warnings = rig.Log.Warnings.Where(w => w.Contains("minimum factor")).ToList();
        Assert.AreEqual(1, warnings.Count);
        StringAssert.Contains(warnings[0], "[AnimVariety]");
        StringAssert.Contains(warnings[0], "0.920");
    }

    [TestMethod]
    public void OnAgentBuild_ADefaultedMinFactor_ShowsAsTheDefaultInTheSettingsLine()
    {
        var rig = new Rig(gaitOn: false, minFactor: float.NaN);

        rig.Build();

        Assert.IsTrue(rig.Log.Info.Any(l => l.Contains("minFactor=0.920")));
    }

    [TestMethod]
    public void OnAgentBuild_StepLeverOff_PassesTheVanillaStepWhenTheSetChanges()
    {
        var rig = new Rig(stepOn: false, existing: AllVariants());

        var changed = Enumerable.Range(0, 200)
            .Select(s => rig.Build(a => { a.BodyPropertiesSeed = s; a.VanillaStepSize = 0.87f; }))
            .Where(a => a.Applied.Count == 1).ToList();

        Assert.IsTrue(changed.Count > 50);
        Assert.IsTrue(changed.All(a => a.Applied[0].Step == 0.87f));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(0.5f)]
    [DataRow(5f)]
    public void OnAgentBuild_BadMinFactorWithTheStepLeverOff_DoesNotWarn(float bad)
    {
        var rig = new Rig(stepOn: false, minFactor: bad, gaitOn: true, existing: AllVariants());

        for (int seed = 0; seed < 20; seed++) rig.Build(a => a.BodyPropertiesSeed = seed);
        rig.Service.End();

        Assert.AreEqual(0, rig.Log.Warnings.Count, "the factor is unused while the step lever is off");
        Assert.AreEqual(1, rig.Log.Info.Count(l => l.Contains("gait settings randomStepSize=False")));
    }

    [TestMethod]
    public void OnAgentBuild_BadMinFactorWithTheStepLeverOff_ShowsTheRawValueInTheSettingsLine()
    {
        var rig = new Rig(stepOn: false, minFactor: 0.5f, gaitOn: true);

        rig.Build();

        Assert.IsTrue(rig.Log.Info.Contains("[AnimVariety] gait settings randomStepSize=False minFactor=0.500 randomGaitSet=True"));
    }

    // ------------------------------------------------------------------ lever 2: random gait set

    [TestMethod]
    public void OnAgentBuild_GaitLeverWithEveryVariant_PicksBaseAndBothVariantsInEqualShares()
    {
        var rig = new Rig(stepOn: false, existing: AllVariants());
        int baseCount = 0, b = 0, c = 0;
        for (int seed = 0; seed < 3000; seed++)
        {
            var agent = rig.Build(a => a.BodyPropertiesSeed = seed * 31 + 5);
            if (agent.Applied.Count == 0) { baseCount++; continue; }
            if (agent.Applied[0].Set == Orc + "_b") b++;
            else if (agent.Applied[0].Set == Orc + "_c") c++;
            else Assert.Fail("unexpected set " + agent.Applied[0].Set);
        }

        foreach (var share in new[] { baseCount, b, c })
            Assert.IsTrue(share > 800 && share < 1200, $"base {baseCount} b {b} c {c}");
    }

    [DataTestMethod]
    [DataRow(Orc)]
    [DataRow(Goblin)]
    [DataRow(GoblinF)]
    public void OnAgentBuild_GaitLever_OnlyEverChoosesFromTheAgentsOwnBaseSet(string baseSet)
    {
        var rig = new Rig(stepOn: false, existing: AllVariants());
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 300; seed++)
        {
            var agent = rig.Build(a => { a.ActionSetName = baseSet; a.BodyPropertiesSeed = seed; });
            foreach (var applied in agent.Applied) seen.Add(applied.Set);
        }

        CollectionAssert.AreEquivalent(new[] { baseSet + "_b", baseSet + "_c" }, seen.ToArray());
    }

    [TestMethod]
    public void OnAgentBuild_GaitLeverWithOnlyBPresent_ChoosesBetweenBaseAndB()
    {
        var rig = new Rig(stepOn: false, existing: Orc + "_b");
        var seen = new HashSet<string>();
        int none = 0;
        for (int seed = 0; seed < 300; seed++)
        {
            var agent = rig.Build(a => a.BodyPropertiesSeed = seed);
            if (agent.Applied.Count == 0) none++;
            foreach (var applied in agent.Applied) seen.Add(applied.Set);
        }

        CollectionAssert.AreEqual(new[] { Orc + "_b" }, seen.ToArray());
        Assert.IsTrue(none > 100 && none < 200, "base picks: " + none);
    }

    [TestMethod]
    public void OnAgentBuild_GaitLeverAndNoVariantPresent_AppliesNothingAndWritesNoSummary()
    {
        var rig = new Rig(stepOn: false);

        for (int seed = 0; seed < 20; seed++)
            Assert.AreEqual(0, rig.Build(a => a.BodyPropertiesSeed = seed).Applied.Count);
        rig.Service.End();

        Assert.IsFalse(rig.Log.Info.Any(l => l.Contains("gait set=")), "nothing was applied");
    }

    [TestMethod]
    public void OnAgentBuild_GaitLeverPickingTheBaseSet_MakesNoEngineCallWhenTheStepLeverIsOff()
    {
        var rig = new Rig(stepOn: false, existing: Orc + "_b");
        var picks = Enumerable.Range(0, 100)
            .Select(s => rig.Build(a => a.BodyPropertiesSeed = s)).ToList();

        Assert.IsTrue(picks.Any(a => a.Applied.Count == 0));
        Assert.IsTrue(picks.All(a => a.Applied.Count <= 1));
    }

    [TestMethod]
    public void OnAgentBuild_GaitLeverPickingTheBaseSet_StillCountsItInTheSummary()
    {
        var rig = new Rig(stepOn: false, existing: Orc + "_b");
        for (int seed = 0; seed < 100; seed++)
            rig.Build(a => a.BodyPropertiesSeed = seed);
        rig.Service.End();

        var line = rig.Log.Info.Single(l => l.Contains("gait set=" + Orc));
        StringAssert.Contains(line, "n=100");
        Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(line, @" " + Orc + @"=\d+ "), line);
        Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(line, @" " + Orc + @"_b=\d+"), line);
    }

    // ------------------------------------------------------------------ both levers

    [TestMethod]
    public void OnAgentBuild_BothLevers_MakeExactlyOneEngineCallCarryingTheSetAndTheStep()
    {
        var rig = new Rig(existing: AllVariants());
        var variantSteps = new List<(string Set, float Step)>();
        for (int seed = 0; seed < 200; seed++)
        {
            var agent = rig.Build(a => { a.BodyPropertiesSeed = seed; a.VanillaStepSize = 1f; });
            Assert.AreEqual(1, agent.Applied.Count);
            variantSteps.Add(agent.Applied[0]);
        }

        Assert.AreEqual(3, variantSteps.Select(v => v.Set).Distinct().Count());
        Assert.IsTrue(variantSteps.All(v => v.Step >= 0.92f && v.Step <= 1f));
    }

    [TestMethod]
    public void OnAgentBuild_BothLevers_ADrawnBaseSetStillAppliesTheNewStep()
    {
        var rig = new Rig(existing: Orc + "_b");

        var applied = Enumerable.Range(0, 100)
            .Select(s => rig.Build(a => a.BodyPropertiesSeed = s).Applied.Single()).ToList();

        Assert.IsTrue(applied.Any(a => a.Set == Orc), "the base set was drawn for some soldiers");
        Assert.IsTrue(applied.Any(a => a.Set == Orc + "_b"));
    }

    [TestMethod]
    public void OnAgentBuild_BothLeversOff_AppliesNothingButStillLogsTheSettingsOnce()
    {
        var rig = new Rig(stepOn: false, gaitOn: false, existing: AllVariants());

        var agent = rig.Build();
        rig.Build();
        rig.Service.End();

        Assert.AreEqual(0, agent.Applied.Count);
        Assert.AreEqual(1, rig.Log.Info.Count(l => l.Contains("gait settings")));
        Assert.IsFalse(rig.Log.Info.Any(l => l.Contains("gait set=")));
    }

    [TestMethod]
    public void OnAgentBuild_LeversReadForEachAgent_SoAToggleTakesEffectOnTheNextSpawn()
    {
        var rig = new Rig(stepOn: true, gaitOn: false);
        Assert.AreEqual(1, rig.Build().Applied.Count);

        rig.Settings.RandomStepSize.Returns(false);

        Assert.AreEqual(0, rig.Build().Applied.Count);
    }

    // ------------------------------------------------------------------ the pure draw

    [TestMethod]
    public void Seed_IsStableForTheSameCharacterAndBodySeed()
    {
        Assert.AreEqual(GaitVarietyService.Seed("orc_a", 9), GaitVarietyService.Seed("orc_a", 9));
    }

    [TestMethod]
    public void Seed_ChangesWithTheCharacterAndWithTheBodySeed()
    {
        Assert.AreNotEqual(GaitVarietyService.Seed("orc_a", 9), GaitVarietyService.Seed("orc_b", 9));
        Assert.AreNotEqual(GaitVarietyService.Seed("orc_a", 9), GaitVarietyService.Seed("orc_a", 10));
    }

    [TestMethod]
    public void Seed_NullOrEmptyCharacterId_DoesNotThrowAndAgreesWithEachOther()
    {
        Assert.AreEqual(GaitVarietyService.Seed(null!, 3), GaitVarietyService.Seed("", 3));
    }

    [TestMethod]
    public void Seed_NegativeBodySeed_Works()
    {
        Assert.AreNotEqual(GaitVarietyService.Seed("orc_a", -1), GaitVarietyService.Seed("orc_a", 1));
    }

    [TestMethod]
    public void UnitFromSeed_IsAlwaysAtLeastZeroAndBelowOne()
    {
        foreach (var seed in new uint[] { 0, 1, 2, uint.MaxValue, uint.MaxValue - 1, 0x80000000u, 123456789u })
        {
            var u = GaitVarietyService.UnitFromSeed(seed);
            Assert.IsTrue(u >= 0f && u < 1f, $"seed {seed} gave {u}");
        }
        for (uint seed = 0; seed < 5000; seed++)
        {
            var u = GaitVarietyService.UnitFromSeed(seed * 2654435761u);
            Assert.IsTrue(u >= 0f && u < 1f);
        }
    }

    [TestMethod]
    public void PickIndex_StaysInsideTheCountAndReachesEveryIndex()
    {
        foreach (var count in new[] { 1, 2, 3 })
        {
            var seen = new HashSet<int>();
            for (uint seed = 0; seed < 2000; seed++)
            {
                var index = GaitVarietyService.PickIndex(seed * 2246822519u, count);
                Assert.IsTrue(index >= 0 && index < count);
                seen.Add(index);
            }
            Assert.AreEqual(count, seen.Count);
        }
    }

    [TestMethod]
    public void PickIndex_AndUnitFromSeed_AreNotTheSameDraw()
    {
        // The variant must not be a function of the step factor alone: soldiers with the same factor
        // bucket would then always share a variant.
        var pairs = new HashSet<(int, int)>();
        for (uint seed = 0; seed < 3000; seed++)
        {
            var s = seed * 2246822519u + 17;
            pairs.Add(((int)(GaitVarietyService.UnitFromSeed(s) * 4), GaitVarietyService.PickIndex(s, 3)));
        }

        Assert.AreEqual(12, pairs.Count);
    }

    // ------------------------------------------------------------------ logging

    [TestMethod]
    public void OnAgentBuild_FirstEligibleAgent_LogsTheSettingsLineOnce()
    {
        var rig = new Rig(existing: AllVariants());

        rig.Build();
        rig.Build();

        var lines = rig.Log.Info.Where(l => l.Contains("gait settings")).ToList();
        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual("[AnimVariety] gait settings randomStepSize=True minFactor=0.920 randomGaitSet=True", lines[0]);
    }

    [TestMethod]
    public void OnAgentBuild_SettingsLine_ShowsTheLeversAsSet()
    {
        var rig = new Rig(stepOn: false, minFactor: 0.8f, gaitOn: true);

        rig.Build();

        Assert.IsTrue(rig.Log.Info.Contains("[AnimVariety] gait settings randomStepSize=False minFactor=0.800 randomGaitSet=True"));
    }

    [TestMethod]
    public void OnAgentBuild_FirstEligibleAgent_LogsWhichVariantsExistForEveryBaseSet()
    {
        var rig = new Rig(existing: new[] { Orc + "_b" });

        rig.Build();

        var line = rig.Log.Info.Single(l => l.Contains("gait variants"));
        Assert.AreEqual(
            "[AnimVariety] gait variants as_orc_warrior_b=present as_orc_warrior_c=missing " +
            "as_goblin_warrior_b=missing as_goblin_warrior_c=missing " +
            "as_goblin_female_warrior_b=missing as_goblin_female_warrior_c=missing", line);
    }

    [TestMethod]
    public void OnAgentBuild_VariantsLine_IsWrittenOncePerMission()
    {
        var rig = new Rig();

        rig.Build();
        rig.Build(a => a.ActionSetName = Goblin);

        Assert.AreEqual(1, rig.Log.Info.Count(l => l.Contains("gait variants")));
    }

    [TestMethod]
    public void OnAgentBuild_MissingVariants_NeverWarnAndTheEngineIsListedOncePerProcess()
    {
        var engine = Substitute.For<IGaitVarietyAdapter>();
        engine.LoadedActionSetNames().Returns(new List<string> { "as_human_warrior", Orc });
        var settings = Substitute.For<IGaitVarietySettingsProvider>();
        settings.RandomStepSize.Returns(false);
        settings.StepSizeMinFactor.Returns(0.92f);
        settings.RandomGaitSet.Returns(true);
        var log = new RecordingLogger();
        var catalog = new GaitVariantCatalog(engine);

        for (int mission = 0; mission < 3; mission++)
        {
            var service = new GaitVarietyService(catalog, settings, log);
            for (int i = 0; i < 10; i++) service.OnAgentBuild(new FakeAgent());
            service.End();
        }

        Assert.AreEqual(0, log.Warnings.Count, "the INFO =missing line is the report; the engine logs nothing");
        Assert.AreEqual(3, log.Info.Count(l => l.Contains("as_orc_warrior_b=missing")), "one variants line per mission");
        engine.Received(1).LoadedActionSetNames();
    }

    [TestMethod]
    public void OnAgentBuild_NoAgentOfTheThreeSets_LogsNothingAndEndIsSilent()
    {
        var rig = new Rig();

        rig.Build(a => a.ActionSetName = "as_human_warrior");
        rig.Service.End();

        Assert.AreEqual(0, rig.Log.Total);
    }

    // ------------------------------------------------------------------ the mission summary

    [TestMethod]
    public void End_StepLeverAlone_WritesOneExactLinePerBaseSet()
    {
        var rig = new Rig(gaitOn: false, minFactor: 1.0f);
        rig.Build(a => a.VanillaStepSize = 0.9f);
        rig.Build(a => a.VanillaStepSize = 1.0f);

        rig.Service.End();

        Assert.IsTrue(rig.Log.Info.Contains(
            "[AnimVariety] gait set=as_orc_warrior n=2 variants as_orc_warrior=2 step n=2 min=0.900 max=1.000 mean=0.950"),
            string.Join("\n", rig.Log.Info));
    }

    [TestMethod]
    public void End_GaitLeverAlone_ReportsStepOffAndEveryChosenVariantCount()
    {
        var rig = new Rig(stepOn: false, existing: AllVariants());
        for (int seed = 0; seed < 90; seed++)
            rig.Build(a => a.BodyPropertiesSeed = seed);

        rig.Service.End();

        var line = rig.Log.Info.Single(l => l.StartsWith("[AnimVariety] gait set="));
        StringAssert.Matches(line,
            new System.Text.RegularExpressions.Regex(
                @"^\[AnimVariety\] gait set=as_orc_warrior n=90 variants as_orc_warrior=\d+ as_orc_warrior_b=\d+ as_orc_warrior_c=\d+ step off$"));
        var counts = System.Text.RegularExpressions.Regex.Matches(line, @"=(\d+)").Cast<System.Text.RegularExpressions.Match>()
            .Skip(1).Select(m => int.Parse(m.Groups[1].Value)).ToList();
        Assert.AreEqual(90, counts.Sum());
    }

    [TestMethod]
    public void End_WritesOneLinePerBaseSetThatSpawned_InOrdinalOrder()
    {
        var rig = new Rig(gaitOn: false, minFactor: 1.0f);
        rig.Build(a => a.ActionSetName = Goblin);
        rig.Build(a => a.ActionSetName = Orc);
        rig.Build(a => a.ActionSetName = GoblinF);

        rig.Service.End();

        var sets = rig.Log.Info.Where(l => l.StartsWith("[AnimVariety] gait set="))
            .Select(l => l.Split(' ')[2]).ToArray();
        CollectionAssert.AreEqual(
            new[] { "set=as_goblin_female_warrior", "set=as_goblin_warrior", "set=as_orc_warrior" }, sets);
    }

    [TestMethod]
    public void End_StepStatistics_UseInvariantCultureWithThreeDecimals()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var rig = new Rig(gaitOn: false, minFactor: 1.0f);
            rig.Build(a => a.VanillaStepSize = 0.8125f);

            rig.Service.End();

            Assert.IsTrue(rig.Log.Info.Any(l => l.Contains("min=0.813 max=0.813 mean=0.813")),
                string.Join("\n", rig.Log.Info));
            Assert.IsTrue(rig.Log.Info.Any(l => l.Contains("minFactor=1.000")));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [TestMethod]
    public void End_CalledTwice_WritesTheSummaryOnce()
    {
        var rig = new Rig(gaitOn: false);
        rig.Build();

        rig.Service.End();
        rig.Service.End();

        Assert.AreEqual(1, rig.Log.Info.Count(l => l.StartsWith("[AnimVariety] gait set=")));
    }

    [TestMethod]
    public void OnAgentBuild_AfterEnd_IsIgnored()
    {
        var rig = new Rig();
        rig.Service.End();

        var late = rig.Build();

        Assert.AreEqual(0, late.Applied.Count);
        Assert.AreEqual(0, rig.Log.Total);
    }

    [TestMethod]
    public void End_WhenNothingWasApplied_WritesNoSummaryLine()
    {
        var rig = new Rig(stepOn: false, gaitOn: false);
        rig.Build();

        rig.Service.End();

        Assert.IsFalse(rig.Log.Info.Any(l => l.StartsWith("[AnimVariety] gait set=")));
    }

    [TestMethod]
    public void OnAgentBuild_WhenTheEngineCallThrows_PropagatesAndTheAgentIsNotCounted()
    {
        var rig = new Rig(gaitOn: false, minFactor: 1.0f);
        var boom = new InvalidOperationException("native");

        var thrown = Assert.ThrowsException<InvalidOperationException>(() => rig.Build(a => a.ApplyThrows = boom));
        rig.Build(a => a.VanillaStepSize = 1f);
        rig.Service.End();

        Assert.AreSame(boom, thrown);
        StringAssert.Contains(rig.Log.Info.Single(l => l.StartsWith("[AnimVariety] gait set=")), "n=1 ");
    }
}
