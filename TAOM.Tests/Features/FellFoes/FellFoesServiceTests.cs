using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FellFoes;
using TAOM.Features.FellFoes.Domain;

namespace TAOM.Tests.Features.FellFoes;

/// <summary>The Fell Foes service: the once-a-second throttle, the gate, the toggles, and the writes through the adapter.</summary>
[TestClass]
public class FellFoesServiceTests
{
    private IFellFoesAdapter _adapter = null!;
    private IFellFoesSettingsProvider _settings = null!;
    private IModLogger _logger = null!;
    private FellFoesTunables _tunables = null!;
    private FellFoesScan _scan = null!;
    private FellFoesScanRequest? _lastRequest;
    private float _roll;
    private bool _open;
    private int _gateCalls;
    private FellFoesService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IFellFoesAdapter>();
        _settings = Substitute.For<IFellFoesSettingsProvider>();
        _logger = Substitute.For<IModLogger>();
        _tunables = FellFoesTunables.Create();
        _settings.CorneredRoutersEnabled.Returns(true);
        _settings.ArchersTargetPlayerEnabled.Returns(true);
        _settings.StragglersLeaveFieldEnabled.Returns(true);
        _settings.DebugEnabled.Returns(false);
        _settings.Tunables.Returns(_ => _tunables);
        _scan = new FellFoesScan { PlayerAlive = true, IsFieldBattle = true, EnemyReserves = 0 };
        _adapter.Scan(Arg.Do<FellFoesScanRequest>(r => _lastRequest = r)).Returns(_ => _scan);
        _adapter.StopRout(Arg.Any<object>()).Returns(true);
        _adapter.Unsteady(Arg.Any<object>()).Returns(true);
        _adapter.SendOff(Arg.Any<object>()).Returns(true);
        _adapter.HoldTarget(Arg.Any<object>()).Returns(true);
        _roll = 0f;
        _open = true;
        _gateCalls = 0;
        _sut = NewService();
    }

    private FellFoesService NewService() =>
        new FellFoesService(_adapter, _settings, _logger, () => _roll, () => { _gateCalls++; return _open; });

    private static EnemyAgentView Router() => new EnemyAgentView
    {
        Handle = new object(), IsPanicked = true, IsFleeing = true, HealthFraction = 1f, NearbyFoes = 2, Morale = 0.5f,
    };

    private static EnemyAgentView Archer() => new EnemyAgentView
    {
        Handle = new object(), IsIdleArcher = true, Morale = 80f, DistanceToPlayer = 10f, HealthFraction = 1f,
    };

    private static EnemyAgentView Soldier() => new EnemyAgentView { Handle = new object(), HealthFraction = 1f, Morale = 80f };

    private void Seconds(int n)
    {
        for (var i = 0; i < n; i++)
            _sut.Tick(1f);
    }

    /// <summary>The router is seen once, then hit: the second pass is the cornering.</summary>
    private void HitTheRouter(EnemyAgentView router)
    {
        _scan.Enemies = new[] { router, Soldier() };
        Seconds(1);
        router.HealthFraction = 0.8f;
        Seconds(1);
    }

    // ------------------------------------------------------------ throttle, gate and toggles

    [TestMethod]
    public void Tick_ScansOncePerSecond_NotEveryFrame()
    {
        for (var i = 0; i < 9; i++)
            _sut.Tick(0.1f);
        _adapter.DidNotReceive().Scan(Arg.Any<FellFoesScanRequest>());

        _sut.Tick(0.1f);
        _sut.Tick(0.1f);
        _adapter.Received(1).Scan(Arg.Any<FellFoesScanRequest>());
    }

    [TestMethod]
    public void Tick_NonFiniteOrNegativeDelta_IsIgnored_AndNeverReachesTheGate()
    {
        _sut.Tick(float.NaN);
        _sut.Tick(float.PositiveInfinity);
        _sut.Tick(-4f);

        _adapter.DidNotReceive().Scan(Arg.Any<FellFoesScanRequest>());
        Assert.AreEqual(0, _gateCalls);
    }

    [TestMethod]
    public void Tick_TheGateIsReadOncePerSecond_NotEveryFrame()
    {
        for (var i = 0; i < 25; i++)
            _sut.Tick(0.1f);

        Assert.AreEqual(2, _gateCalls);
    }

    [TestMethod]
    public void Tick_AClosedGate_ScansNothing_AndWritesNothing()
    {
        _open = false;
        _scan.Enemies = new[] { Router(), Archer() };

        Seconds(3);

        _adapter.DidNotReceive().Scan(Arg.Any<FellFoesScanRequest>());
        _adapter.DidNotReceive().StopRout(Arg.Any<object>());
        _adapter.DidNotReceive().HoldTarget(Arg.Any<object>());
    }

    [TestMethod]
    public void Tick_TheGateClosing_ReleasesEveryHold()
    {
        var a = Archer();
        var b = Archer();
        _scan.Enemies = new[] { a, b };
        Seconds(1);

        _open = false;
        Seconds(1);

        _adapter.Received(1).ReleaseTarget(a.Handle);
        _adapter.Received(1).ReleaseTarget(b.Handle);
    }

    [TestMethod]
    public void Tick_EveryTweakOff_NeverScans()
    {
        _settings.CorneredRoutersEnabled.Returns(false);
        _settings.ArchersTargetPlayerEnabled.Returns(false);
        _settings.StragglersLeaveFieldEnabled.Returns(false);

        Seconds(3);

        _adapter.DidNotReceive().Scan(Arg.Any<FellFoesScanRequest>());
    }

    [TestMethod]
    public void Tick_ARouterHit_WithFoesBeside_IsStopped()
    {
        var r = Router();

        HitTheRouter(r);

        _adapter.Received(1).StopRout(r.Handle);
    }

    [TestMethod]
    public void Tick_CorneredToggleOff_StopsNobody_AndAsksForNoFoeCount()
    {
        _settings.CorneredRoutersEnabled.Returns(false);

        HitTheRouter(Router());

        _adapter.DidNotReceive().StopRout(Arg.Any<object>());
        Assert.AreEqual(0f, _lastRequest!.CorneredRadius);
    }

    [TestMethod]
    public void Tick_AFailedStop_IsRolledBack_SoTheManIsNotUnsteadiedLater()
    {
        var r = Router();
        _adapter.StopRout(r.Handle).Returns(false);
        HitTheRouter(r);
        // The stop failed and he is no longer panicked: without the rollback he would sit in the turned set with
        // no foe near and be unsteadied, a morale write on a man the service never turned.
        r.IsPanicked = false;
        r.IsFleeing = false;
        r.HoldFoes = 0;

        Seconds(1);

        _adapter.DidNotReceive().Unsteady(Arg.Any<object>());
    }

    [TestMethod]
    public void Tick_AStoppedAgentWithNoFoesLeft_IsUnsteadied()
    {
        var r = Router();
        HitTheRouter(r);
        r.IsPanicked = false;
        r.IsFleeing = false;
        r.HoldFoes = 0;

        Seconds(1);

        _adapter.Received(1).Unsteady(r.Handle);
    }

    [TestMethod]
    public void Tick_TheCorneredSet_IsPassedToTheAdapterAsTheStateOwnsIt_NotACopy()
    {
        var stateField = typeof(FellFoesService).GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var same = new List<bool>();
        _adapter.Scan(Arg.Do<FellFoesScanRequest>(r =>
            same.Add(ReferenceEquals(r.Cornered, ((FellFoesMissionState)stateField.GetValue(_sut)!).Cornered)))).Returns(_ => _scan);
        HitTheRouter(Router());

        Seconds(1);

        Assert.IsTrue(same.Count >= 3 && same.All(x => x), "every request carries the state's own cornered set instance");
    }

    [TestMethod]
    public void Tick_TheHoldRadius_IsAskedFor_OnlyWhileCorneredIsOn()
    {
        Seconds(1);
        Assert.AreEqual(_tunables.HoldRadius, _lastRequest!.HoldRadius);

        _settings.CorneredRoutersEnabled.Returns(false);
        Seconds(1);
        Assert.AreEqual(0f, _lastRequest!.HoldRadius);
    }

    [TestMethod]
    public void Tick_SwitchingCorneredOffAndOn_ForgetsTheOldHealth_SoTheNextPassIsAFirstSighting()
    {
        var r = Router();
        _scan.Enemies = new[] { r, Soldier() };
        Seconds(1);
        _settings.CorneredRoutersEnabled.Returns(false);
        Seconds(1);
        r.HealthFraction = 0.5f;
        _settings.CorneredRoutersEnabled.Returns(true);

        Seconds(1);

        _adapter.DidNotReceive().StopRout(Arg.Any<object>());
    }

    [TestMethod]
    public void Tick_DebugOn_CountsHandlesStoppedMoreThanOnce()
    {
        _settings.DebugEnabled.Returns(true);
        var r = Router();
        HitTheRouter(r);
        r.HealthFraction = 0.6f;

        Seconds(1);

        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("restopped=1")));
    }

    [TestMethod]
    public void Tick_DebugOn_AManStoppedOnce_IsNotCountedAsRestopped()
    {
        _settings.DebugEnabled.Returns(true);
        HitTheRouter(Router());

        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("stopped=1") && m.Contains("restopped=0")));
    }

    [TestMethod]
    public void Tick_TheArcherReadRange_IsTheFieldRange_OnlyWhileArchersAreOn()
    {
        Seconds(1);
        Assert.AreEqual(_tunables.ArcherFieldRange, _lastRequest!.ArcherReadRange);

        _settings.ArchersTargetPlayerEnabled.Returns(false);
        Seconds(1);
        Assert.AreEqual(0f, _lastRequest!.ArcherReadRange);
    }

    // ------------------------------------------------------------ archers

    [TestMethod]
    public void Tick_AnIdleArcher_IsHeld()
    {
        var a = Archer();
        _scan.Enemies = new[] { a };

        Seconds(1);

        _adapter.Received(1).HoldTarget(a.Handle);
    }

    [TestMethod]
    public void Tick_AFailedHold_LeavesNoHoldBehind()
    {
        var a = Archer();
        _scan.Enemies = new[] { a };
        _adapter.HoldTarget(a.Handle).Returns(false);

        Seconds(1);
        _sut.ReleaseHolds();

        _adapter.DidNotReceive().ReleaseTarget(Arg.Any<object>());
    }

    [TestMethod]
    public void Tick_AHoldExpires_AndIsReleased()
    {
        var a = Archer();
        _scan.Enemies = new[] { a };
        Seconds(1);
        _roll = 0.99f;

        Seconds((int)_tunables.ArcherHoldSeconds + 1);

        _adapter.Received(1).ReleaseTarget(a.Handle);
    }

    [TestMethod]
    public void Tick_TwoExpiredHolds_WhereTheFirstReleaseThrows_StillReleaseTheSecond()
    {
        var a = Archer();
        var b = Archer();
        _scan.Enemies = new[] { a, b };
        Seconds(1);
        _roll = 0.99f;
        _adapter.When(x => x.ReleaseTarget(a.Handle)).Do(_ => throw new InvalidOperationException("gone"));

        Seconds((int)_tunables.ArcherHoldSeconds + 1);

        _adapter.Received(1).ReleaseTarget(b.Handle);
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("release") && m.Contains("gone")));
    }

    [TestMethod]
    public void Tick_SwitchingArchersOffMidBattle_ReleasesEveryHold()
    {
        var a = Archer();
        var b = Archer();
        _scan.Enemies = new[] { a, b };
        Seconds(1);
        _settings.ArchersTargetPlayerEnabled.Returns(false);

        Seconds(1);

        _adapter.Received(1).ReleaseTarget(a.Handle);
        _adapter.Received(1).ReleaseTarget(b.Handle);
    }

    [TestMethod]
    public void ReleaseHolds_ReleasesEveryHoldOnce_AndSurvivesAThrowingRelease()
    {
        var a = Archer();
        var b = Archer();
        _scan.Enemies = new[] { a, b };
        Seconds(1);
        _adapter.When(x => x.ReleaseTarget(a.Handle)).Do(_ => throw new InvalidOperationException("gone"));

        _sut.ReleaseHolds();
        _sut.ReleaseHolds();

        _adapter.Received(1).ReleaseTarget(a.Handle);
        _adapter.Received(1).ReleaseTarget(b.Handle);
    }

    [TestMethod]
    public void ReleaseHolds_WithNoHolds_TouchesNothing()
    {
        _sut.ReleaseHolds();

        _adapter.DidNotReceive().ReleaseTarget(Arg.Any<object>());
    }

    [TestMethod]
    public void ReleaseHolds_RunsEvenWhenEveryToggleIsOff()
    {
        var a = Archer();
        _scan.Enemies = new[] { a };
        Seconds(1);
        _settings.ArchersTargetPlayerEnabled.Returns(false);
        _settings.CorneredRoutersEnabled.Returns(false);
        _settings.StragglersLeaveFieldEnabled.Returns(false);

        _sut.ReleaseHolds();

        _adapter.Received(1).ReleaseTarget(a.Handle);
    }

    // ------------------------------------------------------------ stragglers

    private static List<EnemyAgentView> Army(int n) =>
        Enumerable.Range(0, n).Select(_ => new EnemyAgentView { Handle = new object(), HealthFraction = 1f }).ToList();

    [TestMethod]
    public void Tick_StragglersAfterTheHold_OneIsSentPerSecond()
    {
        _scan.Enemies = Army(100);
        Seconds(1);
        _scan.Enemies = Army(3);

        Seconds((int)_tunables.StragglerHoldSeconds + 1);
        _adapter.ClearReceivedCalls();
        Seconds(1);
        Seconds(1);

        _adapter.Received(2).SendOff(Arg.Any<object>());
    }

    [TestMethod]
    public void Tick_AFailedSendOff_IsTriedAgainOnTheNextPass()
    {
        _scan.Enemies = Army(100);
        Seconds(1);
        var last = Army(1);
        _scan.Enemies = last;
        _adapter.SendOff(last[0].Handle).Returns(false, true);

        Seconds((int)_tunables.StragglerHoldSeconds + 3);

        _adapter.Received(2).SendOff(last[0].Handle);
    }

    [TestMethod]
    public void Tick_StragglersToggleOff_SendsNobody()
    {
        _settings.StragglersLeaveFieldEnabled.Returns(false);
        _scan.Enemies = Army(100);
        Seconds(1);
        _scan.Enemies = Army(3);

        Seconds(60);

        _adapter.DidNotReceive().SendOff(Arg.Any<object>());
    }

    // ------------------------------------------------------------ independence and failure

    [TestMethod]
    public void Tick_OnePassThrowing_LeavesTheOthersRunning_AndIsNotRetried()
    {
        var r = Router();
        var a = Archer();
        _scan.Enemies = new[] { r, a, Soldier() };
        _adapter.StopRout(Arg.Any<object>()).Throws(new InvalidOperationException("boom"));
        Seconds(1);
        r.HealthFraction = 0.8f;

        Seconds(3);

        _adapter.Received(1).StopRout(r.Handle);
        _adapter.Received(1).HoldTarget(a.Handle);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("cornered") && m.Contains("boom")));
    }

    [TestMethod]
    public void Tick_TheStragglerPassThrowing_IsIsolated_LoggedOnce_AndNotRetried()
    {
        _scan.Enemies = Army(100);
        Seconds(1);
        var last = Army(1);
        _scan.Enemies = last;
        _adapter.SendOff(Arg.Any<object>()).Throws(new InvalidOperationException("boom"));

        Seconds((int)_tunables.StragglerHoldSeconds + 5);

        _adapter.Received(1).SendOff(last[0].Handle);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("straggler") && m.Contains("boom")));
    }

    [TestMethod]
    public void Tick_AThrowingArcherPass_ReleasesWhatItHeld_OnTheNextPass()
    {
        var a = Archer();
        var b = Archer();
        _scan.Enemies = new[] { a };
        Seconds(1);
        _scan.Enemies = new[] { a, b };
        _adapter.HoldTarget(b.Handle).Throws(new InvalidOperationException("boom"));

        Seconds(1);
        _adapter.DidNotReceive().ReleaseTarget(a.Handle);

        Seconds(1);
        _adapter.Received(1).ReleaseTarget(a.Handle);
        _adapter.Received(1).ReleaseTarget(b.Handle);
    }

    [TestMethod]
    public void Tick_AThrowingScan_IsLoggedOnce_NeverEscapes_AndReleasesTheHolds()
    {
        var a = Archer();
        _scan.Enemies = new[] { a };
        Seconds(1);
        _adapter.Scan(Arg.Any<FellFoesScanRequest>()).Throws(new InvalidOperationException("native"));

        Seconds(3);

        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("scan")));
        _adapter.Received(1).ReleaseTarget(a.Handle);
        _adapter.DidNotReceive().StopRout(Arg.Any<object>());
    }

    [TestMethod]
    public void Tick_ARejectedTunable_IsLoggedOnceAsAWarning()
    {
        _tunables = FellFoesTunables.Create(archerChance: 5f);
        _scan.Enemies = new EnemyAgentView[0];

        Seconds(3);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("ArcherChance")));
    }

    // ------------------------------------------------------------ debug line

    [TestMethod]
    public void Tick_DebugOff_LogsNoInfoLines_AndAsksForNoTargetReads()
    {
        _scan.Enemies = new[] { Archer() };

        Seconds(2);

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
        Assert.AreEqual(0, _lastRequest!.Held.Count);
    }

    [TestMethod]
    public void Tick_DebugOnWithNothingDone_WritesNoLine()
    {
        _settings.DebugEnabled.Returns(true);
        _scan.Enemies = new[] { Soldier() };

        Seconds(3);

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void Tick_DebugOn_LogsWhatItDid_AndHowManyHeldArchersAreReallyAimingAtYou()
    {
        _settings.DebugEnabled.Returns(true);
        var a = Archer();
        _scan.Enemies = new[] { a };
        Seconds(1);
        a.TargetsPlayer = true;

        Seconds(1);

        CollectionAssert.Contains(_lastRequest!.Held.ToList(), a.Handle);
        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("[FellFoes]") && m.Contains("held 1, aiming 1 of 1 checked")));
    }

    [TestMethod]
    public void Tick_DebugOn_ASecondArcherHeldInThePass_StillReadsOneOfOne()
    {
        _settings.DebugEnabled.Returns(true);
        var a = Archer();
        _scan.Enemies = new[] { a };
        Seconds(1);
        a.TargetsPlayer = true;
        var b = Archer();
        _scan.Enemies = new[] { a, b };

        Seconds(1);

        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("held 2, aiming 1 of 1 checked")));
    }
}
