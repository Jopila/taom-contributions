using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Activities;
using TAOM.Features.FieldCamp.Domain;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// The decisions of camp work (#860): every block of a start, the progress a tick adds, the one midpoint injury, the payout and
/// what a stop or a fault leaves. The engine is a substituted adapter; the camp is a plain <see cref="CampState"/>.
/// </summary>
[TestClass]
public class CampActivityServiceTests
{
    private ICampService _camps = null!;
    private ICampSettingsProvider _settings = null!;
    private ICampWorkAdapter _work = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private IModLogger _logger = null!;
    private CampState _camp = null!;
    private CampWorkFacts _facts;
    private CampActivityService _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _camp = new CampState { TypeEnum = CampType.Field };
        _camps = Substitute.For<ICampService>();
        _camps.PlayerCamp.Returns(_ => _camp);
        _settings = Substitute.For<ICampSettingsProvider>();
        _settings.Enabled.Returns(true);
        _settings.CampActivityHours.Returns(10f);
        _settings.CampActivityYieldPerTenMen.Returns(10f);
        _settings.CampActivityYieldCap.Returns(30);
        _facts = new CampWorkFacts
        {
            CampReady = true, HealthyMen = 20, HealthyRegulars = 19, ClanTier = 2, ToolsHeld = 5, TerrainType = (int)TerrainType.Forest,
        };
        _work = Substitute.For<ICampWorkAdapter>();
        _work.Read().Returns(_ => _facts);
        _work.WaterNearby().Returns(true);
        _work.AddGoods(Arg.Any<CampActivity>(), Arg.Any<int>()).Returns(c => c.ArgAt<int>(1));
        _work.WoundRegulars(Arg.Any<int>()).Returns(c => c.ArgAt<int>(0));
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _logger = Substitute.For<IModLogger>();
        _sut = new CampActivityService(_camps, _settings, _work, _coop, _dedicated, _logger);
    }

    private void Working(CampActivity activity, float done = 0f, float target = 10f, bool rolled = false)
    {
        _camp.Activity = (int)activity;
        _camp.ActivityHoursDone = done;
        _camp.ActivityTargetHours = target;
        _camp.InjuryRolled = rolled;
    }

    // ---- Blocks of a start

    [TestMethod]
    public void AReadyCampWithMenAndForest_OffersEveryWorkItCanDo()
    {
        Assert.IsTrue(_sut.GetOffer(CampActivity.Hunt).Ready);
        Assert.IsTrue(_sut.GetOffer(CampActivity.Timber).Ready);
        Assert.IsTrue(_sut.GetOffer(CampActivity.Fish).Ready);
    }

    [TestMethod]
    public void NoCamp_BlocksAsNotReady()
    {
        _camp = null!;

        Assert.AreEqual(CampWorkBlock.CampNotReady, _sut.GetOffer(CampActivity.Hunt).Block);
    }

    [TestMethod]
    public void ACampStillBeingRaised_BlocksAsNotReady()
    {
        _facts.CampReady = false;

        Assert.AreEqual(CampWorkBlock.CampNotReady, _sut.GetOffer(CampActivity.Hunt).Block);
    }

    [DataTestMethod]
    [DataRow(CampType.Ambush)]
    [DataRow(CampType.Lookout)]
    public void AnAmbushOrLookoutCamp_BlocksAsNotReady(CampType type)
    {
        _camp.TypeEnum = type;

        Assert.AreEqual(CampWorkBlock.CampNotReady, _sut.GetOffer(CampActivity.Hunt).Block);
    }

    [TestMethod]
    public void AFortifiedCamp_MayWork()
    {
        _camp.TypeEnum = CampType.Fortified;

        Assert.IsTrue(_sut.GetOffer(CampActivity.Hunt).Ready);
    }

    [TestMethod]
    public void AnEncounter_BlocksAsBusy()
    {
        _facts.Busy = true;

        Assert.AreEqual(CampWorkBlock.Busy, _sut.GetOffer(CampActivity.Hunt).Block);
    }

    [TestMethod]
    public void AFarWatchRide_BlocksTheWork()
    {
        _facts.ScoutRideOut = true;

        Assert.AreEqual(CampWorkBlock.ScoutRideOut, _sut.GetOffer(CampActivity.Hunt).Block);
    }

    [TestMethod]
    public void ARunningWork_BlocksAStartAsAlreadyWorking()
    {
        Working(CampActivity.Hunt);

        Assert.AreEqual(CampWorkBlock.AlreadyWorking, _sut.GetOffer(CampActivity.Timber).Block);
    }

    [TestMethod]
    public void TheCoopGate_HasAllFourTerms_AndEachBlocks()
    {
        _coop.IsSessionActive.Returns(true);
        Assert.AreEqual(CampWorkBlock.SharedCampaign, _sut.GetOffer(CampActivity.Hunt).Block, "a live co-op session");

        _coop.IsSessionActive.Returns(false);
        _coop.ShouldDeferToHost.Returns(true);
        Assert.AreEqual(CampWorkBlock.SharedCampaign, _sut.GetOffer(CampActivity.Hunt).Block, "a co-op mod TAOM cannot probe");

        _coop.ShouldDeferToHost.Returns(false);
        _dedicated.IsDedicatedServer.Returns(true);
        Assert.AreEqual(CampWorkBlock.SharedCampaign, _sut.GetOffer(CampActivity.Hunt).Block, "a dedicated server");

        _dedicated.IsDedicatedServer.Returns(false);
        _coop.IsAuthority.Returns(false);
        Assert.AreEqual(CampWorkBlock.SharedCampaign, _sut.GetOffer(CampActivity.Hunt).Block, "a peer that may not write the world");

        _coop.IsAuthority.Returns(true);
        Assert.IsTrue(_sut.GetOffer(CampActivity.Hunt).Ready, "single-player again");
    }

    [TestMethod]
    public void TheGlobalBlocks_ComeInTheOrder_Off_Coop_Busy_NotReady_ScoutRide()
    {
        _facts.CampReady = false;
        _facts.Busy = true;
        _facts.ScoutRideOut = true;
        Assert.AreEqual(CampWorkBlock.Busy, _sut.GlobalBlock(_facts));

        _facts.Busy = false;
        Assert.AreEqual(CampWorkBlock.CampNotReady, _sut.GlobalBlock(_facts));

        _facts.CampReady = true;
        Assert.AreEqual(CampWorkBlock.ScoutRideOut, _sut.GlobalBlock(_facts));

        _coop.IsSessionActive.Returns(true);
        Assert.AreEqual(CampWorkBlock.SharedCampaign, _sut.GlobalBlock(_facts));

        _settings.Enabled.Returns(false);
        Assert.AreEqual(CampWorkBlock.FeatureDisabled, _sut.GlobalBlock(_facts), "the master toggle outranks everything");
    }

    [TestMethod]
    public void NoHealthyMen_BlocksAsNoMen()
    {
        _facts.HealthyMen = 0;

        Assert.AreEqual(CampWorkBlock.NoMen, _sut.GetOffer(CampActivity.Hunt).Block);
    }

    [TestMethod]
    public void Timber_NeedsForest()
    {
        _facts.TerrainType = (int)TerrainType.Plain;

        Assert.AreEqual(CampWorkBlock.NothingHere, _sut.GetOffer(CampActivity.Timber).Block);
        Assert.IsTrue(_sut.GetOffer(CampActivity.Hunt).Ready, "plain still has game");
    }

    [TestMethod]
    public void Hunt_OnWaterOrAnUnknownGround_HasNothingHere()
    {
        _facts.TerrainType = (int)TerrainType.Lake;
        Assert.AreEqual(CampWorkBlock.NothingHere, _sut.GetOffer(CampActivity.Hunt).Block);

        _facts.TerrainType = 999;
        Assert.AreEqual(CampWorkBlock.NothingHere, _sut.GetOffer(CampActivity.Hunt).Block);

        _facts.TerrainType = -1;
        Assert.AreEqual(CampWorkBlock.NothingHere, _sut.GetOffer(CampActivity.Hunt).Block);
    }

    [TestMethod]
    public void Fish_NeedsWaterNearby_AndAsksTheAdapterOnlyForFish()
    {
        _work.WaterNearby().Returns(false);
        Assert.AreEqual(CampWorkBlock.NothingHere, _sut.GetOffer(CampActivity.Fish).Block);

        _work.ClearReceivedCalls();
        _sut.GetOffer(CampActivity.Hunt);
        _sut.GetOffer(CampActivity.Timber);
        _work.DidNotReceive().WaterNearby();
    }

    [DataTestMethod]
    [DataRow(10, 5, true)]
    [DataRow(11, 1, false)]
    [DataRow(11, 2, true)]
    [DataRow(25, 3, true)]
    [DataRow(25, 2, false)]
    public void Tools_AreCheckedAtTheStart_AboveTenMen(int men, int held, bool ready)
    {
        _facts.HealthyMen = men;
        _facts.ToolsHeld = held;

        var offer = _sut.GetOffer(CampActivity.Hunt);

        Assert.AreEqual(ready, offer.Ready);
        if (!ready)
            Assert.AreEqual(CampWorkBlock.NoTools, offer.Block);
        Assert.AreEqual(CampActivityRules.ToolsNeeded(men), offer.ToolsNeeded);
    }

    // ---- A start

    [TestMethod]
    public void TryStart_SavesTheKind_TheTargetSnapshot_AndAClearProgress()
    {
        _settings.CampActivityHours.Returns(12f);

        var offer = _sut.TryStart(CampActivity.Timber);

        Assert.IsTrue(offer.Ready);
        Assert.AreEqual((int)CampActivity.Timber, _camp.Activity);
        Assert.AreEqual(12f, _camp.ActivityTargetHours);
        Assert.AreEqual(0f, _camp.ActivityHoursDone);
        Assert.IsFalse(_camp.InjuryRolled);
    }

    [TestMethod]
    public void TryStart_TheTargetIsASnapshot_ALaterSettingChangeDoesNotMoveIt()
    {
        _sut.TryStart(CampActivity.Hunt);
        _settings.CampActivityHours.Returns(40f);

        Assert.AreEqual(10f, _sut.Current().TotalHours);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(-3f)]
    [DataRow(0f)]
    [DataRow(float.PositiveInfinity)]
    public void TryStart_ABadHoursSetting_FallsBackToTheDefault(float hours)
    {
        _settings.CampActivityHours.Returns(hours);

        _sut.TryStart(CampActivity.Hunt);

        Assert.AreEqual(CampSettingsProvider.DefaultActivityHours, _camp.ActivityTargetHours);
    }

    [TestMethod]
    public void TryStart_Blocked_ChangesNothing()
    {
        _facts.Busy = true;

        var offer = _sut.TryStart(CampActivity.Hunt);

        Assert.AreEqual(CampWorkBlock.Busy, offer.Block);
        Assert.AreEqual(0, _camp.Activity);
    }

    [TestMethod]
    public void TryStart_NoneIsNotAWork()
    {
        Assert.IsFalse(_sut.TryStart(CampActivity.None).Ready);
        Assert.AreEqual(0, _camp.Activity);
    }

    // ---- Progress and ticks

    [TestMethod]
    public void Current_WithoutACampOrAWork_IsNotRunning()
    {
        Assert.IsFalse(_sut.Current().Running);
        _camp = null!;
        Assert.IsFalse(_sut.Current().Running);
    }

    [TestMethod]
    public void Current_ReadsTheSavedHours_SoALoadResumesWhereItStopped()
    {
        Working(CampActivity.Fish, done: 2.5f, target: 10f);

        var p = _sut.Current();

        Assert.IsTrue(p.Running);
        Assert.AreEqual(CampActivity.Fish, p.Activity);
        Assert.AreEqual(0.25f, p.Fraction, 1e-6f);
        Assert.AreEqual(10d, p.TotalHours);
    }

    [TestMethod]
    public void Advance_AddsTheHours_AndClampsAtTheTarget()
    {
        Working(CampActivity.Hunt, done: 8f);

        var p = _sut.Advance(1.5d);
        Assert.AreEqual(9.5f, _camp.ActivityHoursDone, 1e-5f);
        Assert.AreEqual(0.95f, p.Fraction, 1e-5f);

        p = _sut.Advance(100d);
        Assert.AreEqual(10f, _camp.ActivityHoursDone);
        Assert.AreEqual(1f, p.Fraction);
        Assert.IsTrue(p.Running, "the engine ends the wait at 1 and runs the completion; the work is still saved until then");
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(-2d)]
    [DataRow(0d)]
    public void Advance_IgnoresADeltaThatIsNotAPositiveFiniteNumber(double dt)
    {
        Working(CampActivity.Hunt, done: 3f);

        var p = _sut.Advance(dt);

        Assert.AreEqual(3f, _camp.ActivityHoursDone);
        Assert.IsTrue(p.Running);
    }

    [TestMethod]
    public void Advance_WithNoWork_DoesNothing()
    {
        var p = _sut.Advance(1d);

        Assert.IsFalse(p.Running);
        _work.DidNotReceiveWithAnyArgs().NextRandom();
    }

    [TestMethod]
    public void Advance_ARecordThatCannotRun_IsClearedAndNotRunning()
    {
        Working(CampActivity.Hunt, done: float.NaN);

        var p = _sut.Advance(1d);

        Assert.IsFalse(p.Running);
        Assert.AreEqual(0, _camp.Activity);
    }

    // ---- The injury roll

    [TestMethod]
    public void TheInjuryRoll_HappensOnceWhenProgressCrossesHalf_NotBefore()
    {
        Working(CampActivity.Hunt);
        _work.NextRandom().Returns(0.5f);

        _sut.Advance(4.9d);
        _work.DidNotReceive().NextRandom();
        Assert.IsFalse(_camp.InjuryRolled);

        _sut.Advance(0.2d);
        _work.Received(1).NextRandom();
        Assert.IsTrue(_camp.InjuryRolled, "the flag is saved, so a reload cannot roll again");

        _sut.Advance(3d);
        _work.Received(1).NextRandom();
    }

    [TestMethod]
    public void TheInjuryRoll_ASavedFlag_StopsASecondRoll()
    {
        Working(CampActivity.Hunt, done: 4.9f, rolled: true);

        _sut.Advance(0.5d);

        _work.DidNotReceiveWithAnyArgs().NextRandom();
    }

    [TestMethod]
    public void AHit_WoundsAboutFivePercentOfRegulars_AndReportsHowManyOnThatTickOnly()
    {
        Working(CampActivity.Hunt);
        _facts.HealthyRegulars = 60;
        _work.NextRandom().Returns(0.05f);

        var p = _sut.Advance(6d);

        _work.Received(1).WoundRegulars(3);
        Assert.AreEqual(3, p.Injured);
        Assert.AreEqual(0, _sut.Advance(1d).Injured);
    }

    [TestMethod]
    public void AMiss_WoundsNobody()
    {
        Working(CampActivity.Hunt);
        _work.NextRandom().Returns(0.5f);

        var p = _sut.Advance(6d);

        _work.DidNotReceiveWithAnyArgs().WoundRegulars(0);
        Assert.AreEqual(0, p.Injured);
    }

    [TestMethod]
    public void AHit_WithOnlyHeroesHealthy_WoundsNobody()
    {
        Working(CampActivity.Hunt);
        _facts.HealthyRegulars = 0;
        _work.NextRandom().Returns(0.0f);

        var p = _sut.Advance(6d);

        Assert.AreEqual(0, p.Injured);
        _work.DidNotReceiveWithAnyArgs().WoundRegulars(0);
    }

    // ---- The payout

    [TestMethod]
    public void Complete_PaysTheGoods_TheMorale_AndTakesTheTools_ThenClearsTheWork()
    {
        Working(CampActivity.Hunt, done: 10f);
        _facts.HealthyMen = 25;

        var report = _sut.Complete();

        Assert.IsNotNull(report);
        Assert.AreEqual(CampActivity.Hunt, report!.Value.Activity);
        Assert.AreEqual(25, report.Value.Goods, "10 per ten men, 25 men, forest");
        _work.Received(1).AddGoods(CampActivity.Hunt, 25);
        _work.Received(1).TakeTools(3);
        Assert.AreEqual(3, report.Value.ToolsUsed);
        // 25 men: one full ten above ten is the cost, tier 2 takes a fifth off it. The work costs morale, it never raises it.
        _work.Received(1).LowerMorale(Arg.Is<float>(m => Math.Abs(m - 0.8f) < 1e-5f));
        Assert.AreEqual(0, _camp.Activity);
        Assert.AreEqual(0f, _camp.ActivityHoursDone);
    }

    [TestMethod]
    public void Complete_TenMenOrFewer_UseNoToolsAndCostNoMorale()
    {
        Working(CampActivity.Hunt, done: 10f);
        _facts.HealthyMen = 10;

        var report = _sut.Complete();

        Assert.AreEqual(10, report!.Value.Goods);
        _work.DidNotReceiveWithAnyArgs().TakeTools(0);
        _work.DidNotReceiveWithAnyArgs().LowerMorale(0f);
    }

    [TestMethod]
    public void Complete_UsesTheGroundFactor_AndTheCap()
    {
        Working(CampActivity.Hunt, done: 10f);
        _facts.HealthyMen = 10;
        _facts.TerrainType = (int)TerrainType.Desert;
        Assert.AreEqual(2, _sut.Complete()!.Value.Goods);

        Working(CampActivity.Hunt, done: 10f);
        _facts.HealthyMen = 500;
        _facts.TerrainType = (int)TerrainType.Forest;
        _facts.ToolsHeld = 99;
        Assert.AreEqual(30, _sut.Complete()!.Value.Goods);
    }

    [TestMethod]
    public void Complete_TheToolsTaken_NeverExceedWhatIsHeld()
    {
        Working(CampActivity.Hunt, done: 10f);
        _facts.HealthyMen = 40;
        _facts.ToolsHeld = 1;

        var report = _sut.Complete();

        _work.Received(1).TakeTools(1);
        Assert.AreEqual(1, report!.Value.ToolsUsed);
    }

    [TestMethod]
    public void Complete_Fish_ReadsTheWaterAtThePayout()
    {
        Working(CampActivity.Fish, done: 10f);
        _facts.HealthyMen = 10;
        _facts.TerrainType = (int)TerrainType.Plain;

        Assert.AreEqual(10, _sut.Complete()!.Value.Goods);
        _work.Received(1).AddGoods(CampActivity.Fish, 10);
    }

    [TestMethod]
    public void Complete_ClearsTheWorkBeforeAnythingIsWritten_SoAThrowCannotPayTwice()
    {
        Working(CampActivity.Hunt, done: 10f);
        int activityWhenWriting = -1;
        _work.AddGoods(Arg.Any<CampActivity>(), Arg.Any<int>()).Returns(_ =>
        {
            activityWhenWriting = _camp.Activity;
            throw new InvalidOperationException("the roster write failed");
        });

        Assert.ThrowsException<InvalidOperationException>(() => _sut.Complete());

        Assert.AreEqual(0, activityWhenWriting);
        Assert.IsNull(_sut.Complete(), "a second completion pays nothing");
    }

    [TestMethod]
    public void Complete_WithNoWork_ReturnsNull_AndWritesNothing()
    {
        Assert.IsNull(_sut.Complete());
        _work.DidNotReceiveWithAnyArgs().AddGoods(default, 0);
    }

    [TestMethod]
    public void Complete_InASharedCampaign_DropsTheWorkAndPaysNothing()
    {
        Working(CampActivity.Hunt, done: 10f);
        _coop.IsSessionActive.Returns(true);

        Assert.IsNull(_sut.Complete());

        Assert.AreEqual(0, _camp.Activity);
        _work.DidNotReceiveWithAnyArgs().AddGoods(default, 0);
        _work.DidNotReceiveWithAnyArgs().LowerMorale(0f);
    }

    // ---- Stop, break, fault

    [TestMethod]
    public void Stop_DiscardsTheProgress_AndPaysNothing()
    {
        Working(CampActivity.Hunt, done: 9f);

        Assert.IsTrue(_sut.Stop());

        Assert.AreEqual(0, _camp.Activity);
        Assert.AreEqual(0f, _camp.ActivityHoursDone);
        _work.DidNotReceiveWithAnyArgs().AddGoods(default, 0);
        Assert.IsFalse(_sut.Stop(), "nothing left to stop");
    }

    [TestMethod]
    public void BreakingTheCamp_TakesTheWorkWithIt_AndANewCampStartsClean()
    {
        Working(CampActivity.Hunt, done: 9f);
        _camp = null!;
        Assert.IsFalse(_sut.Current().Running);
        Assert.IsFalse(_sut.Advance(1d).Running);
        Assert.IsNull(_sut.Complete());

        _camp = new CampState { TypeEnum = CampType.Field };
        Assert.IsFalse(_sut.Current().Running, "the new camp has no inherited work");
    }

    // ---- The master toggle (#860 review fix 2): a work ends the way a Far Watch ride does

    [TestMethod]
    public void TheMasterToggleOff_RefusesEveryOffer_AndTryStart()
    {
        _settings.Enabled.Returns(false);

        foreach (var activity in new[] { CampActivity.Hunt, CampActivity.Timber, CampActivity.Fish })
        {
            Assert.AreEqual(CampWorkBlock.FeatureDisabled, _sut.GetOffer(activity).Block, activity.ToString());
            Assert.AreEqual(CampWorkBlock.FeatureDisabled, _sut.TryStart(activity).Block, activity.ToString());
        }

        Assert.AreEqual(0, _camp.Activity);
    }

    [TestMethod]
    public void TheMasterToggleOff_CurrentClearsTheWork_AndIsNotRunning()
    {
        Working(CampActivity.Hunt, done: 4f);
        _settings.Enabled.Returns(false);

        Assert.IsFalse(_sut.Current().Running);

        Assert.AreEqual(0, _camp.Activity);
        Assert.AreEqual(0f, _camp.ActivityHoursDone);
    }

    [TestMethod]
    public void TheMasterToggleOff_AdvanceClearsTheWork_PaysNothing_AndMakesNoInjuryRoll()
    {
        Working(CampActivity.Hunt, done: 4.9f);
        _settings.Enabled.Returns(false);

        var p = _sut.Advance(2d);

        Assert.IsFalse(p.Running, "not running, so the wait menu lands");
        Assert.AreEqual(0, _camp.Activity);
        _work.DidNotReceiveWithAnyArgs().NextRandom();
        _work.DidNotReceiveWithAnyArgs().AddGoods(default, 0);
    }

    [TestMethod]
    public void TheMasterToggleOff_ACompletionThatArrivesFirst_PaysNothing()
    {
        Working(CampActivity.Hunt, done: 10f);
        _settings.Enabled.Returns(false);

        Assert.IsNull(_sut.Complete());

        Assert.AreEqual(0, _camp.Activity);
        _work.DidNotReceiveWithAnyArgs().AddGoods(default, 0);
        _work.DidNotReceiveWithAnyArgs().LowerMorale(0f);
        _work.DidNotReceiveWithAnyArgs().TakeTools(0);
    }

    // ---- A shared campaign ends the work at the next tick (#860 review fix 3)

    [TestMethod]
    public void ASharedCampaign_AdvanceClearsTheWork_ReportsWhyOnce_BeforeAnyInjuryRoll()
    {
        Working(CampActivity.Hunt, done: 4.9f);
        _coop.IsSessionActive.Returns(true);
        _work.NextRandom().Returns(0f);

        var p = _sut.Advance(1d);

        Assert.IsFalse(p.Running, "not running, so the wait lands");
        Assert.IsTrue(p.EndedBySharedCampaign, "the menu prints the line; the service composes no player text");
        Assert.AreEqual(0, _camp.Activity);
        _work.DidNotReceiveWithAnyArgs().NextRandom();
        _work.DidNotReceiveWithAnyArgs().WoundRegulars(0);
        _work.DidNotReceiveWithAnyArgs().ShowMessage(default!);

        var next = _sut.Advance(1d);
        Assert.IsFalse(next.Running);
        Assert.IsFalse(next.EndedBySharedCampaign, "the reason is reported once, by the call that ended the work");
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void EachTermOfTheCoopGate_EndsARunningWorkAtTheTick(int term)
    {
        Working(CampActivity.Hunt, done: 1f);
        switch (term)
        {
            case 1: _coop.IsSessionActive.Returns(true); break;
            case 2: _coop.ShouldDeferToHost.Returns(true); break;
            case 3: _dedicated.IsDedicatedServer.Returns(true); break;
            default: _coop.IsAuthority.Returns(false); break;
        }

        var p = _sut.Advance(1d);
        Assert.IsFalse(p.Running);
        Assert.IsTrue(p.EndedBySharedCampaign);
        Assert.AreEqual(0, _camp.Activity);
    }

    [TestMethod]
    public void TheMasterToggleOff_EndsTheWorkSilently_NotAsASharedCampaign()
    {
        Working(CampActivity.Hunt, done: 1f);
        _settings.Enabled.Returns(false);

        var p = _sut.Advance(1d);

        Assert.IsFalse(p.Running);
        Assert.IsFalse(p.EndedBySharedCampaign);
    }

    [TestMethod]
    public void HasWork_FollowsTheSavedRecord()
    {
        Assert.IsFalse(_sut.HasWork);
        Working(CampActivity.Timber);
        Assert.IsTrue(_sut.HasWork);
    }
}
