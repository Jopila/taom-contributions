using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FarWatch;
using TAOM.Features.FieldCamp;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.FarWatch;

/// <summary>
/// The rules of Far Watch (docs/features/far-watch.md): what the camp option shows and who rides, how a ride starts, runs,
/// returns and ends early, what the hourly look decides, and the sight it adds. The service runs over the real store and
/// the real Hidden Hosts service (the seam is the store itself); the engine is the substituted adapter. The report's contents
/// are in <see cref="FarWatchSweepTests"/>.
/// </summary>
[TestClass]
public class FarWatchServiceTests
{
    private const double Now = 1000d;

    private FarWatchStore _store = null!;
    private IFarWatchWorldAdapter _world = null!;
    private IHiddenHostsSettings _hiddenSettings = null!;
    private HiddenHostsService _hidden = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private ICampSettingsProvider _camps = null!;
    private IModLogger _logger = null!;
    private FarWatchService _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new FarWatchStore(_logger);
        _world = Substitute.For<IFarWatchWorldAdapter>();
        _world.NowHours.Returns(Now);
        _hiddenSettings = Substitute.For<IHiddenHostsSettings>();
        _hiddenSettings.Enabled.Returns(true);
        _hidden = new HiddenHostsService(_hiddenSettings, _store);
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _camps = Substitute.For<ICampSettingsProvider>();
        _camps.Enabled.Returns(true);
        _sut = new FarWatchService(_store, _world, _hidden, _coop, _dedicated, _camps, _logger);
    }

    /// <summary>A raised camp, a Ranger companion (Scouting 150) in the Scout role, a party of more than the hero.</summary>
    private static ScoutCampFacts Camp() => new()
    {
        CampExists = true, CampReady = true, MainHeroId = "main", MainHeroName = "Aldric", MainHeroSkill = 80,
        AssignedScoutId = "scout_1", AssignedScoutName = "Mirien", AssignedScoutSkill = 150, AssignedScoutInParty = true,
    };

    private void StartSurroundings(double at = Now, int skill = 150) =>
        _store.StartRide(ScoutKind.Surroundings, "scout_1", "Mirien", false, null, null, at, FarWatchMath.RideHours(ScoutKind.Surroundings, skill), skill);

    private void StartSettlement(double at = Now, int skill = 150) =>
        _store.StartRide(ScoutKind.Settlement, "scout_1", "Mirien", false, "town_9", "Edoras", at, FarWatchMath.RideHours(ScoutKind.Settlement, skill), skill);

    // ---- The offer

    [TestMethod]
    public void GetOffer_ARaisedCampAndAnAssignedScout_IsReady_WithWhoRides()
    {
        var offer = _sut.GetOffer(Camp(), Now);

        Assert.AreEqual(ScoutOfferStatus.Ready, offer.Status);
        Assert.AreEqual("scout_1", offer.ScoutId);
        Assert.AreEqual("Mirien", offer.ScoutName);
        Assert.AreEqual(150, offer.ScoutSkill);
        Assert.IsFalse(offer.SelfScout);
    }

    [TestMethod]
    public void GetOffer_ARideRunning_IsRunning_WhateverElseIsTrue()
    {
        StartSurroundings();
        _camps.Enabled.Returns(false);
        _coop.IsSessionActive.Returns(true);

        Assert.AreEqual(ScoutOfferStatus.Running, _sut.GetOffer(Camp() with { CampExists = false, InEncounter = true }, Now).Status);
    }

    [TestMethod]
    public void GetOffer_TheCampFeatureOff_AndNoRide_IsHidden()
    {
        _camps.Enabled.Returns(false);

        Assert.AreEqual(ScoutOfferStatus.Hidden, _sut.GetOffer(Camp(), Now).Status);
    }

    [DataTestMethod]
    [DataRow("session")]
    [DataRow("defer")]
    [DataRow("dedicated")]
    [DataRow("notauthority")]
    public void GetOffer_ASharedCampaign_IsBlocked(string why)
    {
        switch (why)
        {
            case "session": _coop.IsSessionActive.Returns(true); break;
            case "defer": _coop.ShouldDeferToHost.Returns(true); break;
            case "dedicated": _dedicated.IsDedicatedServer.Returns(true); break;
            default: _coop.IsAuthority.Returns(false); break;
        }

        var offer = _sut.GetOffer(Camp(), Now);

        Assert.AreEqual(ScoutOfferStatus.Blocked, offer.Status, why);
        Assert.AreEqual(ScoutBlock.SharedCampaign, offer.Block, why);
    }

    [DataTestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void GetOffer_AnEncounterOrABattle_IsBusy(bool encounter, bool mapEvent)
    {
        var offer = _sut.GetOffer(Camp() with { InEncounter = encounter, InMapEvent = mapEvent }, Now);

        Assert.AreEqual(ScoutBlock.Busy, offer.Block);
        Assert.AreEqual(ScoutOfferStatus.Blocked, offer.Status);
    }

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void GetOffer_ACampNotStandingOrNotYetRaised_IsBlocked(bool exists, bool ready)
    {
        var offer = _sut.GetOffer(Camp() with { CampExists = exists, CampReady = ready }, Now);

        Assert.AreEqual(ScoutBlock.CampNotReady, offer.Block);
    }

    [TestMethod]
    public void GetOffer_NoScoutInTheRoleAndAPartyBeyondTheHero_NeedsAScout()
    {
        var offer = _sut.GetOffer(Camp() with { AssignedScoutId = null, AssignedScoutInParty = false }, Now);

        Assert.AreEqual(ScoutBlock.NoScout, offer.Block);
    }

    [TestMethod]
    public void GetOffer_AScoutWhoLeftTheParty_IsNoScout() =>
        Assert.AreEqual(ScoutBlock.NoScout, _sut.GetOffer(Camp() with { AssignedScoutInParty = false }, Now).Block);

    [TestMethod]
    public void GetOffer_TheMainHeroInTheScoutRole_DoesNotCountAsAScout_UnlessHeIsAlone()
    {
        var asScout = Camp() with { AssignedScoutId = "main", AssignedScoutName = "Aldric" };

        Assert.AreEqual(ScoutBlock.NoScout, _sut.GetOffer(asScout, Now).Block, "a hero cannot send himself while he leads a party");

        var offer = _sut.GetOffer(asScout with { PartyAlone = true }, Now);
        Assert.AreEqual(ScoutOfferStatus.Ready, offer.Status);
        Assert.IsTrue(offer.SelfScout);
        Assert.AreEqual("main", offer.ScoutId);
    }

    [TestMethod]
    public void GetOffer_AloneWithNoAssignedScout_TheHeroRidesHimself()
    {
        var offer = _sut.GetOffer(Camp() with { PartyAlone = true, AssignedScoutId = null, AssignedScoutInParty = false }, Now);

        Assert.AreEqual(ScoutOfferStatus.Ready, offer.Status);
        Assert.IsTrue(offer.SelfScout);
        Assert.AreEqual("main", offer.ScoutId);
        Assert.AreEqual("Aldric", offer.ScoutName);
        Assert.AreEqual(80, offer.ScoutSkill);
    }

    [TestMethod]
    public void GetOffer_AloneWithAnAssignedScoutInTheParty_NeverHappens_ButTheAssignedOneWins()
    {
        // PartyAlone says the roster is only the hero; if a scout is nonetheless reported inside, he is the one who rides.
        var offer = _sut.GetOffer(Camp() with { PartyAlone = true }, Now);

        Assert.AreEqual("scout_1", offer.ScoutId);
        Assert.IsFalse(offer.SelfScout);
    }

    [TestMethod]
    public void GetOffer_AloneWithNoMainHero_IsNoScout() =>
        Assert.AreEqual(ScoutBlock.NoScout,
            _sut.GetOffer(Camp() with { PartyAlone = true, AssignedScoutId = null, AssignedScoutInParty = false, MainHeroId = null }, Now).Block);

    [TestMethod]
    public void GetOffer_AWoundedScout_IsBlocked_NamingHim()
    {
        var offer = _sut.GetOffer(Camp() with { AssignedScoutWounded = true }, Now);

        Assert.AreEqual(ScoutBlock.Wounded, offer.Block);
        Assert.AreEqual("Mirien", offer.ScoutName);
    }

    [TestMethod]
    public void GetOffer_AWoundedHeroRidingAlone_IsBlocked_ButAWoundedHeroLeadingAPartyIsNot()
    {
        var alone = Camp() with { PartyAlone = true, AssignedScoutId = null, AssignedScoutInParty = false, MainHeroWounded = true };

        Assert.AreEqual(ScoutBlock.Wounded, _sut.GetOffer(alone, Now).Block);
        Assert.AreEqual(ScoutOfferStatus.Ready, _sut.GetOffer(Camp() with { MainHeroWounded = true }, Now).Status);
    }

    [TestMethod]
    public void GetOffer_ARestingScout_IsBlocked_WithTheHoursLeftRoundedUp()
    {
        _store.SetRest(Now + 3.2d);

        var offer = _sut.GetOffer(Camp(), Now);

        Assert.AreEqual(ScoutBlock.Resting, offer.Block);
        Assert.AreEqual(4, offer.RestHoursLeft);
        Assert.AreEqual("Mirien", offer.ScoutName);
    }

    [TestMethod]
    public void GetOffer_TheRestEndsAtItsHour()
    {
        _store.SetRest(Now);
        Assert.AreEqual(ScoutOfferStatus.Ready, _sut.GetOffer(Camp(), Now).Status);

        _store.SetRest(Now + 0.001d);
        Assert.AreEqual(1, _sut.GetOffer(Camp(), Now).RestHoursLeft);
    }

    [TestMethod]
    public void GetOffer_ARestingScout_NamesTheRiderWhoRests_NotTheScoutNamedNow()
    {
        _store.SetRest(Now + 3d, "Mirien");
        var another = Camp() with { AssignedScoutId = "scout_2", AssignedScoutName = "Tamsin" };

        var offer = _sut.GetOffer(another, Now);

        Assert.AreEqual(ScoutBlock.Resting, offer.Block);
        Assert.AreEqual("Mirien", offer.ScoutName);
    }

    [TestMethod]
    public void GetOffer_ARestWithNoStoredRider_FallsBackToTheScoutNamedNow()
    {
        _store.SetRest(Now + 3d);

        Assert.AreEqual("Mirien", _sut.GetOffer(Camp(), Now).ScoutName);
    }

    [DataTestMethod]
    [DataRow("complete")]
    [DataRow("end")]
    public void TheRest_RemembersWhoRode(string how)
    {
        StartSurroundings(at: Now - 6d);
        HeroIsHome();

        if (how == "complete") _sut.Complete(Now);
        else _sut.End(Now, ScoutEndReason.CallBack);

        Assert.AreEqual("Mirien", _store.RestRiderName);
    }

    [TestMethod]
    public void GetOffer_ASoloRideRunning_SaysItIsTheHeroHimself()
    {
        _store.StartRide(ScoutKind.Surroundings, "main", "Aldric", true, null, null, Now, 8d, 80);

        Assert.IsTrue(_sut.GetOffer(Camp(), Now).SelfScout);
        Assert.IsTrue(_sut.Progress(Now).SelfScout);
    }

    [TestMethod]
    public void GetOffer_ACompanionsRideRunning_IsNotASoloRide()
    {
        StartSurroundings();

        Assert.IsFalse(_sut.GetOffer(Camp(), Now).SelfScout);
        Assert.IsFalse(_sut.Progress(Now).SelfScout);
    }

    [TestMethod]
    public void GetOffer_WhenSeveralBlocksApply_TheFirstInTheDocumentedOrderWins()
    {
        var all = Camp() with
        {
            InEncounter = true, CampReady = false, AssignedScoutInParty = false, AssignedScoutWounded = true,
        };
        _store.SetRest(Now + 5d);
        _coop.IsSessionActive.Returns(true);

        Assert.AreEqual(ScoutBlock.SharedCampaign, _sut.GetOffer(all, Now).Block);
        _coop.IsSessionActive.Returns(false);
        Assert.AreEqual(ScoutBlock.Busy, _sut.GetOffer(all, Now).Block);
        all = all with { InEncounter = false };
        Assert.AreEqual(ScoutBlock.CampNotReady, _sut.GetOffer(all, Now).Block);
        all = all with { CampReady = true };
        Assert.AreEqual(ScoutBlock.NoScout, _sut.GetOffer(all, Now).Block);
        all = all with { AssignedScoutInParty = true };
        Assert.AreEqual(ScoutBlock.Wounded, _sut.GetOffer(all, Now).Block);
        all = all with { AssignedScoutWounded = false };
        Assert.AreEqual(ScoutBlock.Resting, _sut.GetOffer(all, Now).Block);
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(-1d)]
    [DataRow(double.PositiveInfinity)]
    public void GetOffer_NoUsableClock_OffersNothing(double now) =>
        Assert.AreEqual(ScoutOfferStatus.Hidden, _sut.GetOffer(Camp(), now).Status);

    // ---- Starting

    [TestMethod]
    public void TryStart_Surroundings_StoresTheRideWithItsLength()
    {
        var offer = _sut.TryStart(ScoutKind.Surroundings, null, null, Camp(), Now);

        Assert.AreEqual(ScoutOfferStatus.Ready, offer.Status);
        Assert.AreEqual(ScoutKind.Surroundings, _store.Kind);
        Assert.AreEqual("scout_1", _store.HeroId);
        Assert.AreEqual("Mirien", _store.HeroName);
        Assert.IsFalse(_store.SelfScout);
        Assert.AreEqual(Now, _store.StartHour);
        Assert.AreEqual(6d, _store.DurationHours, 1e-9, "8 less 4 x 150 / 300");
        Assert.AreEqual(150, _store.Skill);
    }

    [TestMethod]
    public void TryStart_Settlement_StoresTheTargetAndTheShorterLength()
    {
        _sut.TryStart(ScoutKind.Settlement, "town_9", "Edoras", Camp(), Now);

        Assert.AreEqual(ScoutKind.Settlement, _store.Kind);
        Assert.AreEqual("town_9", _store.TargetId);
        Assert.AreEqual("Edoras", _store.TargetName);
        Assert.AreEqual(4.2d, _store.DurationHours, 1e-9);
        Assert.IsTrue(_store.IsScoutAway);
    }

    [TestMethod]
    public void TryStart_TheHeroRidingAlone_IsRecordedAsSelf()
    {
        _sut.TryStart(ScoutKind.Surroundings, null, null, Camp() with { PartyAlone = true, AssignedScoutId = null, AssignedScoutInParty = false }, Now);

        Assert.IsTrue(_store.SelfScout);
        Assert.AreEqual("main", _store.HeroId);
        Assert.AreEqual(80, _store.Skill);
    }

    [TestMethod]
    public void TryStart_RunsTheSameChecksAsTheOffer_AndStartsNothingWhenBlocked()
    {
        _store.SetRest(Now + 3d);

        var offer = _sut.TryStart(ScoutKind.Surroundings, null, null, Camp(), Now);

        Assert.AreEqual(ScoutOfferStatus.Blocked, offer.Status);
        Assert.AreEqual(ScoutBlock.Resting, offer.Block);
        Assert.IsFalse(_store.HasRide);
    }

    [TestMethod]
    public void TryStart_ACampThatIsNotStanding_StartsNothing()
    {
        _sut.TryStart(ScoutKind.Surroundings, null, null, Camp() with { CampExists = false }, Now);

        Assert.IsFalse(_store.HasRide);
    }

    [TestMethod]
    public void TryStart_WhileARideRuns_ChangesNothing()
    {
        StartSurroundings(at: Now - 2d);

        var offer = _sut.TryStart(ScoutKind.Settlement, "town_9", "Edoras", Camp(), Now);

        Assert.AreEqual(ScoutOfferStatus.Running, offer.Status);
        Assert.AreEqual(ScoutKind.Surroundings, _store.Kind);
        Assert.AreEqual(Now - 2d, _store.StartHour);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void TryStart_ASettlementRideWithoutASettlement_StartsNothing(string? site)
    {
        var offer = _sut.TryStart(ScoutKind.Settlement, site, null, Camp(), Now);

        Assert.AreNotEqual(ScoutOfferStatus.Ready, offer.Status);
        Assert.IsFalse(_store.HasRide);
    }

    [TestMethod]
    public void TryStart_NoKind_StartsNothing()
    {
        _sut.TryStart(ScoutKind.None, null, null, Camp(), Now);

        Assert.IsFalse(_store.HasRide);
    }

    [TestMethod]
    public void TryStart_KeepsTheBuffAndTheNamedHosts()
    {
        _store.SetBuff(0.5f, Now - 5d, 24d);
        _store.SetNamed(new[] { "p1" }, Now + 30d);

        _sut.TryStart(ScoutKind.Surroundings, null, null, Camp(), Now);

        Assert.AreEqual(0.5f, _store.BuffLevel);
        Assert.AreEqual(1, _store.NamedCount);
    }

    // ---- Progress

    [TestMethod]
    public void Progress_NoRide_IsNotRunning() => Assert.IsFalse(_sut.Progress(Now).Running);

    [TestMethod]
    public void Progress_ARideRunning_SaysHowFarAlong()
    {
        StartSettlement(at: Now - 2.1d);

        var p = _sut.Progress(Now);

        Assert.IsTrue(p.Running);
        Assert.AreEqual(0.5f, p.Fraction, 1e-5f);
        Assert.AreEqual(4.2d, p.TotalHours, 1e-9);
        Assert.AreEqual(ScoutKind.Settlement, p.Kind);
        Assert.AreEqual("Mirien", p.ScoutName);
        Assert.AreEqual("Edoras", p.TargetName);
    }

    [TestMethod]
    public void Progress_PastTheEnd_IsOne_AndStillRunningUntilCompleted()
    {
        StartSurroundings(at: Now - 20d);

        var p = _sut.Progress(Now);

        Assert.IsTrue(p.Running);
        Assert.AreEqual(1f, p.Fraction);
    }

    [TestMethod]
    public void Progress_ABadClock_IsZero_SoItNeverCompletes()
    {
        StartSurroundings();

        Assert.AreEqual(0f, _sut.Progress(double.NaN).Fraction);
    }

    // ---- Completing

    [TestMethod]
    public void Complete_NoRide_ReturnsNull_AndDoesNothing()
    {
        Assert.IsNull(_sut.Complete(Now));

        _world.DidNotReceiveWithAnyArgs().GrantScoutingXp(default!, default);
    }

    [TestMethod]
    public void Complete_ASecondCall_ReturnsNull_SoAnEngineCompletionAndAReconcileCannotBothPay()
    {
        StartSurroundings(at: Now - 6d);
        _world.IsHeroWithParty("scout_1").Returns(true);

        Assert.IsNotNull(_sut.Complete(Now));
        Assert.IsNull(_sut.Complete(Now));

        _world.Received(1).GrantScoutingXp(Arg.Any<string>(), Arg.Any<float>());
    }

    private void HeroIsHome(string id = "scout_1") => _world.IsHeroWithParty(id).Returns(true);

    [TestMethod]
    public void Complete_Surroundings_SetsTheBuff_AndTheRest_AndClearsTheRide()
    {
        StartSurroundings(at: Now - 6d, skill: 150);
        HeroIsHome();

        var report = _sut.Complete(Now);

        Assert.IsNotNull(report);
        Assert.AreEqual(ScoutKind.Surroundings, report!.Kind);
        Assert.AreEqual("Mirien", report.ScoutName);
        Assert.IsFalse(report.Lost);
        Assert.IsFalse(_store.HasRide);
        Assert.AreEqual(0.5f, _store.BuffLevel);
        Assert.AreEqual(Now, _store.BuffStartHour);
        Assert.AreEqual(36d, _store.BuffHoldHours, 1e-9, "24 x (1 + 150 / 300)");
        Assert.AreEqual(Now + 8d, _store.RestUntilHour);
        Assert.AreEqual(36, report.SightHours);
    }

    [TestMethod]
    public void Complete_Surroundings_PaysTheXpToTheStoredHero_150PlusHalfTheSkill()
    {
        StartSurroundings(at: Now - 6d, skill: 150);
        HeroIsHome();

        _sut.Complete(Now);

        _world.Received(1).GrantScoutingXp("scout_1", 225f);
    }

    [TestMethod]
    public void Complete_TheStoreIsAlreadyClear_WhenTheXpIsWritten()
    {
        StartSurroundings(at: Now - 6d);
        HeroIsHome();
        bool hadRideWhenPaid = true;
        _world.When(w => w.GrantScoutingXp(Arg.Any<string>(), Arg.Any<float>())).Do(_ => hadRideWhenPaid = _store.HasRide);

        _sut.Complete(Now);

        Assert.IsFalse(hadRideWhenPaid, "a throw in the world write must not leave a ride that pays again");
    }

    [TestMethod]
    public void Complete_AThrowingXpWrite_LeavesNoRideBehind()
    {
        StartSurroundings(at: Now - 6d);
        HeroIsHome();
        _world.When(w => w.GrantScoutingXp(Arg.Any<string>(), Arg.Any<float>())).Do(_ => throw new InvalidOperationException("boom"));

        Assert.ThrowsException<InvalidOperationException>(() => _sut.Complete(Now));

        Assert.IsFalse(_store.HasRide);
        Assert.IsNull(_sut.Complete(Now));
    }

    [TestMethod]
    public void Complete_Settlement_ReadsTheSite_RemembersIt_AndSetsNoBuff()
    {
        StartSettlement(at: Now - 4.2d);
        HeroIsHome();
        _world.ReadSite("town_9").Returns(new ScoutSiteReport
        {
            Found = true, Name = "Edoras",
            Intel = new SettlementIntelSnapshot { HasData = true, StringId = "town_9", DefenderCount = 245, NowHour = 1000, MainSeeingRange = 100f, DistanceToMain = 70f },
            Defenders = { new ScoutDefender { Kind = ScoutDefenderKind.Garrison, Name = "Edoras Garrison", Count = 245, Foot = 245 } },
        });

        var report = _sut.Complete(Now)!;

        Assert.AreEqual(ScoutKind.Settlement, report.Kind);
        Assert.AreEqual("Edoras", report.SiteName);
        Assert.AreEqual(245, report.SiteTotal);
        Assert.AreEqual(1, report.Defenders.Count);
        Assert.AreEqual(0, report.SightHours);
        Assert.AreEqual(0f, _store.BuffLevel);
        Assert.AreEqual(Now + 8d, _store.RestUntilHour);
        Assert.AreEqual("~245", _hidden.EvaluateTooltipSettlement(new SettlementIntelSnapshot
        {
            HasData = true, StringId = "town_9", DefenderCount = 245, NowHour = 1002, MainSeeingRange = 100f, DistanceToMain = 90f,
        }).MemoryText, "Hidden Hosts remembers the defenders exactly");
        _world.Received(1).GrantScoutingXp("scout_1", 225f);
    }

    [TestMethod]
    public void Complete_ASettlementThatCannotBeFound_ReportsNoDefenders_AndStillPays()
    {
        StartSettlement(at: Now - 4.2d);
        HeroIsHome();
        _world.ReadSite("town_9").Returns(new ScoutSiteReport());

        var report = _sut.Complete(Now)!;

        Assert.IsFalse(report.Lost);
        Assert.AreEqual(0, report.Defenders.Count);
        Assert.AreEqual("Edoras", report.SiteName, "the stored name stands in");
    }

    [TestMethod]
    public void Complete_FieldCampTurnedOff_EndsTheRideAsCampStruck_WithNoReportNoXpAndNoSightLine()
    {
        StartSurroundings(at: Now - 6d);
        HeroIsHome();
        _camps.Enabled.Returns(false);

        var report = _sut.Complete(Now)!;

        Assert.IsNotNull(report.Ended);
        Assert.AreEqual(ScoutEndReason.CampStruck, report.Ended!.Value.Reason);
        Assert.AreEqual("Mirien", report.Ended.Value.ScoutName);
        Assert.AreEqual(0, report.Hosts.Count);
        Assert.AreEqual(0, report.SightHours, "the contributor returns 0 while Field Camp is off, so no sight is promised");
        Assert.IsFalse(_store.HasRide);
        Assert.AreEqual(Now + 8d, _store.RestUntilHour);
        _world.DidNotReceiveWithAnyArgs().GrantScoutingXp(default!, default);
        _world.DidNotReceiveWithAnyArgs().CollectSightings(default!);
    }

    [TestMethod]
    public void Complete_AWeakerScoutsReturn_DoesNotShortenALongerHeldFullBuff()
    {
        _store.SetBuff(0.5f, Now - 12d, 48d);   // a Scouting 300 return 12 hours ago: full until Now + 36
        StartSurroundings(at: Now - 8d, skill: 0);
        HeroIsHome();

        var report = _sut.Complete(Now)!;

        Assert.AreEqual(Now - 12d, _store.BuffStartHour, "the held buff keeps its clock: the new 24 hour hold would end at Now + 24");
        Assert.AreEqual(48d, _store.BuffHoldHours, 1e-9);
        Assert.AreEqual(36, report.SightHours, "the report promises what is left of the held full strength, not the new ride's 24");
    }

    [TestMethod]
    public void Complete_ALongerHoldThanTheHeldBuffs_ReplacesIt()
    {
        _store.SetBuff(0.5f, Now - 12d, 24d);   // full until Now + 12
        StartSurroundings(at: Now - 4d, skill: 150);
        HeroIsHome();

        var report = _sut.Complete(Now)!;

        Assert.AreEqual(Now, _store.BuffStartHour);
        Assert.AreEqual(36d, _store.BuffHoldHours, 1e-9);
        Assert.AreEqual(36, report.SightHours);
    }

    [TestMethod]
    public void Complete_AHeldBuffThatIsAlreadyFading_OrBelowFull_IsReplaced()
    {
        _store.SetBuff(0.5f, Now - 60d, 48d);   // past its hold: fading
        StartSurroundings(at: Now - 8d, skill: 0);
        HeroIsHome();
        _sut.Complete(Now);
        Assert.AreEqual(Now, _store.BuffStartHour, "a fading buff gives way to a fresh full one");

        _store.SetBuff(0.4f, Now - 1d, 48d);    // held, but not at full strength
        StartSurroundings(at: Now - 8d, skill: 0);
        _sut.Complete(Now + 8d);
        Assert.AreEqual(0.5f, _store.BuffLevel, "a lesser level gives way to the full one");
    }

    [TestMethod]
    public void Complete_TheHeroGone_IsLost_NoXp_NoSweep_NoBuff_ButTheRestStarts()
    {
        StartSurroundings(at: Now - 6d);
        _world.IsHeroWithParty("scout_1").Returns(false);

        var report = _sut.Complete(Now)!;

        Assert.IsTrue(report.Lost);
        Assert.AreEqual("Mirien", report.ScoutName, "the stored name stands in for a hero who is gone");
        Assert.IsFalse(_store.HasRide);
        Assert.AreEqual(0f, _store.BuffLevel);
        Assert.AreEqual(Now + 8d, _store.RestUntilHour);
        _world.DidNotReceiveWithAnyArgs().GrantScoutingXp(default!, default);
        _world.DidNotReceiveWithAnyArgs().CollectSightings(default!);
        _world.DidNotReceiveWithAnyArgs().ReadSite(default!);
    }

    [TestMethod]
    public void Complete_ACoopClient_GetsTheReport_ButNoXp()
    {
        StartSurroundings(at: Now - 6d);
        HeroIsHome();
        _coop.IsCoopClient.Returns(true);

        Assert.IsNotNull(_sut.Complete(Now));

        _world.DidNotReceiveWithAnyArgs().GrantScoutingXp(default!, default);
    }

    [TestMethod]
    public void Complete_ANonAuthority_GetsNoXp()
    {
        StartSurroundings(at: Now - 6d);
        HeroIsHome();
        _coop.IsAuthority.Returns(false);

        _sut.Complete(Now);

        _world.DidNotReceiveWithAnyArgs().GrantScoutingXp(default!, default);
    }

    [TestMethod]
    public void Complete_ALongerRide_StillUsesTheStoredSkillForXp()
    {
        StartSurroundings(at: Now - 8d, skill: 0);
        HeroIsHome();

        _sut.Complete(Now);

        _world.Received(1).GrantScoutingXp("scout_1", 150f);
    }

    [TestMethod]
    public void Complete_KeepsAnExistingBuffThatIsHigherThanThePartialOne_ByAlwaysSettingTheFullOne()
    {
        _store.SetBuff(0.3f, Now - 10d, 24d);
        StartSurroundings(at: Now - 6d);
        HeroIsHome();

        _sut.Complete(Now);

        Assert.AreEqual(0.5f, _store.BuffLevel);
        Assert.AreEqual(Now, _store.BuffStartHour, "a fresh clock for the new sight");
    }

    // ---- Ending early

    [TestMethod]
    public void End_NoRide_ReturnsNull() => Assert.IsNull(_sut.End(Now, ScoutEndReason.CallBack));

    [DataTestMethod]
    [DataRow("CallBack")]
    [DataRow("Battle")]
    [DataRow("CampStruck")]
    [DataRow("Lost")]
    [DataRow("SharedCampaign")]
    public void End_EveryReason_ClearsTheRide_StartsTheRest_PaysNothing_AndNamesTheScout(string reasonName)
    {
        var reason = (ScoutEndReason)Enum.Parse(typeof(ScoutEndReason), reasonName);
        StartSurroundings();

        var notice = _sut.End(Now + 1d, reason);

        Assert.IsNotNull(notice);
        Assert.AreEqual(reason, notice!.Value.Reason);
        Assert.AreEqual("Mirien", notice.Value.ScoutName);
        Assert.IsFalse(_store.HasRide);
        Assert.AreEqual(Now + 1d + 8d, _store.RestUntilHour);
        _world.DidNotReceiveWithAnyArgs().GrantScoutingXp(default!, default);
        _world.DidNotReceiveWithAnyArgs().CollectSightings(default!);
        Assert.AreEqual(0, _store.NamedCount, "no report, so no named hosts");
    }

    [TestMethod]
    public void End_ASurroundingsRide_KeepsTheSightItHadReached()
    {
        StartSurroundings(at: Now, skill: 150);

        _sut.End(Now + 3d, ScoutEndReason.CallBack);

        Assert.AreEqual(0.25f, _store.BuffLevel, 1e-5f, "half way along 6 hours: half of the 0.5");
        Assert.AreEqual(Now + 3d, _store.BuffStartHour);
        Assert.AreEqual(36d, _store.BuffHoldHours, 1e-9);
    }

    [TestMethod]
    public void End_ASurroundingsRide_KeepsTheStrongerOfThePartialAndTheOldBuff()
    {
        _store.SetBuff(0.4f, Now - 1d, 24d);
        StartSurroundings(at: Now, skill: 150);

        _sut.End(Now + 3d, ScoutEndReason.CallBack);

        Assert.AreEqual(0.4f, _store.BuffLevel, 1e-5f);
        Assert.AreEqual(Now - 1d, _store.BuffStartHour, "a smaller partial sight leaves the larger one's clock alone");
        Assert.AreEqual(24d, _store.BuffHoldHours, 1e-9);
    }

    [TestMethod]
    public void End_AShortRideCalledBack_DoesNotRenewAnOlderLargerBuffsClock()
    {
        _store.SetBuff(0.5f, Now - 40d, 48d);
        StartSurroundings(at: Now - 0.1d, skill: 0);

        _sut.End(Now, ScoutEndReason.CallBack);

        Assert.AreEqual(0.5f, _store.BuffLevel);
        Assert.AreEqual(Now - 40d, _store.BuffStartHour, "the held sight ends when it was going to end");
        Assert.AreEqual(48d, _store.BuffHoldHours, 1e-9);
    }

    [TestMethod]
    public void End_ARideFurtherAlongThanTheHeldBuff_StartsAFreshBuff()
    {
        _store.SetBuff(0.1f, Now - 10d, 24d);
        StartSurroundings(at: Now, skill: 150);

        _sut.End(Now + 3d, ScoutEndReason.CallBack);

        Assert.AreEqual(0.25f, _store.BuffLevel, 1e-5f);
        Assert.AreEqual(Now + 3d, _store.BuffStartHour);
        Assert.AreEqual(36d, _store.BuffHoldHours, 1e-9);
    }

    [TestMethod]
    public void End_ASurroundingsRideCalledBackAtOnce_AddsNoBuff()
    {
        StartSurroundings(at: Now);

        _sut.End(Now, ScoutEndReason.CallBack);

        Assert.AreEqual(0f, _store.BuffLevel);
        Assert.AreEqual(0d, _store.BuffHoldHours);
    }

    [TestMethod]
    public void End_ASettlementRide_AddsNoBuff_AndLeavesAnOldOneAlone()
    {
        _store.SetBuff(0.4f, Now - 1d, 24d);
        StartSettlement(at: Now);

        _sut.End(Now + 2d, ScoutEndReason.Battle);

        Assert.AreEqual(0.4f, _store.BuffLevel);
        Assert.AreEqual(Now - 1d, _store.BuffStartHour);
    }

    [TestMethod]
    public void End_ASettlementRide_NoLongerLowersSight()
    {
        StartSettlement(at: Now);
        Assert.AreEqual(FarWatchMath.SettlementPenalty, _sut.SightFactor(Now + 1d));

        _sut.End(Now + 1d, ScoutEndReason.CallBack);

        Assert.AreEqual(0f, _sut.SightFactor(Now + 1d));
    }

    // ---- The hourly look

    [TestMethod]
    public void Reconcile_NoRide_DoesNothing_AndAsksNoOne()
    {
        var turn = _sut.Reconcile(Camp(), Now);

        Assert.AreEqual(ScoutTurnKind.Nothing, turn.Kind);
        _world.DidNotReceiveWithAnyArgs().IsHeroWithParty(default!);
    }

    [TestMethod]
    public void Reconcile_ARideInTime_WithEverythingInOrder_DoesNothing()
    {
        StartSurroundings(at: Now - 1d);
        HeroIsHome();

        Assert.AreEqual(ScoutTurnKind.Nothing, _sut.Reconcile(Camp(), Now).Kind);
        Assert.IsTrue(_store.HasRide);
    }

    [TestMethod]
    public void Reconcile_TheMomentTheRideIsDue_IsDue_AndTheCallerCompletes()
    {
        StartSurroundings(at: Now - 6d);
        HeroIsHome();

        Assert.AreEqual(ScoutTurnKind.Due, _sut.Reconcile(Camp(), Now).Kind);
        Assert.IsTrue(_store.HasRide, "reconcile judges; it does not complete");

        StartSurroundings(at: Now - 5.99d);
        Assert.AreEqual(ScoutTurnKind.Nothing, _sut.Reconcile(Camp(), Now).Kind);
    }

    [DataTestMethod]
    [DataRow("session", "SharedCampaign")]
    [DataRow("defer", "SharedCampaign")]
    [DataRow("dedicated", "SharedCampaign")]
    [DataRow("notauthority", "SharedCampaign")]
    [DataRow("camp", "CampStruck")]
    [DataRow("battle", "Battle")]
    [DataRow("encounter", "Battle")]
    [DataRow("hero", "Lost")]
    public void Reconcile_EachCause_EndsTheRide_WithItsReason(string cause, string expectedName)
    {
        var expected = (ScoutEndReason)Enum.Parse(typeof(ScoutEndReason), expectedName);
        StartSurroundings(at: Now - 1d);
        HeroIsHome();
        var camp = Camp();
        switch (cause)
        {
            case "session": _coop.IsSessionActive.Returns(true); break;
            case "defer": _coop.ShouldDeferToHost.Returns(true); break;
            case "dedicated": _dedicated.IsDedicatedServer.Returns(true); break;
            case "notauthority": _coop.IsAuthority.Returns(false); break;
            case "camp": camp = camp with { CampExists = false }; break;
            case "battle": camp = camp with { InMapEvent = true }; break;
            case "encounter": camp = camp with { InEncounter = true }; break;
            default: _world.IsHeroWithParty("scout_1").Returns(false); break;
        }

        var turn = _sut.Reconcile(camp, Now);

        Assert.AreEqual(ScoutTurnKind.Ended, turn.Kind, cause);
        Assert.AreEqual(expected, turn.Notice!.Value.Reason, cause);
        Assert.AreEqual("Mirien", turn.Notice.Value.ScoutName);
        Assert.IsFalse(_store.HasRide);
        Assert.AreEqual(Now + 8d, _store.RestUntilHour);
    }

    [TestMethod]
    public void Reconcile_ADueRideWhoseCampIsGone_EndsRatherThanCompletes()
    {
        StartSurroundings(at: Now - 6d);
        HeroIsHome();

        var turn = _sut.Reconcile(Camp() with { CampExists = false }, Now);

        Assert.AreEqual(ScoutTurnKind.Ended, turn.Kind);
        _world.DidNotReceiveWithAnyArgs().GrantScoutingXp(default!, default);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Reconcile_TheCampFeatureTurnedOffMidRide_EndsIt_SoTheSightAndTheAwayRuleStopTogether(bool due)
    {
        StartSurroundings(at: due ? Now - 7d : Now - 1d);
        _camps.Enabled.Returns(false);

        var turn = _sut.Reconcile(Camp(), Now);

        Assert.AreEqual(ScoutTurnKind.Ended, turn.Kind);
        Assert.AreEqual(ScoutEndReason.CampStruck, turn.Notice!.Value.Reason);
        Assert.IsFalse(_store.HasRide);
        Assert.IsFalse(_store.IsScoutAway);
        _world.DidNotReceiveWithAnyArgs().GrantScoutingXp(default!, default);
    }

    [TestMethod]
    public void Reconcile_TheCauses_AreJudgedInOrder_SharedThenCampThenBattleThenHeroThenDue()
    {
        StartSurroundings(at: Now - 7d);
        _world.IsHeroWithParty("scout_1").Returns(false);
        var camp = Camp() with { CampExists = false, InMapEvent = true };
        _coop.IsSessionActive.Returns(true);
        Assert.AreEqual(ScoutEndReason.SharedCampaign, EndedReason(camp));

        StartSurroundings(at: Now - 7d);
        _coop.IsSessionActive.Returns(false);
        Assert.AreEqual(ScoutEndReason.CampStruck, EndedReason(camp));

        StartSurroundings(at: Now - 7d);
        camp = camp with { CampExists = true };
        Assert.AreEqual(ScoutEndReason.Battle, EndedReason(camp));

        StartSurroundings(at: Now - 7d);
        camp = camp with { InMapEvent = false };
        Assert.AreEqual(ScoutEndReason.Lost, EndedReason(camp));
    }

    private ScoutEndReason EndedReason(ScoutCampFacts camp)
    {
        var turn = _sut.Reconcile(camp, Now);
        Assert.AreEqual(ScoutTurnKind.Ended, turn.Kind);
        return turn.Notice!.Value.Reason;
    }

    // ---- Sight

    [TestMethod]
    public void Sight_BothMembers_AllocateNothing_PerQuery()
    {
        var counter = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
        Assert.IsNotNull(counter, "the runtime must expose GC.GetAllocatedBytesForCurrentThread");
        var allocatedBytes = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), counter!);
        StartSurroundings(at: Now - 3d);
        _store.SetBuff(0.4f, Now - 30d, 24d);
        for (int i = 0; i < 5; i++) { _sut.SightFactor(Now); _sut.SightCapBonus(Now); }

        long before = allocatedBytes();
        float sink = 0f;
        for (int i = 0; i < 1000; i++) sink += _sut.SightFactor(Now) + _sut.SightCapBonus(Now);
        long allocated = allocatedBytes() - before;

        Assert.AreEqual(0L, allocated);
        Assert.IsTrue(sink > 0f, "and it did the work");
    }

    [TestMethod]
    public void Sight_NothingRunningAndNoBuff_IsZero()
    {
        Assert.AreEqual(0f, _sut.SightFactor(Now));
        Assert.AreEqual(0f, _sut.SightCapBonus(Now));
    }

    [TestMethod]
    public void Sight_ASurroundingsRide_RisesWithTheRamp_InBothMembers()
    {
        StartSurroundings(at: Now, skill: 150);

        Assert.AreEqual(0.25f, _sut.SightFactor(Now + 3d), 1e-5f);
        Assert.AreEqual(0.25f, _sut.SightCapBonus(Now + 3d), 1e-5f);
        Assert.AreEqual(0.5f, _sut.SightFactor(Now + 6d), 1e-5f);
        Assert.AreEqual(0.5f, _sut.SightFactor(Now + 60d), 1e-5f, "a ride past its end that nobody completed yet keeps the full ramp");
    }

    [TestMethod]
    public void Sight_ASettlementRide_LowersSightByThirtyFivePercent_ButNeverTheCap()
    {
        StartSettlement(at: Now);

        Assert.AreEqual(-0.35f, _sut.SightFactor(Now + 1d), 1e-6f);
        Assert.AreEqual(0f, _sut.SightCapBonus(Now + 1d));
    }

    [TestMethod]
    public void Sight_TheKeptBuff_HoldsThenFades()
    {
        _store.SetBuff(0.5f, Now, 24d);

        Assert.AreEqual(0.5f, _sut.SightFactor(Now + 24d), 1e-6f);
        Assert.AreEqual(0.25f, _sut.SightFactor(Now + 30d), 1e-5f);
        Assert.AreEqual(0.25f, _sut.SightCapBonus(Now + 30d), 1e-5f);
        Assert.AreEqual(0f, _sut.SightFactor(Now + 36d));
    }

    [TestMethod]
    public void Sight_ASettlementRideOverAKeptBuff_NetsTheTwo_AndTheCapKeepsTheBuff()
    {
        _store.SetBuff(0.5f, Now, 24d);
        StartSettlement(at: Now + 1d);

        Assert.AreEqual(0.15f, _sut.SightFactor(Now + 2d), 1e-5f);
        Assert.AreEqual(0.5f, _sut.SightCapBonus(Now + 2d), 1e-5f);
    }

    [TestMethod]
    public void Sight_ARampBelowTheBuff_TakesTheBuff_AndTheOtherWayAround()
    {
        _store.SetBuff(0.3f, Now, 24d);
        StartSurroundings(at: Now + 1d);
        Assert.AreEqual(0.3f, _sut.SightFactor(Now + 2d), 1e-5f, "ramp 0.083 under buff 0.3");

        Assert.AreEqual(0.5f, _sut.SightFactor(Now + 7d), 1e-5f, "ramp 0.5 over buff 0.3");
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(-1d)]
    [DataRow(double.PositiveInfinity)]
    public void Sight_NoUsableClock_IsZero(double now)
    {
        StartSettlement();
        _store.SetBuff(0.5f, 0d, 24d);

        Assert.AreEqual(0f, _sut.SightFactor(now));
        Assert.AreEqual(0f, _sut.SightCapBonus(now));
    }

    [TestMethod]
    public void Sight_AStoreHoldingNonsense_NeverHandsTheEngineANonFiniteNumber()
    {
        _store.SetBuff(float.NaN, Now, 24d);
        StartSurroundings();

        Assert.IsTrue(TAOM.Core.Validation.FiniteFloatValidator.IsFinite(_sut.SightFactor(Now + 1d)));
        Assert.IsTrue(TAOM.Core.Validation.FiniteFloatValidator.IsFinite(_sut.SightCapBonus(Now + 1d)));
    }

    // ---- The pick

    [TestMethod]
    public void PickSites_OffersTheNearestEightWithinFifty_NotTheClansOwn()
    {
        _world.When(w => w.CollectFiefs(Arg.Any<List<ScoutSite>>())).Do(call =>
        {
            var into = call.Arg<List<ScoutSite>>();
            into.Clear();
            into.Add(new ScoutSite { Id = "far", Name = "Far", Distance = 120f });
            into.Add(new ScoutSite { Id = "own", Name = "Own", Distance = 5f, OwnedByPlayerClan = true });
            into.Add(new ScoutSite { Id = "b", Name = "B", Distance = 30f });
            into.Add(new ScoutSite { Id = "a", Name = "A", Distance = 10f });
        });
        var picked = new List<ScoutSite>();

        _sut.PickSites(picked);

        CollectionAssert.AreEqual(new[] { "a", "b" }, picked.Select(s => s.Id).ToList());
    }
}
