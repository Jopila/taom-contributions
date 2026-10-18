using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AnimationVariety;

namespace TAOM.Tests.Features.AnimationVariety;

[TestClass]
public class AnimVarietyServiceTests
{
    private const string Base = "act_release_overswing_1h";
    private const string Alt = "act_taom_release_overswing_1h_b";
    private const string AltC = "act_taom_release_overswing_1h_c";
    private const string Ready = "act_ready_overswing_1h";

    private const int BaseIdx = 1;
    private const int AltIdx = 2;
    private const int AltCIdx = 3;
    private const int ReadyIdx = 4;

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
        public IEnumerable<string> All => Info.Concat(Warnings);
    }

    private sealed class FakeAgent : IAnimVarietyAgent
    {
        public object Handle { get; } = new object();
        public bool IsActive { get; set; } = true;
        public int ActionSetKey { get; set; }
        public int Channel1ActionIndex { get { ActionReads++; return Action; } }
        public bool NullActionName { get; set; }
        public string Channel1ActionName => NullActionName ? null! : Action switch
        {
            BaseIdx => Base, AltIdx => Alt, AltCIdx => AltC, ReadyIdx => Ready, _ => "act_other",
        };
        public bool Channel1IsAttackRelease { get; set; }
        public float Channel1Progress { get; set; }
        public int Action { get; set; } = ReadyIdx;
        public int ActionReads { get; private set; }

        public void Swing(int action) { Action = action; Channel1IsAttackRelease = true; }
        public void Idle() { Action = ReadyIdx; Channel1IsAttackRelease = false; }
    }

    private sealed class Rig
    {
        public readonly IAnimVarietyAdapter Engine = Substitute.For<IAnimVarietyAdapter>();
        public readonly IAnimVarietyProbeSettingsProvider Settings = Substitute.For<IAnimVarietyProbeSettingsProvider>();
        public readonly RecordingLogger Log = new();
        public readonly AnimVarietyService Service;
        public readonly List<IAnimVarietyAgent> Agents = new();
        public int SourceCalls;

        public Rig(params string[] definedCodes)
        {
            var defined = new HashSet<string>(definedCodes);
            Settings.SwingAlternativeProbe.Returns(true);
            Engine.ActionCodeExists(Arg.Any<string>()).Returns(c => defined.Contains(c.Arg<string>()));
            Engine.ActionSetName(Arg.Any<int>()).Returns(c => c.Arg<int>() == 7 ? "as_orc_warrior" : "as_set" + c.Arg<int>());
            Engine.AreAlternatives(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
            Engine.ClipExists(Arg.Any<int>(), Arg.Any<string>()).Returns(true);
            Engine.ClipName(Arg.Any<int>(), Arg.Any<string>()).Returns(c => "clip_" + c.ArgAt<string>(1));
            Engine.ClipDuration(Arg.Any<int>(), Arg.Any<string>()).Returns(1.5f);
            Engine.ClipBlendOutStart(Arg.Any<int>(), Arg.Any<string>()).Returns(0.8f);
            Service = new AnimVarietyService(Engine, Settings, Log);
        }

        public void Tick(float dt = 0.016f) => Service.Tick(dt, Source);

        private IReadOnlyList<IAnimVarietyAgent> Source()
        {
            SourceCalls++;
            return Agents;
        }
    }

    private static Rig WithGroup() => new Rig(Base, Alt);

    private static int GroupLines(Rig rig) => rig.Log.Info.Count(l => l.StartsWith("[AnimVariety] group "));

    private static int SwingLines(Rig rig) => rig.Log.Info.Count(l => l.StartsWith("[AnimVariety] swings "));

    // ------------------------------------------------------------------ group check

    [TestMethod]
    public void Tick_NoAltCodeDefined_LogsOffLineOnceAndNeverReadsAgents()
    {
        var rig = new Rig(Base);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();
        rig.Tick();

        var lines = rig.Log.Info.Where(l => l == "[AnimVariety] no alternative groups found; swing tracking off").ToList();
        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual(0, rig.SourceCalls);
        Assert.IsFalse(rig.Service.IsTracking);
    }

    [TestMethod]
    public void Tick_BaseCodeMissing_SkipsTheCombination()
    {
        var rig = new Rig(Alt);

        rig.Tick();

        Assert.IsTrue(rig.Log.Info.Any(l => l.Contains("no alternative groups found")));
    }

    [TestMethod]
    public void Tick_FirstSightOfASet_LogsOneGroupLinePerDefinedAltWithAllFields()
    {
        var rig = new Rig(Base, Alt);
        rig.Engine.ClipDuration(7, Base).Returns(1.234f);
        rig.Engine.ClipDuration(7, Alt).Returns(1.24f);
        rig.Engine.ClipBlendOutStart(7, Base).Returns(0.75f);
        rig.Engine.ClipBlendOutStart(7, Alt).Returns(0.755f);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        var group = rig.Log.Info.Where(l => l.StartsWith("[AnimVariety] group ")).ToList();
        Assert.AreEqual(1, group.Count);
        Assert.AreEqual(
            "[AnimVariety] group set=as_orc_warrior base=act_release_overswing_1h alt=act_taom_release_overswing_1h_b " +
            "alternatives=True baseClip=clip_act_release_overswing_1h baseClipExists=True baseDuration=1.234 " +
            "baseBlendOut=0.750 altClip=clip_act_taom_release_overswing_1h_b altClipExists=True altDuration=1.240 " +
            "altBlendOut=0.755 note=the melee table row of the alt code cannot be checked from managed code; " +
            "a missing row crashes the first swing",
            group[0]);
        Assert.AreEqual(0, rig.Log.Warnings.Count);
    }

    [TestMethod]
    public void Tick_UndefinedAlts_AreSilentlySkipped()
    {
        var rig = new Rig(Base, Alt, AltC);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        Assert.AreEqual(2, GroupLines(rig));
        Assert.IsFalse(rig.Log.All.Any(l => l.Contains("_d ")));
    }

    [TestMethod]
    public void Tick_EveryFamilyDirectionAndStanceSuffix_IsProbed()
    {
        var all = new List<string>();
        foreach (var family in new[] { "release", "quick_release", "blocked", "quick_blocked" })
        foreach (var dir in new[] { "overswing", "slashleft", "slashright", "thrust" })
        foreach (var suffix in new[] { "1h", "1h_left_stance" })
        {
            all.Add($"act_{family}_{dir}_{suffix}");
            all.Add($"act_taom_{family}_{dir}_{suffix}_h");
        }
        var rig = new Rig(all.ToArray());
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        Assert.AreEqual(32, GroupLines(rig));
    }

    [TestMethod]
    public void Tick_SameSetTwice_ChecksTheGroupOnce()
    {
        var rig = WithGroup();
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();
        rig.Tick();

        Assert.AreEqual(1, GroupLines(rig));
        rig.Engine.Received(1).AreAlternatives(7, Base, Alt);
    }

    [TestMethod]
    public void Tick_NewSetLater_IsCheckedWhenFirstSeen()
    {
        var rig = WithGroup();
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });
        rig.Tick();

        rig.Agents.Add(new FakeAgent { ActionSetKey = 9 });
        rig.Tick();

        Assert.AreEqual(2, GroupLines(rig));
        Assert.IsTrue(rig.Log.Info.Any(l => l.StartsWith("[AnimVariety] group set=as_set9 ")));
    }

    [TestMethod]
    public void Tick_SetWithNoName_IsLoggedAsUnnamed()
    {
        var rig = WithGroup();
        rig.Engine.ActionSetName(7).Returns((string)null!);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        Assert.IsTrue(rig.Log.Info.Any(l => l.StartsWith("[AnimVariety] group set=<unnamed> ")));
    }

    [TestMethod]
    public void Tick_NotAlternatives_WarnsGroupDisabled()
    {
        var rig = WithGroup();
        rig.Engine.AreAlternatives(7, Base, Alt).Returns(false);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        Assert.IsTrue(rig.Log.Warnings.Any(w => w.Contains("WARNING") && w.Contains("alt=" + Alt) &&
            w.Contains("group disabled: one member, or the base row lacks alternative_group")));
        Assert.IsTrue(rig.Log.Info.Any(l => l.Contains("alternatives=False")));
    }

    [TestMethod]
    public void Tick_AltClipMissing_WarnsTheGroupIsDisabledForThatCodeAndNotACrashRisk()
    {
        // The parser writes the {clip, group} slot only after the clip resolves, so the group stays -1 and
        // the engine's random pick never returns this code.
        var rig = WithGroup();
        rig.Engine.ClipExists(7, Alt).Returns(false);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        Assert.IsTrue(rig.Log.Warnings.Any(w => w.Contains("alt=" + Alt) && w.Contains("no clip for the alt code") &&
            w.Contains("group disabled for that code") && w.Contains("not a crash risk")));
        Assert.IsFalse(rig.Log.Warnings.Any(w => w.Contains("CRASH RISK")));
        Assert.IsTrue(rig.Log.Info.Any(l => l.Contains("altClipExists=False") && l.Contains("altBlendOut=n/a")));
        Assert.IsFalse(rig.Log.Warnings.Any(w => w.Contains("TIMING RISK")));
    }

    [TestMethod]
    public void Tick_BaseClipMissing_WarnsCrashRisk()
    {
        var rig = WithGroup();
        rig.Engine.ClipExists(7, Base).Returns(false);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        Assert.AreEqual(1, rig.Log.Warnings.Count(w => w.Contains("CRASH RISK: no clip for the base code") && w.Contains("base=" + Base)));
        Assert.IsTrue(rig.Log.Info.Any(l => l.Contains("baseClipExists=False") && l.Contains("baseClip=none") && l.Contains("baseDuration=n/a")));
        Assert.IsFalse(rig.Log.Warnings.Any(w => w.Contains("TIMING RISK")));
    }

    [TestMethod]
    public void Tick_BothClipsMissing_WarnsBaseAsCrashRiskAndAltAsDisabledAndNeverComparesTiming()
    {
        var rig = WithGroup();
        rig.Engine.ClipExists(7, Base).Returns(false);
        rig.Engine.ClipExists(7, Alt).Returns(false);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        Assert.AreEqual(2, rig.Log.Warnings.Count);
        Assert.AreEqual(1, rig.Log.Warnings.Count(w => w.Contains("CRASH RISK")));
        rig.Engine.DidNotReceiveWithAnyArgs().ClipDuration(default, default!);
        rig.Engine.DidNotReceiveWithAnyArgs().ClipBlendOutStart(default, default!);
    }

    [TestMethod]
    public void Tick_DurationsDifferByMoreThanTolerance_WarnsTimingRisk()
    {
        var rig = WithGroup();
        rig.Engine.ClipDuration(7, Alt).Returns(1.6f);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        Assert.IsTrue(rig.Log.Warnings.Any(w => w.Contains("TIMING RISK") && w.Contains("alt=" + Alt)));
    }

    [TestMethod]
    public void Tick_DurationsWithinTolerance_NoTimingWarning()
    {
        var rig = WithGroup();
        rig.Engine.ClipDuration(7, Alt).Returns(1.505f);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        Assert.AreEqual(0, rig.Log.Warnings.Count);
    }

    [TestMethod]
    public void Tick_NaNDuration_WarnsTimingRisk()
    {
        var rig = WithGroup();
        rig.Engine.ClipDuration(7, Alt).Returns(float.NaN);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        Assert.IsTrue(rig.Log.Warnings.Any(w => w.Contains("TIMING RISK")));
    }

    [TestMethod]
    public void Tick_BlendOutStartDiffersWhileDurationsMatch_WarnsTimingRisk()
    {
        // Hit timing is the original clip's duration minus its blend-out period, so a different blend-out
        // start moves the hit even when the clips are equally long.
        var rig = WithGroup();
        rig.Engine.ClipBlendOutStart(7, Alt).Returns(0.85f);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        var warning = rig.Log.Warnings.Single(w => w.Contains("TIMING RISK"));
        StringAssert.Contains(warning, "alt=" + Alt);
        StringAssert.Contains(warning, "blend-out");
        StringAssert.Contains(warning, "0.800");
        StringAssert.Contains(warning, "0.850");
    }

    [TestMethod]
    public void Tick_BlendOutStartWithinTolerance_NoTimingWarning()
    {
        var rig = WithGroup();
        rig.Engine.ClipBlendOutStart(7, Alt).Returns(0.805f);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        Assert.AreEqual(0, rig.Log.Warnings.Count);
    }

    [TestMethod]
    public void Tick_NaNBlendOutStart_WarnsTimingRisk()
    {
        var rig = WithGroup();
        rig.Engine.ClipBlendOutStart(7, Base).Returns(float.NaN);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        Assert.IsTrue(rig.Log.Warnings.Any(w => w.Contains("TIMING RISK")));
    }

    [TestMethod]
    public void Tick_DurationsAndBlendOutBothDiffer_WarnsOnce()
    {
        var rig = WithGroup();
        rig.Engine.ClipDuration(7, Alt).Returns(1.9f);
        rig.Engine.ClipBlendOutStart(7, Alt).Returns(0.5f);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        rig.Tick();

        Assert.AreEqual(1, rig.Log.Warnings.Count(w => w.Contains("TIMING RISK")));
    }

    // ------------------------------------------------------------------ swing tracking

    [TestMethod]
    public void Tick_EntryToAttackRelease_CountsTheSwingOnceNotWhileItContinues()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();

        agent.Swing(BaseIdx);
        rig.Tick();
        rig.Tick();
        agent.Idle();
        rig.Tick();
        agent.Swing(AltIdx);
        rig.Tick();

        rig.Service.End();
        var swings = rig.Log.Info.Single(l => l.StartsWith("[AnimVariety] swings "));
        Assert.AreEqual(
            "[AnimVariety] swings set=as_orc_warrior act_release_overswing_1h=1 (50%) act_taom_release_overswing_1h_b=1 (50%)",
            swings);
    }

    [TestMethod]
    public void Tick_SameCodeAfterIdle_CountsAgain()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();

        for (int i = 0; i < 3; i++)
        {
            agent.Swing(BaseIdx);
            rig.Tick();
            agent.Idle();
            rig.Tick();
        }

        rig.Service.End();
        Assert.IsTrue(rig.Log.Info.Any(l => l == "[AnimVariety] swings set=as_orc_warrior act_release_overswing_1h=3 (100%)"));
    }

    [TestMethod]
    public void Tick_ActionChangeThenStageRelease_CountsOnce()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();

        agent.Action = BaseIdx;
        agent.Channel1IsAttackRelease = false;
        rig.Tick();
        agent.Channel1IsAttackRelease = true;
        rig.Tick();
        rig.Tick();

        rig.Service.End();
        Assert.IsTrue(rig.Log.Info.Any(l => l == "[AnimVariety] swings set=as_orc_warrior act_release_overswing_1h=1 (100%)"));
    }

    [TestMethod]
    public void Tick_SwingShares_UseRoundedWholeNumbers()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();
        for (int i = 0; i < 14; i++) { agent.Swing(BaseIdx); rig.Tick(); agent.Idle(); rig.Tick(); }
        for (int i = 0; i < 12; i++) { agent.Swing(AltIdx); rig.Tick(); agent.Idle(); rig.Tick(); }

        rig.Service.End();

        Assert.IsTrue(rig.Log.Info.Any(l => l ==
            "[AnimVariety] swings set=as_orc_warrior act_release_overswing_1h=14 (54%) act_taom_release_overswing_1h_b=12 (46%)"));
    }

    [TestMethod]
    public void Tick_SwingWithNoActionName_CountsUnderActNone()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();

        agent.NullActionName = true;
        agent.Swing(BaseIdx);
        rig.Tick();

        rig.Service.End();
        Assert.IsTrue(rig.Log.Info.Any(l => l == "[AnimVariety] swings set=as_orc_warrior act_none=1 (100%)"));
    }

    [TestMethod]
    public void Tick_SetWithoutValidGroup_IsNeverRead()
    {
        var rig = WithGroup();
        rig.Engine.AreAlternatives(9, Base, Alt).Returns(false);
        var untracked = new FakeAgent { ActionSetKey = 9 };
        rig.Agents.Add(untracked);

        rig.Tick();
        untracked.Swing(BaseIdx);
        rig.Tick();

        Assert.AreEqual(0, untracked.ActionReads);
        rig.Service.End();
        Assert.AreEqual(0, SwingLines(rig));
    }

    [TestMethod]
    public void Tick_AgentAbsentFromATick_IsForgottenSoARespawnedHandleStartsClean()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();
        agent.Swing(BaseIdx);
        rig.Tick();

        rig.Agents.Clear();
        rig.Tick();
        Assert.AreEqual(0, rig.Service.TrackedAgentCount);

        rig.Agents.Add(agent);
        rig.Tick();
        rig.Tick();

        rig.Service.End();
        Assert.IsTrue(rig.Log.Info.Any(l => l == "[AnimVariety] swings set=as_orc_warrior act_release_overswing_1h=2 (100%)"));
    }

    [TestMethod]
    public void Tick_InactiveAgent_IsSkippedAndNotRead()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();
        agent.IsActive = false;
        agent.Swing(BaseIdx);
        int readsBefore = agent.ActionReads;

        rig.Tick();

        Assert.AreEqual(readsBefore, agent.ActionReads);
        Assert.AreEqual(0, rig.Service.TrackedAgentCount);
    }

    [TestMethod]
    public void Tick_NullAgentInTheSnapshot_IsSkipped()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(null!);
        rig.Agents.Add(agent);

        rig.Tick();

        Assert.AreEqual(1, rig.Service.TrackedAgentCount);
        Assert.AreEqual(1, GroupLines(rig));
    }

    [TestMethod]
    public void Tick_SwingsOfTwoSets_AreGroupedPerSetInNameOrder()
    {
        var rig = WithGroup();
        var a = new FakeAgent { ActionSetKey = 7 };
        var b = new FakeAgent { ActionSetKey = 9 };
        rig.Agents.Add(b);
        rig.Agents.Add(a);
        rig.Tick();
        a.Swing(BaseIdx);
        b.Swing(AltIdx);
        rig.Tick();

        rig.Service.End();

        var swings = rig.Log.Info.Where(l => l.StartsWith("[AnimVariety] swings ")).ToList();
        Assert.AreEqual(2, swings.Count);
        StringAssert.StartsWith(swings[0], "[AnimVariety] swings set=as_orc_warrior ");
        StringAssert.StartsWith(swings[1], "[AnimVariety] swings set=as_set9 ");
    }

    // ------------------------------------------------------------------ hits

    [TestMethod]
    public void RecordHit_TrackedAttacker_AccumulatesCountMeanMinMax()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();

        rig.Service.RecordHit(agent.Handle, Base, 0.33f, false);
        rig.Service.RecordHit(agent.Handle, Base, 0.52f, false);
        rig.Service.RecordHit(agent.Handle, Base, 0.38f, false);
        rig.Service.RecordHit(agent.Handle, Alt, 0.5f, false);
        rig.Service.End();

        Assert.IsTrue(rig.Log.Info.Any(l => l ==
            "[AnimVariety] hits act_release_overswing_1h n=3 progress mean=0.41 min=0.33 max=0.52"));
        Assert.IsTrue(rig.Log.Info.Any(l => l ==
            "[AnimVariety] hits act_taom_release_overswing_1h_b n=1 progress mean=0.50 min=0.50 max=0.50"));
    }

    [TestMethod]
    public void RecordHit_ProgressFallsAfterTheFirstSample_LowersTheMin()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();

        rig.Service.RecordHit(agent.Handle, Base, 0.5f, false);
        rig.Service.RecordHit(agent.Handle, Base, 0.3f, false);
        rig.Service.RecordHit(agent.Handle, Base, 0.7f, false);
        rig.Service.End();

        Assert.IsTrue(rig.Log.Info.Any(l => l ==
            "[AnimVariety] hits act_release_overswing_1h n=3 progress mean=0.50 min=0.30 max=0.70"));
    }

    [TestMethod]
    public void RecordHit_UntrackedAttacker_IsIgnored()
    {
        var rig = WithGroup();
        rig.Engine.AreAlternatives(9, Base, Alt).Returns(false);
        var untracked = new FakeAgent { ActionSetKey = 9 };
        rig.Agents.Add(untracked);
        rig.Tick();

        rig.Service.RecordHit(untracked.Handle, Base, 0.4f, false);
        rig.Service.RecordHit(new object(), Base, 0.4f, false);
        rig.Service.End();

        Assert.IsFalse(rig.Log.Info.Any(l => l.StartsWith("[AnimVariety] hits ")));
    }

    [TestMethod]
    public void RecordHit_NaNOrInfiniteProgress_IsIgnored()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();

        rig.Service.RecordHit(agent.Handle, Base, float.NaN, false);
        rig.Service.RecordHit(agent.Handle, Base, float.PositiveInfinity, false);
        rig.Service.End();

        Assert.IsFalse(rig.Log.Info.Any(l => l.StartsWith("[AnimVariety] hits ")));
    }

    [TestMethod]
    public void RecordHit_MissileBlow_IsIgnored()
    {
        // A projectile that lands while the shooter's channel 1 plays a later swing would be filed under
        // that swing's clip.
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();
        agent.Swing(BaseIdx);
        rig.Tick();

        rig.Service.RecordHit(agent.Handle, Base, 0.4f, true);
        rig.Service.End();

        Assert.IsFalse(rig.Log.Info.Any(l => l.StartsWith("[AnimVariety] hits ")));
    }

    [TestMethod]
    public void RecordHit_BeforeTheFirstTick_IsIgnored()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);

        rig.Service.RecordHit(agent.Handle, Base, 0.4f, false);
        rig.Tick();
        rig.Service.End();

        Assert.IsFalse(rig.Log.Info.Any(l => l.StartsWith("[AnimVariety] hits ")));
    }

    [TestMethod]
    public void RecordHit_AfterEnd_IsIgnored()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();
        rig.Service.End();

        rig.Service.RecordHit(agent.Handle, Base, 0.4f, false);

        Assert.IsFalse(rig.Service.IsTracking);
        Assert.IsFalse(rig.Log.Info.Any(l => l.StartsWith("[AnimVariety] hits ")));
    }

    [TestMethod]
    public void RecordHit_NullHandle_IsIgnored()
    {
        var rig = WithGroup();
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });
        rig.Tick();

        rig.Service.RecordHit(null!, Base, 0.4f, false);
        rig.Service.End();

        Assert.IsFalse(rig.Log.Info.Any(l => l.StartsWith("[AnimVariety] hits ")));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void RecordHit_NullOrEmptyActionName_IsIgnored(string? actionName)
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();

        rig.Service.RecordHit(agent.Handle, actionName!, 0.4f, false);
        rig.Service.End();

        Assert.IsFalse(rig.Log.Info.Any(l => l.StartsWith("[AnimVariety] hits ")));
    }

    // ------------------------------------------------------------------ summary and end

    [TestMethod]
    public void Tick_EveryThirtySecondsOfMissionTime_WritesTheSummary()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();
        agent.Swing(BaseIdx);
        rig.Tick();

        rig.Tick(29f);
        Assert.AreEqual(0, SwingLines(rig));

        rig.Tick(1.5f);
        Assert.AreEqual(1, SwingLines(rig));

        rig.Tick(29f);
        Assert.AreEqual(1, SwingLines(rig));
        rig.Tick(2f);
        Assert.AreEqual(2, SwingLines(rig));
    }

    [TestMethod]
    public void Tick_NaNInfiniteOrNegativeFrameTime_NeverAdvancesTheTimer()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();
        agent.Swing(BaseIdx);
        rig.Tick();

        for (int i = 0; i < 5; i++) rig.Tick(float.NaN);
        rig.Tick(float.PositiveInfinity);
        rig.Tick(-5f);

        Assert.AreEqual(0, SwingLines(rig));
    }

    [TestMethod]
    public void End_NoSwingOrHitYet_WritesNoSummaryLine()
    {
        var rig = WithGroup();
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });
        rig.Tick();

        rig.Tick(31f);
        rig.Service.End();

        Assert.IsFalse(rig.Log.Info.Any(l => l.StartsWith("[AnimVariety] swings ") || l.StartsWith("[AnimVariety] hits ")));
    }

    [TestMethod]
    public void End_CalledTwice_WritesOnceAndStopsFurtherTicks()
    {
        var rig = WithGroup();
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);
        rig.Tick();
        agent.Swing(BaseIdx);
        rig.Tick();

        rig.Service.End();
        rig.Service.End();
        agent.Idle();
        rig.Tick();
        agent.Swing(AltIdx);
        rig.Tick();
        rig.Tick(40f);

        Assert.AreEqual(1, SwingLines(rig));
        Assert.IsFalse(rig.Service.IsTracking);
    }

    [TestMethod]
    public void End_BeforeAnyTick_WritesNothing()
    {
        var rig = WithGroup();

        rig.Service.End();

        Assert.AreEqual(0, rig.Log.All.Count());
    }

    [TestMethod]
    public void End_NoAlternativeGroupsFound_WritesNothingMoreAndStaysOff()
    {
        var rig = new Rig(Base);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });
        rig.Tick();
        int linesBefore = rig.Log.All.Count();

        rig.Service.End();
        rig.Service.End();
        rig.Tick();

        Assert.AreEqual(linesBefore, rig.Log.All.Count());
        Assert.IsFalse(rig.Service.IsTracking);
    }

    // ------------------------------------------------------------------ the off switch

    [TestMethod]
    public void Tick_ProbeSwitchedOff_NeverListsActionCodesReadsAgentsOrLogs()
    {
        var rig = WithGroup();
        rig.Settings.SwingAlternativeProbe.Returns(false);
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        for (int i = 0; i < 5; i++) rig.Tick();
        rig.Tick(60f);
        rig.Service.End();

        rig.Engine.DidNotReceiveWithAnyArgs().ActionCodeExists(default!);
        Assert.AreEqual(0, rig.SourceCalls);
        Assert.AreEqual(0, rig.Log.All.Count(), "not even the 'tracking off' line");
        Assert.IsFalse(rig.Service.IsTracking);
        Assert.AreEqual(0, rig.Service.TrackedAgentCount);
    }

    [TestMethod]
    public void Tick_ProbeSwitchedOff_ReadsTheSwitchOnceAndIgnoresHits()
    {
        var rig = WithGroup();
        rig.Settings.SwingAlternativeProbe.Returns(false);
        var agent = new FakeAgent { ActionSetKey = 7 };
        rig.Agents.Add(agent);

        for (int i = 0; i < 5; i++) rig.Tick();
        rig.Service.RecordHit(agent.Handle, Base, 0.4f, false);
        rig.Service.End();

        _ = rig.Settings.Received(1).SwingAlternativeProbe;
        Assert.AreEqual(0, rig.Log.All.Count());
    }

    [TestMethod]
    public void Tick_ProbeSwitchedOn_ReadsTheSwitchOncePerMission()
    {
        var rig = WithGroup();
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });

        for (int i = 0; i < 5; i++) rig.Tick();

        _ = rig.Settings.Received(1).SwingAlternativeProbe;
        Assert.IsTrue(rig.Service.IsTracking);
    }

    [TestMethod]
    public void Tick_SwitchTurnedOffAfterTheFirstTick_DoesNotStopAMissionAlreadyTracking()
    {
        // Read once per mission: a mid-mission toggle waits for the next mission.
        var rig = WithGroup();
        rig.Agents.Add(new FakeAgent { ActionSetKey = 7 });
        rig.Tick();

        rig.Settings.SwingAlternativeProbe.Returns(false);
        rig.Tick();

        Assert.IsTrue(rig.Service.IsTracking);
    }
}
