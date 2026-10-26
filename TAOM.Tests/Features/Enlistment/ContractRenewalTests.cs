using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>#836: the end of the term, the once-per-term offer, and renew or leave.</summary>
[TestClass]
public class ContractRenewalTests
{
    // ---- the gates (pure) ----

    private static bool Offer(
        EnlistmentState state = EnlistmentState.EnlistedAttached, double now = 400, double? end = 365, double? stamp = null)
        => ContractRenewalPolicy.IsDue(state, now, end, stamp);

    [TestMethod]
    public void IsDue_TheTermHasEnded_True() => Assert.IsTrue(Offer());

    [TestMethod]
    public void IsDue_AtTheEndDay_True() => Assert.IsTrue(Offer(now: 365));

    [TestMethod]
    public void IsDue_BeforeTheEnd_False() => Assert.IsFalse(Offer(now: 364.9));

    [TestMethod]
    public void IsDue_NoEndDay_False() => Assert.IsFalse(Offer(end: null));

    [TestMethod]
    public void IsDue_AlreadyOfferedForThisEndDay_False() => Assert.IsFalse(Offer(stamp: 365));

    [TestMethod]
    public void IsDue_AGuardFromAnEarlierTerm_StillOffers() => Assert.IsTrue(Offer(end: 730, now: 731, stamp: 365));

    [DataTestMethod]
    [DataRow(EnlistmentState.EnlistedBattle)]
    [DataRow(EnlistmentState.CommanderUnavailable)]
    [DataRow(EnlistmentState.EnlistedPlayerCaptive)]
    [DataRow(EnlistmentState.NotEnlisted)]
    [DataRow(EnlistmentState.Discharging)]
    public void IsDue_NotAttached_False(EnlistmentState state) => Assert.IsFalse(Offer(state: state));

    [TestMethod]
    public void IsDue_NonFiniteNow_False() => Assert.IsFalse(Offer(now: double.NaN));

    // ---- the service ----

    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;
    private IEnlistmentContentStore _content = null!;
    private IEnlistmentContentConfigProvider _config = null!;
    private IEnlistmentConfigProvider _core = null!;
    private IServiceRewardService _rewards = null!;
    private IDischargeService _discharge = null!;
    private ICoopSessionProvider _coop = null!;
    private ICommanderLordAdapter _commander = null!;
    private IEncounterAdapter _encounter = null!;
    private IInquiryAdapter _inquiry = null!;
    private ContractRenewalService _sut = null!;
    private Action? _renew;
    private string? _optionAKey;
    private string? _optionBKey;
    private Action? _leave;
    private IReadOnlyDictionary<string, string>? _vars;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
        _content = new EnlistmentContentStore(_logger);
        _config = Substitute.For<IEnlistmentContentConfigProvider>();
        _config.GetConfig().Returns(EnlistmentContentConfigProvider.BuildDefaults());
        _core = Substitute.For<IEnlistmentConfigProvider>();
        _core.GetConfig().Returns(new EnlistmentCoreConfig());
        _rewards = Substitute.For<IServiceRewardService>();
        _rewards.PayBonusFromCommander(Arg.Any<int>(), Arg.Any<string>()).Returns(ci => ci.ArgAt<int>(0));
        _discharge = Substitute.For<IDischargeService>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _commander = Substitute.For<ICommanderLordAdapter>();
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_1", partyIsActive: true, name: "Boromir"));
        _encounter = Substitute.For<IEncounterAdapter>();
        _encounter.GetOwnership(Arg.Any<string>()).Returns(new EncounterOwnershipSnapshot(hasEncounter: false));
        _inquiry = Substitute.For<IInquiryAdapter>();
        _inquiry.When(i => i.ShowTwoOptionInquiry(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Action>(), Arg.Any<Action>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<string>()))
            .Do(ci =>
            {
                _optionAKey = ci.ArgAt<string>(4);
                _optionBKey = ci.ArgAt<string>(6);
                _leave = ci.ArgAt<Action>(8);
                _renew = ci.ArgAt<Action>(9);
                _vars = ci.ArgAt<IReadOnlyDictionary<string, string>>(12);
            });
        _sut = new ContractRenewalService(_store, _content, _config, _core, _rewards, _discharge, _coop, _commander, _encounter, _inquiry, _logger);

        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
        _store.Record.ContractEndDay = 365;
        _content.Record.Rank = ServiceRank.Sergeant;
    }

    private int Wage() => WagePolicy.DailyWageForRank((int)ServiceRank.Sergeant, EnlistmentContentConfigProvider.BuildDefaults().Progression.DailyWageByRank);

    [TestMethod]
    public void OfferIfDue_TheTermEnded_ShowsTheInquiryOnceAndSetsTheStamp()
    {
        Assert.IsTrue(_sut.OfferIfDue(400));

        _inquiry.ReceivedWithAnyArgs(1).ShowTwoOptionInquiry(
            default!, default!, default!, default!, default!, default!, default!, default!, default!, default!);
        Assert.AreEqual("Boromir", _vars!["COMMANDER"]);
        Assert.AreEqual((30 * Wage()).ToString(), _vars["BONUS"]);

        Assert.IsFalse(_sut.OfferIfDue(401), "one offer per term (the daily tick can run twice in a frame)");
        _inquiry.ReceivedWithAnyArgs(1).ShowTwoOptionInquiry(
            default!, default!, default!, default!, default!, default!, default!, default!, default!, default!);
    }

    [TestMethod]
    public void OfferIfDue_NoAuthority_NeverOffers()
    {
        _coop.IsAuthority.Returns(false);

        Assert.IsFalse(_sut.OfferIfDue(400));
        _coop.IsAuthority.Returns(true);
        Assert.IsTrue(_sut.OfferIfDue(400), "a refused call leaves no guard behind");
    }

    [TestMethod]
    public void OfferIfDue_CommanderInAMapEvent_WaitsForTheNextDay()
    {
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, partyId: "lord_party_1", partyIsActive: true, partyIsInMapEvent: true, name: "Boromir"));

        Assert.IsFalse(_sut.OfferIfDue(400));
    }

    [TestMethod]
    public void OfferIfDue_InAConversation_Waits()
    {
        _encounter.GetOwnership(Arg.Any<string>()).Returns(new EncounterOwnershipSnapshot(hasEncounter: true, conversationInProgress: true));

        Assert.IsFalse(_sut.OfferIfDue(400));
    }

    [TestMethod]
    public void OfferIfDue_PlayerInMapEvent_Waits()
    {
        _encounter.GetOwnership(Arg.Any<string>()).Returns(new EncounterOwnershipSnapshot(hasEncounter: false, playerInMapEvent: true));

        Assert.IsFalse(_sut.OfferIfDue(400));
    }

    [TestMethod]
    public void OfferIfDue_OwnershipReadFailed_Waits()
    {
        _encounter.GetOwnership(Arg.Any<string>()).Returns(new EncounterOwnershipSnapshot(hasEncounter: false, readFailed: true));

        Assert.IsFalse(_sut.OfferIfDue(400));
    }

    [DataTestMethod]
    [DataRow(false, true, false, DisplayName = "missing commander")]
    [DataRow(true, false, false, DisplayName = "dead commander")]
    [DataRow(true, true, true, DisplayName = "captured commander")]
    public void OfferIfDue_CommanderCannotHoldAnOffer_Waits(bool exists, bool alive, bool prisoner)
    {
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: exists, isAlive: alive, isPrisoner: prisoner, partyId: "lord_party_1", partyIsActive: true, name: "Boromir"));

        Assert.IsFalse(_sut.OfferIfDue(400));
    }

    [TestMethod]
    public void ResetForNewSession_ForgetsTheOfferGuard()
    {
        _sut.OfferIfDue(400);
        Assert.IsFalse(_sut.OfferIfDue(400));

        _sut.ResetForNewSession();

        Assert.IsTrue(_sut.OfferIfDue(400));
    }

    [TestMethod]
    public void ForceTermEnd_SetsTheEndDayToNow_AndTheNextTickOffers()
    {
        _store.Record.ContractEndDay = 900;
        Assert.IsFalse(_sut.OfferIfDue(400));

        Assert.IsTrue(_sut.ForceTermEnd(400));

        Assert.AreEqual(400.0, _store.Record.ContractEndDay);
        Assert.IsTrue(_sut.OfferIfDue(400));
    }

    [TestMethod]
    public void ForceTermEnd_NotEnlistedOrNotTheHost_ChangesNothing()
    {
        _coop.IsAuthority.Returns(false);
        Assert.IsFalse(_sut.ForceTermEnd(400));
        _coop.IsAuthority.Returns(true);
        _store.Record.State = EnlistmentState.NotEnlisted;
        Assert.IsFalse(_sut.ForceTermEnd(400));
        Assert.AreEqual(365.0, _store.Record.ContractEndDay);
    }

    [TestMethod]
    public void Renew_ThenTheSameDayTicksAgain_DoesNotOffer()
    {
        _sut.OfferIfDue(400);
        _renew!();

        Assert.IsFalse(_sut.OfferIfDue(400));
    }

    [TestMethod]
    public void TheConsoleCommand_IsRegisteredThroughTaomConsole()
    {
        var src = RepoPaths.ReadSource("Main/Features/DevConsole/Cheats/EnlistmentTermCheats.cs", stripComments: true);
        StringAssert.Contains(src, "CommandLineArgumentFunction(\"set_term_end\", \"taom\")");
        StringAssert.Contains(src, "TaomConsole.RunInCampaign");
        StringAssert.Contains(src, "ForceTermEnd");
    }

    [TestMethod]
    public void OfferIfDue_ASettlementEncounterAlone_DoesNotBlock()
    {
        _encounter.GetOwnership(Arg.Any<string>()).Returns(new EncounterOwnershipSnapshot(hasEncounter: true, playerInsideSettlement: true));

        Assert.IsTrue(_sut.OfferIfDue(400));
    }

    [TestMethod]
    public void Renew_PaysTheBonus_AddsTrust_RestartsTheTerm_AndStaysEnlisted()
    {
        _sut.OfferIfDue(400);

        _renew!();

        _rewards.Received(1).PayBonusFromCommander(30 * Wage(), "contract-renewal");
        _rewards.Received(1).Grant(Arg.Is<RewardSpec>(r => r.Gold == 0 && r.Trust == 10), "contract-renewal");
        _inquiry.Received(1).ShowMessage("taom_enlist_term_bonus_paid", Arg.Any<string>(), "GOLD", (30 * Wage()).ToString());
        Assert.AreEqual(400 + 365.0, _store.Record.ContractEndDay, "the oath's own expression: now + ContractDays");
        Assert.AreEqual(EnlistmentState.EnlistedAttached, _store.Record.State);
        _discharge.DidNotReceiveWithAnyArgs().Execute(default);
    }

    [TestMethod]
    public void Renew_NothingDelivered_ShowsNoBonusNotice()
    {
        _rewards.PayBonusFromCommander(Arg.Any<int>(), Arg.Any<string>()).Returns(0);
        _sut.OfferIfDue(400);

        _renew!();

        _inquiry.DidNotReceiveWithAnyArgs().ShowMessage(default!, default!, default!, default!);
    }

    [TestMethod]
    public void TheEscapeOption_IsRenew_AndLeaveIsTheFirstButton()
    {
        // The engine runs option B on Escape and the gamepad B button, so the safe choice (stay in
        // service) is B and leaving is A.
        _sut.OfferIfDue(400);

        Assert.AreEqual("taom_enlist_term_leave", _optionAKey);
        Assert.AreEqual("taom_enlist_term_renew", _optionBKey);
    }

    [TestMethod]
    public void Leave_NotAttachedAnyMore_DoesNothing_AndTheOfferReturnsNextTick()
    {
        // A battle can start in the same frame after the offer shows.
        _sut.OfferIfDue(400);
        _store.Record.State = EnlistmentState.EnlistedBattle;

        _leave!();

        _discharge.DidNotReceiveWithAnyArgs().Execute(default);
        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("leave") && m.Contains("not attached")));
        _store.Record.State = EnlistmentState.EnlistedAttached;
        Assert.IsTrue(_sut.OfferIfDue(401));
    }

    [TestMethod]
    public void OfferIfDue_BeforeTheEnd_ReadsNoAdapter()
    {
        Assert.IsFalse(_sut.OfferIfDue(100));

        _commander.DidNotReceiveWithAnyArgs().GetSnapshot(default!);
        _encounter.DidNotReceiveWithAnyArgs().GetOwnership(default);
    }

    [TestMethod]
    public void OfferIfDue_AlreadyOffered_ReadsNoAdapter()
    {
        _sut.OfferIfDue(400);
        _commander.ClearReceivedCalls();
        _encounter.ClearReceivedCalls();

        Assert.IsFalse(_sut.OfferIfDue(401));

        _commander.DidNotReceiveWithAnyArgs().GetSnapshot(default!);
        _encounter.DidNotReceiveWithAnyArgs().GetOwnership(default);
    }

    [TestMethod]
    public void IsDue_TheCheapGate()
    {
        Assert.IsTrue(ContractRenewalPolicy.IsDue(EnlistmentState.EnlistedAttached, 400, 365, null));
        Assert.IsFalse(ContractRenewalPolicy.IsDue(EnlistmentState.EnlistedAttached, 300, 365, null));
        Assert.IsFalse(ContractRenewalPolicy.IsDue(EnlistmentState.EnlistedAttached, 400, 365, 365));
        Assert.IsFalse(ContractRenewalPolicy.IsDue(EnlistmentState.EnlistedBattle, 400, 365, null));
        Assert.IsFalse(ContractRenewalPolicy.IsDue(EnlistmentState.EnlistedAttached, 400, null, null));
    }

    [TestMethod]
    public void Renew_ThenTheNextTermEnds_OffersAgain()
    {
        _sut.OfferIfDue(400);
        _renew!();

        Assert.IsTrue(_sut.OfferIfDue(400 + 365.0 + 1));
    }

    [TestMethod]
    public void Leave_DischargesWithContractNotRenewed()
    {
        _sut.OfferIfDue(400);

        _leave!();

        _discharge.Received(1).Execute(DischargeReason.ContractNotRenewed);
        _rewards.DidNotReceiveWithAnyArgs().Grant(default!, default!);
        _rewards.DidNotReceiveWithAnyArgs().PayBonusFromCommander(default, default!);
    }

    [TestMethod]
    public void TheChoice_AfterTheAuthorityIsGone_DoesNothing()
    {
        _sut.OfferIfDue(400);
        _coop.IsAuthority.Returns(false);

        _renew!();
        _leave!();

        _rewards.DidNotReceiveWithAnyArgs().Grant(default!, default!);
        _discharge.DidNotReceiveWithAnyArgs().Execute(default);
    }

    [TestMethod]
    public void TheChoice_AfterTheTermEndedByOtherMeans_DoesNothing()
    {
        _sut.OfferIfDue(400);
        _store.Record.State = EnlistmentState.NotEnlisted;

        _renew!();

        _rewards.DidNotReceiveWithAnyArgs().Grant(default!, default!);
    }

    // ---- source shapes ----

    [TestMethod]
    public void TheBehavior_OffersFromTheDailyTick_AndTheOldToastIsReplaced()
    {
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/Hooks/EnlistmentContentBehavior.cs", stripComments: true);
        StringAssert.Contains(src, "OfferIfDue");
        Assert.IsFalse(src.Contains("taom_enlist_term_served"), "the offer replaces the old toast");
    }

    [TestMethod]
    public void TheOffer_UsesTheNewKeys()
    {
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/Content/ContractRenewalService.cs", stripComments: true);
        foreach (var key in new[] { "taom_enlist_term_title", "taom_enlist_term_body", "taom_enlist_term_renew", "taom_enlist_term_leave", "taom_enlist_term_bonus_paid" })
            StringAssert.Contains(src, key);
    }

    [TestMethod]
    public void TheOldTermServedToast_IsRetiredEverywhere()
    {
        Assert.IsFalse(RepoPaths.ReadSource("Main/_Module/ModuleData/taom_enlistment_strings.xml").Contains("taom_enlist_term_served"));
        var behavior = RepoPaths.ReadSource("Main/Features/Enlistment/Hooks/EnlistmentContentBehavior.cs");
        Assert.IsFalse(behavior.Contains("taom_enlist_term_served"));
        foreach (var cache in System.IO.Directory.GetFiles(RepoPaths.RepoPath("tools", "translation_cache"), "*.json"))
            Assert.IsFalse(System.IO.File.ReadAllText(cache).Contains("taom_enlist_term_served"), cache);
        foreach (var file in System.IO.Directory.GetFiles(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"),
                     "std_taom_enlistment_strings_*.xml", System.IO.SearchOption.AllDirectories))
        {
            Assert.IsFalse(System.IO.File.ReadAllText(file).Contains("taom_enlist_term_served"), file);
        }
    }

    [TestMethod]
    public void TheMaintenanceReset_ResetsTheOfferGuard()
    {
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/ServiceMaintenanceService.cs", stripComments: true);
        StringAssert.Contains(src, "_renewal?.ResetForNewSession()");
    }
}
