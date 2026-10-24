using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.SwornAnew;
using TAOM.Features.SwornAnew.Models;
using TAOM.Features.UncapturableHeroes;
using static TAOM.Tests.Features.SwornAnew.DefectionFactsFixture;

namespace TAOM.Tests.Features.SwornAnew;

/// <summary>
/// The decision a captured lord is asked to join the player's kingdom rests on (docs/features/sworn-anew.md): a free hero is never
/// this feature's business; a prisoner repeats vanilla's own faction checks, then custody, then lore, then alignment. Sides are read
/// from the faction id: "k_free" is Free, "k_evil" is Evil, anything else is Neutral.
/// </summary>
[TestClass]
public class SwornAnewServiceTests
{
    private ISwornAnewSettingsProvider _settings = null!;
    private IAlignmentService _alignment = null!;
    private IUncapturableRegistry _registry = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _server = null!;
    private SwornAnewService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ISwornAnewSettingsProvider>();
        _settings.Enabled.Returns(true);
        _alignment = Substitute.For<IAlignmentService>();
        _alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(call => call.ArgAt<string>(0) switch
        {
            "k_free" => FactionSide.Free,
            "k_evil" => FactionSide.Evil,
            _ => FactionSide.Neutral,
        });
        _registry = Substitute.For<IUncapturableRegistry>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _server = Substitute.For<IDedicatedServerProvider>();
        _service = new SwornAnewService(_settings, _alignment, _registry, _coop, _server);
    }

    // --- not this feature's business --------------------------------------------------------------------------------------

    [TestMethod]
    public void CanPrisonerDefect_NoFacts_DefersToVanilla()
    {
        Assert.IsNull(_service.CanPrisonerDefect(null));
    }

    [TestMethod]
    public void CanPrisonerDefect_AFreeHero_DefersToVanilla_AndAsksNothing()
    {
        var result = _service.CanPrisonerDefect(EligiblePrisoner() with { IsPrisoner = false });

        Assert.IsNull(result, "a free hero is vanilla's own check");
        _registry.DidNotReceiveWithAnyArgs().IsUncapturable(default!, default);
        _alignment.DidNotReceiveWithAnyArgs().ResolveSide(default!, default!);
    }

    [TestMethod]
    public void CanPrisonerDefect_TheSwitchOff_DefersToVanilla()
    {
        _settings.Enabled.Returns(false);

        Assert.IsNull(_service.CanPrisonerDefect(EligiblePrisoner()));
    }

    [TestMethod]
    public void CanPrisonerDefect_ACoopPeerDeferringToTheHost_DefersToVanilla()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.IsNull(_service.CanPrisonerDefect(EligiblePrisoner()));
    }

    [TestMethod]
    public void CanPrisonerDefect_ADedicatedServer_DefersToVanilla()
    {
        _server.IsDedicatedServer.Returns(true);

        Assert.IsNull(_service.CanPrisonerDefect(EligiblePrisoner()));
    }

    // --- the happy path ---------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CanPrisonerDefect_AnEligiblePrisoner_IsTrue()
    {
        Assert.AreEqual(true, _service.CanPrisonerDefect(EligiblePrisoner()));
    }

    // --- vanilla's own faction checks (DefaultDefectionModel line 9) ------------------------------------------------------

    [TestMethod]
    public void CanPrisonerDefect_NotInAKingdom_IsFalse()
    {
        Assert.AreEqual(false, _service.CanPrisonerDefect(EligiblePrisoner() with { InKingdom = false }));
    }

    [TestMethod]
    public void CanPrisonerDefect_AlreadyInThePlayersFaction_IsFalse()
    {
        Assert.AreEqual(false, _service.CanPrisonerDefect(EligiblePrisoner() with { InPlayerFaction = true }));
    }

    [TestMethod]
    public void CanPrisonerDefect_TheRulerOfTheirKingdom_IsFalse()
    {
        Assert.AreEqual(false, _service.CanPrisonerDefect(EligiblePrisoner() with { IsFactionLeader = true }));
    }

    [TestMethod]
    public void CanPrisonerDefect_NoClan_IsFalse()
    {
        Assert.AreEqual(false, _service.CanPrisonerDefect(EligiblePrisoner() with { HasClan = false }));
    }

    [TestMethod]
    public void CanPrisonerDefect_AMinorFactionClan_IsFalse()
    {
        Assert.AreEqual(false, _service.CanPrisonerDefect(EligiblePrisoner() with { IsMinorFaction = true }));
    }

    [TestMethod]
    public void CanPrisonerDefect_AMercenaryClan_IsFalse()
    {
        Assert.AreEqual(false, _service.CanPrisonerDefect(EligiblePrisoner() with { IsMercenary = true }));
    }

    [TestMethod]
    public void CanPrisonerDefect_AClanWithoutAKingdom_IsFalse()
    {
        Assert.AreEqual(false, _service.CanPrisonerDefect(EligiblePrisoner() with { ClanHasKingdom = false }));
    }

    [TestMethod]
    public void CanPrisonerDefect_ABloodFeudWhenThePlayerClanRulesTheTarget_IsFalse()
    {
        var facts = EligiblePrisoner() with { HasBloodFeudWithPlayer = true, TargetRuledByPlayerClan = true };

        Assert.AreEqual(false, _service.CanPrisonerDefect(facts));
    }

    [TestMethod]
    public void CanPrisonerDefect_ABloodFeudWhenThePlayerClanDoesNotRuleTheTarget_StillPasses()
    {
        // Vanilla's line 9 applies the feud only to a kingdom the player's clan rules: a vassal player cannot make the feud decide.
        var facts = EligiblePrisoner() with { HasBloodFeudWithPlayer = true, TargetRuledByPlayerClan = false };

        Assert.AreEqual(true, _service.CanPrisonerDefect(facts));
    }

    [TestMethod]
    public void CanPrisonerDefect_NoFeudWhenThePlayerClanRulesTheTarget_Passes()
    {
        var facts = EligiblePrisoner() with { HasBloodFeudWithPlayer = false, TargetRuledByPlayerClan = true };

        Assert.AreEqual(true, _service.CanPrisonerDefect(facts));
    }

    [TestMethod]
    public void CanPrisonerDefect_AFailedFactionCheck_AsksNeitherLoreNorAlignment()
    {
        _service.CanPrisonerDefect(EligiblePrisoner() with { IsMinorFaction = true });

        _registry.DidNotReceiveWithAnyArgs().IsUncapturable(default!, default);
        _alignment.DidNotReceiveWithAnyArgs().ResolveSide(default!, default!);
    }

    // --- custody ----------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CanPrisonerDefect_HeldByThePlayersPartyOnly_IsTrue()
    {
        var facts = EligiblePrisoner() with { HeldByMainParty = true, InPlayerClanSettlement = false };

        Assert.AreEqual(true, _service.CanPrisonerDefect(facts));
    }

    [TestMethod]
    public void CanPrisonerDefect_InASettlementThePlayersClanOwnsOnly_IsTrue()
    {
        var facts = EligiblePrisoner() with { HeldByMainParty = false, InPlayerClanSettlement = true };

        Assert.AreEqual(true, _service.CanPrisonerDefect(facts));
    }

    [TestMethod]
    public void CanPrisonerDefect_HeldBySomeoneElse_IsFalse()
    {
        var facts = EligiblePrisoner() with { HeldByMainParty = false, InPlayerClanSettlement = false };

        Assert.AreEqual(false, _service.CanPrisonerDefect(facts));
    }

    // --- ransom screen ----------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CanPrisonerDefect_OnTheRansomScreen_IsFalse()
    {
        Assert.AreEqual(false, _service.CanPrisonerDefect(EligiblePrisoner() with { OnRansomScreen = true }));
    }

    [TestMethod]
    public void HidesPrisonerLine_OnTheRansomScreen_IsTrue()
    {
        Assert.IsTrue(_service.HidesPrisonerLine(EligiblePrisoner() with { OnRansomScreen = true }));
    }

    // --- lore -------------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CanPrisonerDefect_ALoreHeroTheRegistryNames_IsFalse()
    {
        // Sauron and the Nine are named by the uncapturable registry, by identity, whatever that feature's own switch says.
        _registry.IsUncapturable("lord_1_17", 4).Returns(true);

        var facts = EligiblePrisoner() with { HeroId = "lord_1_17", RaceId = 4 };

        Assert.AreEqual(false, _service.CanPrisonerDefect(facts));
    }

    [TestMethod]
    public void CanPrisonerDefect_AskedWithTheHeroIdAndRaceId()
    {
        _service.CanPrisonerDefect(EligiblePrisoner() with { HeroId = "lord_7", RaceId = 3 });

        _registry.Received(1).IsUncapturable("lord_7", 3);
    }

    [TestMethod]
    public void CanPrisonerDefect_AnUnreadableRace_IsAskedAsNull_NotAsHuman()
    {
        _service.CanPrisonerDefect(EligiblePrisoner() with { HeroId = "lord_7", RaceId = null });

        _registry.Received(1).IsUncapturable("lord_7", null);
    }

    [TestMethod]
    public void CanPrisonerDefect_AnOrdinaryLord_IsNotLoreLocked()
    {
        _registry.IsUncapturable(Arg.Any<string>(), Arg.Any<int?>()).Returns(false);

        Assert.AreEqual(true, _service.CanPrisonerDefect(EligiblePrisoner()));
    }

    // --- alignment --------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CanPrisonerDefect_AFreeLordForAnEvilPlayer_IsFalse()
    {
        Assert.AreEqual(false, _service.CanPrisonerDefect(EligiblePrisoner(lord: Free, player: Evil)));
    }

    [TestMethod]
    public void CanPrisonerDefect_AnEvilLordForAFreePlayer_IsFalse()
    {
        Assert.AreEqual(false, _service.CanPrisonerDefect(EligiblePrisoner(lord: Evil, player: Free)));
    }

    [TestMethod]
    public void CanPrisonerDefect_BothFree_IsTrue()
    {
        Assert.AreEqual(true, _service.CanPrisonerDefect(EligiblePrisoner(lord: Free, player: Free)));
    }

    [TestMethod]
    public void CanPrisonerDefect_BothEvil_IsTrue()
    {
        Assert.AreEqual(true, _service.CanPrisonerDefect(EligiblePrisoner(lord: Evil, player: Evil)));
    }

    [DataTestMethod]
    [DataRow("lord_free", "player_neutral")]
    [DataRow("lord_evil", "player_neutral")]
    [DataRow("lord_neutral", "player_free")]
    [DataRow("lord_neutral", "player_evil")]
    [DataRow("lord_neutral", "player_neutral")]
    public void CanPrisonerDefect_ANeutralSideOnEitherEnd_IsTrue(string lord, string player)
    {
        SideIds Pick(string key) => key.EndsWith("free") ? Free : key.EndsWith("evil") ? Evil : Unaligned;

        Assert.AreEqual(true, _service.CanPrisonerDefect(EligiblePrisoner(lord: Pick(lord), player: Pick(player))));
    }

    [TestMethod]
    public void CanPrisonerDefect_ResolvesEachSideFromItsOwnPair_NotFromAZippedOne()
    {
        // #957: the faction id and the culture of ONE entity travel together; the service never mixes them.
        var lord = new SideIds("k_lord", "c_lord");
        var player = new SideIds("k_player", "c_player");

        _service.CanPrisonerDefect(EligiblePrisoner(lord: lord, player: player));

        _alignment.Received(1).ResolveSide("k_lord", "c_lord");
        _alignment.Received(1).ResolveSide("k_player", "c_player");
    }

    // --- the postfix's question: should vanilla's prisoner line be taken away ------------------------------------------------

    [TestMethod]
    public void CanPrisonerDefect_ADeferringPeerAndARefusedPrisoner_IsNull_ButTheLineIsStillHidden()
    {
        // The model defers to the host on a co-op peer, but the line is only hidden locally: the veto holds on every peer.
        _coop.ShouldDeferToHost.Returns(true);
        var facts = EligiblePrisoner(lord: Free, player: Evil);

        Assert.IsNull(_service.CanPrisonerDefect(facts));
        Assert.IsTrue(_service.HidesPrisonerLine(facts));
    }

    [TestMethod]
    public void HidesPrisonerLine_ADedicatedServer_StillAnswersTheVerdict()
    {
        _server.IsDedicatedServer.Returns(true);

        Assert.IsTrue(_service.HidesPrisonerLine(EligiblePrisoner(lord: Free, player: Evil)));
        Assert.IsFalse(_service.HidesPrisonerLine(EligiblePrisoner()));
    }

    [TestMethod]
    public void HidesPrisonerLine_NoFacts_IsFalse()
    {
        Assert.IsFalse(_service.HidesPrisonerLine(null));
    }

    [TestMethod]
    public void HidesPrisonerLine_AFreeHero_IsFalse_AndAsksNothing()
    {
        Assert.IsFalse(_service.HidesPrisonerLine(EligiblePrisoner(lord: Free, player: Evil) with { IsPrisoner = false }));

        _registry.DidNotReceiveWithAnyArgs().IsUncapturable(default!, default);
        _alignment.DidNotReceiveWithAnyArgs().ResolveSide(default!, default!);
    }

    [TestMethod]
    public void HidesPrisonerLine_TheSwitchOff_IsFalse()
    {
        _settings.Enabled.Returns(false);

        Assert.IsFalse(_service.HidesPrisonerLine(EligiblePrisoner(lord: Free, player: Evil)));
    }

    [TestMethod]
    public void HidesPrisonerLine_AnEligiblePrisoner_IsFalse()
    {
        Assert.IsFalse(_service.HidesPrisonerLine(EligiblePrisoner()));
    }

    [TestMethod]
    public void HidesPrisonerLine_TheAlignmentGateRefuses_IsTrue()
    {
        Assert.IsTrue(_service.HidesPrisonerLine(EligiblePrisoner(lord: Evil, player: Free)));
    }

    [TestMethod]
    public void HidesPrisonerLine_AMercenaryClan_IsTrue()
    {
        // Vanilla's prisoner line offers a mercenary clan's lord a place, but joining raises JoinAsMercenary and never arms the
        // party-screen commit: the full verdict, not the alignment alone, decides what the line shows.
        Assert.IsTrue(_service.HidesPrisonerLine(EligiblePrisoner() with { IsMercenary = true }));
    }

    [TestMethod]
    public void HidesPrisonerLine_ALoreHero_IsTrue()
    {
        _registry.IsUncapturable("lord_1", 0).Returns(true);

        Assert.IsTrue(_service.HidesPrisonerLine(EligiblePrisoner()));
    }

    // --- committing the party screen --------------------------------------------------------------------------------------

    [TestMethod]
    public void CommitsPartyScreenAfter_ThePartnersClanDefectingToThePlayersKingdom_IsTrue()
    {
        Assert.IsTrue(_service.CommitsPartyScreenAfter(partnerDefectedToPlayersKingdom: true));
    }

    [TestMethod]
    public void CommitsPartyScreenAfter_AnythingElse_IsFalse()
    {
        Assert.IsFalse(_service.CommitsPartyScreenAfter(partnerDefectedToPlayersKingdom: false));
    }

    [TestMethod]
    public void CommitsPartyScreenAfter_TheSwitchOff_IsFalse()
    {
        _settings.Enabled.Returns(false);

        Assert.IsFalse(_service.CommitsPartyScreenAfter(partnerDefectedToPlayersKingdom: true));
    }

    [TestMethod]
    public void CommitsPartyScreenAfter_ACoopPeerDeferringToTheHost_IsFalse()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.IsFalse(_service.CommitsPartyScreenAfter(partnerDefectedToPlayersKingdom: true));
    }

    [TestMethod]
    public void CommitsPartyScreenAfter_ADedicatedServer_IsFalse()
    {
        _server.IsDedicatedServer.Returns(true);

        Assert.IsFalse(_service.CommitsPartyScreenAfter(partnerDefectedToPlayersKingdom: true));
    }
}
