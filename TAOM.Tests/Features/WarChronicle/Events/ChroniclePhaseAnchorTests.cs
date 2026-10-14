using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Events;
using TAOM.Features.WarChronicle.Rally;
using static TAOM.Tests.Features.WarChronicle.Events.ChronicleFixture;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>
/// Phase anchors (decided by the maintainer 2026-10-14): a Phase1 or Phase2 event counts from the later of
/// the configured effective phase day and the day the chronicle first saw the phase reached, less the one
/// step the chronicle runs behind the phase machine. The first-seen day is saved. The fixture's effective
/// days are 30 (Phase 1) and 44 (Phase 2). Each test steps the days in the engine's order: the chronicle's
/// daily listener runs before the phase machine's, so a phase entered on day N's tick is first seen on N+1.
/// </summary>
[TestClass]
public class ChroniclePhaseAnchorTests
{
    private ChronicleFixture _f = null!;
    private ChronicleService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new ChronicleFixture { Phase = WarPhase.Peace };
        _sut = _f.CreateService();
    }

    private static WarChronicleStateService StateFor(ChronicleFixture f) =>
        new WarChronicleStateService(f.Effects, new WarBaselineService(), new RallyTierStore(), f.Logger, f.Store);

    private void Day(double day)
    {
        _f.World.Day = day;
        _sut.RunChronicleDay();
    }

    /// <summary>The chronicle steps day N, then the phase machine enters the phase on the same tick.</summary>
    private void DayThenPhase(double day, WarPhase phase)
    {
        Day(day);
        _f.Phase = phase;
    }

    private void AddPhase1Events()
    {
        _f.Events.Add(Narrative("breaking", ChronicleAnchor.Phase1, 0));
        _f.Events.Add(Narrative("theoden", ChronicleAnchor.Phase1, 7));
    }

    [TestMethod]
    public void RunChronicleDay_WarSwitchedOnAtDay100_OpensPhase1EventsFromDay100PlusTheirOffsets()
    {
        AddPhase1Events();
        _f.World.Day = 0;
        _sut.LaunchChronicle();

        DayThenPhase(100, WarPhase.IsengardWar);
        Day(101);

        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"), "Phase 1 + 0 opens on the first step that sees the phase");
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("theoden"), "Phase 1 + 7 waits for its own day, not the configured day 37");

        Day(106);
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("theoden"));
        Day(107);
        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("theoden"));
    }

    [TestMethod]
    public void RunChronicleDay_TestModeSwitchedOnAtDay60_OpensPhase2SiegesFromDay60PlusTheirOffsets()
    {
        _f.Events.Add(SiegeDef(offset: 5));
        _f.Events.Add(SiegeDef("osgiliath", "town_EW3", "empire_w", new[] { "empire_s" }, offset: 10));
        _f.World.Day = 0;
        _sut.LaunchChronicle();

        // Both phases enter on one call when the phase days drop below today.
        DayThenPhase(60, WarPhase.FullWar);
        Day(61);
        Day(64);

        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("hornburg"), "not on one step with every overdue event");
        Day(65);
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"));
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("osgiliath"));
        Day(70);
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("osgiliath"));
    }

    [TestMethod]
    public void RunChronicleDay_ANormalCampaign_OpensOnTheConfiguredDaysAsBefore()
    {
        AddPhase1Events();
        _f.Events.Add(SiegeDef(offset: 5));
        _f.World.Day = 0;
        _sut.LaunchChronicle();

        DayThenPhase(30, WarPhase.IsengardWar);
        Day(31);
        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("breaking"), "one step behind the phase change, as before");
        Day(36);
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("theoden"));
        Day(37);
        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("theoden"), "the configured day 30 + 7");

        DayThenPhase(44, WarPhase.FullWar);
        Day(45);
        Day(48);
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("hornburg"));
        Day(49);
        Assert.AreEqual(ChronicleEventState.Active, _f.StateOf("hornburg"), "the configured day 44 + 5");
    }

    [TestMethod]
    public void RunChronicleDay_APhaseDayRaisedAfterThePhaseWasSeen_PostponesItsPendingEvents()
    {
        AddPhase1Events();
        _f.World.Day = 0;
        _sut.LaunchChronicle();
        DayThenPhase(30, WarPhase.IsengardWar);
        Day(31);

        _f.Wotr.GetEffectivePhaseDay(WarPhase.IsengardWar).Returns(50);
        Day(40);

        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("theoden"), "the configured day wins when it is later");
        Day(57);
        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("theoden"));
    }

    [TestMethod]
    public void RunChronicleDay_RecordsOnlyTheFirstDayAPhaseWasSeen()
    {
        _f.World.Day = 0;
        _sut.LaunchChronicle();
        DayThenPhase(100, WarPhase.IsengardWar);
        Day(101);
        Day(150);

        Assert.AreEqual(101d, _f.Store.GetPhaseSeenDay(WarPhase.IsengardWar));
        Assert.IsNull(_f.Store.GetPhaseSeenDay(WarPhase.FullWar), "Full War not reached yet");
    }

    [TestMethod]
    public void RunChronicleDay_ChronicleOff_DoesNotRecordASeenDay()
    {
        _f.Phase = WarPhase.FullWar;
        _f.Settings.ChronicleEnabled.Returns(false);

        Day(60);

        Assert.IsNull(_f.Store.GetPhaseSeenDay(WarPhase.IsengardWar));
        Assert.IsNull(_f.Store.GetPhaseSeenDay(WarPhase.FullWar));
    }

    [TestMethod]
    public void SaveAndLoad_KeepsTheSeenDay_SoALoadDoesNotPostponeThePendingEvents()
    {
        _f.Events.Add(Narrative("late", ChronicleAnchor.Phase1, 60));
        _f.World.Day = 0;
        _sut.LaunchChronicle();
        DayThenPhase(100, WarPhase.IsengardWar);
        Day(101);
        Day(120);
        var chunks = StateFor(_f).CaptureChunks();

        var after = new ChronicleFixture { Phase = WarPhase.IsengardWar };
        after.Events.Add(Narrative("late", ChronicleAnchor.Phase1, 60));
        StateFor(after).RestoreFromChunks(chunks);
        var reloaded = after.CreateService();
        after.World.Day = 150;
        reloaded.LaunchChronicle();

        Assert.AreEqual(101d, after.Store.GetPhaseSeenDay(WarPhase.IsengardWar), "the saved day, not the load day");
        after.World.Day = 159;
        reloaded.RunChronicleDay();
        Assert.AreEqual(ChronicleEventState.Pending, after.StateOf("late"));
        after.World.Day = 160;
        reloaded.RunChronicleDay();
        Assert.AreEqual(ChronicleEventState.Occurred, after.StateOf("late"), "day 100 + 60");
    }

    [TestMethod]
    public void LaunchChronicle_ASaveWithoutSeenDays_RecordsTheLoadDayOnFirstSight()
    {
        _f.Phase = WarPhase.IsengardWar;
        _f.Events.Add(Narrative("theoden", ChronicleAnchor.Phase1, 7));
        StateFor(_f).RestoreFromChunks(new List<string> { "{\"v\":1,\"chronicle\":[{\"id\":\"theoden\",\"s\":0}]}" });

        _f.World.Day = 150;
        _sut.LaunchChronicle();

        Assert.AreEqual(150d, _f.Store.GetPhaseSeenDay(WarPhase.IsengardWar));
        Day(155);
        Assert.AreEqual(ChronicleEventState.Pending, _f.StateOf("theoden"), "the overdue event counts from the load, not from day 30");
        Day(156);
        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("theoden"));
    }

    [TestMethod]
    public void LaunchChronicle_ASaveFromBeforeTheChronicle_StillSkipsPhaseEventsPastTheirConfiguredDay()
    {
        // Decision D5 is unchanged: a missing row is judged by the configured day, not by the first sighting.
        _f.Phase = WarPhase.FullWar;
        AddPhase1Events();
        _f.Events.Add(SiegeDef(offset: 5));

        _f.World.Day = 100;
        _sut.LaunchChronicle();

        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("breaking"));
        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("theoden"));
        Assert.AreEqual(ChronicleEventState.Skipped, _f.StateOf("hornburg"));
    }

    [TestMethod]
    public void ANewCampaign_ResetsTheSeenDays()
    {
        AddPhase1Events();
        var state = StateFor(_f);
        _f.World.Day = 0;
        _sut.LaunchChronicle();
        DayThenPhase(100, WarPhase.IsengardWar);
        Day(101);

        // Campaign B in the same process: the behavior's constructor resets, no save is loaded.
        state.ResetForNewSession();
        _f.Phase = WarPhase.Peace;
        _f.World.Day = 0;
        _sut.LaunchChronicle();

        Assert.IsNull(_f.Store.GetPhaseSeenDay(WarPhase.IsengardWar));
        DayThenPhase(30, WarPhase.IsengardWar);
        Day(31);
        Day(37);
        Assert.AreEqual(ChronicleEventState.Occurred, _f.StateOf("theoden"), "campaign A's day 101 does not carry over");
    }
}
