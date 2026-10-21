using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Execution;
using TAOM.Features.GoodDeeds;
using TAOM.Features.GoodDeeds.Models;
using TAOM.Features.Relations;
using static TAOM.Tests.Features.GoodDeeds.GoodDeedsFixture;

namespace TAOM.Tests.Features.GoodDeeds;

/// <summary>
/// Releasing a captured lord: the three grants, their scaling by the side of the lord against the player's, the guards that
/// ignore every other release, and the de-duplication per relation owner.
/// </summary>
[TestClass]
public class GoodDeedsServiceReleaseTests
{
    private GoodDeedsFixture _f = null!;

    [TestInitialize]
    public void Setup() => _f = new GoodDeedsFixture();

    private void Facts(ReleaseFacts facts) => _f.Heroes.ReadRelease("lord").Returns(facts);

    private static readonly ReleaseSource FromMain = new(true, false, false);

    private int Released() => _f.Service.OnPrisonerReleased("lord", true, FromMain);

    // --- the amounts and the multipliers -------------------------------------------------------------------------------

    [TestMethod]
    public void OnPrisonerReleased_SameSide_GrantsFiveTwoAndTwoToThreeOwners()
    {
        Facts(Release(Free, Free, Hero("lord"), Hero("wife"), Hero("leader")));

        var landed = Released();

        CollectionAssert.AreEqual(new[] { ("lord", 5), ("wife", 2), ("leader", 2) }, _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
        Assert.AreEqual(3, landed);
    }

    [TestMethod]
    public void OnPrisonerReleased_NeutralPairing_ScalesByThreeQuartersRoundingAwayFromZero()
    {
        Facts(Release(Free, Unaligned, Hero("lord"), Hero("wife"), Hero("leader")));

        Released();

        // 5 * 0.75 = 3.75 -> 4; 2 * 0.75 = 1.5 -> 2.
        CollectionAssert.AreEqual(new[] { ("lord", 4), ("wife", 2), ("leader", 2) }, _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
    }

    [TestMethod]
    public void OnPrisonerReleased_OppositeSides_ScalesByHalfRoundingAwayFromZero()
    {
        Facts(Release(Free, Evil, Hero("lord"), Hero("wife"), Hero("leader")));

        Released();

        // 5 * 0.5 = 2.5 -> 3; 2 * 0.5 = 1.
        CollectionAssert.AreEqual(new[] { ("lord", 3), ("wife", 1), ("leader", 1) }, _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
    }

    [DataTestMethod]
    [DataRow("k_free", "k_free", 5)]
    [DataRow("k_evil", "k_evil", 5)]
    [DataRow("k_free", "k_evil", 3)]
    [DataRow("k_evil", "k_free", 3)]
    [DataRow("k_free", "k_other", 4)]
    [DataRow("k_other", "k_evil", 4)]
    [DataRow("k_other", "k_other", 4)]
    [DataRow("", "k_free", 4)]
    [DataRow("k_free", "", 4)]
    [DataRow("", "", 4)]
    public void OnPrisonerReleased_TheLordsGrant_FollowsThePairingOfPlayerAndLord(string playerKingdom, string lordKingdom, int expected)
    {
        Facts(Release(new SideIds(playerKingdom, string.Empty), new SideIds(lordKingdom, string.Empty)));

        Released();

        Assert.AreEqual(expected, _f.Grants.Single().Delta);
    }

    [TestMethod]
    public void OnPrisonerReleased_ALordWithNoFactionAtAll_IsNeutral()
    {
        Facts(Release(Free, SideIds.None));

        Released();

        Assert.AreEqual(4, _f.Grants.Single().Delta);
    }

    [TestMethod]
    public void OnPrisonerReleased_AskedForTheSidesOfTheSameEntities_PassesKingdomAndCultureTogether()
    {
        Facts(Release(Free, Evil));

        Released();

        _f.Alignment.Received().ResolveSide("k_free", "c_free");
        _f.Alignment.Received().ResolveSide("k_evil", "c_evil");
    }

    [TestMethod]
    public void OnPrisonerReleased_TheSettingsAmountsAndMultipliers_AreUsed()
    {
        _f.Configure(Defaults with
        {
            ReleaseLordAmount = 10,
            ReleaseSpouseAmount = 6,
            ReleaseLeaderAmount = 4,
            Release = new SideMultipliers(2f, 1f, 1f),
        });
        Facts(Release(Free, Free, Hero("lord"), Hero("wife"), Hero("leader")));

        Released();

        CollectionAssert.AreEqual(new[] { ("lord", 20), ("wife", 12), ("leader", 8) }, _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
    }

    [TestMethod]
    public void OnPrisonerReleased_AMultiplierOfZero_GrantsNothing()
    {
        _f.Configure(Defaults with { Release = new SideMultipliers(1f, 1f, 0f) });
        Facts(Release(Free, Evil, Hero("lord"), Hero("wife"), Hero("leader")));

        var landed = Released();

        Assert.AreEqual(0, _f.Grants.Count);
        Assert.AreEqual(0, landed);
    }

    [TestMethod]
    public void OnPrisonerReleased_AnAmountOfZero_IsNotGranted()
    {
        _f.Configure(Defaults with { ReleaseSpouseAmount = 0, ReleaseLeaderAmount = 0 });
        Facts(Release(Free, Free, Hero("lord"), Hero("wife"), Hero("leader")));

        Released();

        Assert.AreEqual("lord", _f.Grants.Single().HeroId);
    }

    [TestMethod]
    public void OnPrisonerReleased_NamesTheDeedInTheLogReason()
    {
        Facts(Release(Free, Free));

        Released();

        StringAssert.Contains(_f.Grants.Single().Reason, "release");
    }

    // --- the landed result -----------------------------------------------------------------------------------------------

    [TestMethod]
    public void OnPrisonerReleased_ReturnsTheNumberOfGrantsThatLanded()
    {
        _f.GrantService.Grant("wife", Arg.Any<int>(), Arg.Any<string>()).Returns(0);
        Facts(Release(Free, Free, Hero("lord"), Hero("wife"), Hero("leader")));

        Assert.AreEqual(2, Released());
    }

    [TestMethod]
    public void OnPrisonerReleased_NothingLanded_ReturnsZero()
    {
        _f.GrantService.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(0);
        Facts(Release(Free, Free, Hero("lord"), Hero("wife"), Hero("leader")));

        Assert.AreEqual(0, Released());
    }

    // --- de-duplication per relation owner -------------------------------------------------------------------------------

    [TestMethod]
    public void OnPrisonerReleased_LordAndLeaderShareAnOwner_OnlyTheLordsFiveIsGranted()
    {
        // Personal Relations off: both relations land on the clan leader, so 5 and 2 would be 7 for one man.
        Facts(Release(Free, Free, Hero("lord", "leader"), null, Hero("leader", "leader")));

        var landed = Released();

        Assert.AreEqual(("lord", 5), (_f.Grants.Single().HeroId, _f.Grants.Single().Delta));
        Assert.AreEqual(1, landed);
    }

    [TestMethod]
    public void OnPrisonerReleased_TheLordIsHisOwnClansLeader_IsOneGrant()
    {
        Facts(Release(Free, Free, Hero("lord"), null, Hero("lord")));

        Released();

        Assert.AreEqual(1, _f.Grants.Count);
        Assert.AreEqual(5, _f.Grants[0].Delta);
    }

    [TestMethod]
    public void OnPrisonerReleased_LordAndLeaderAreDifferentOwners_BothAreGranted()
    {
        // Personal Relations on: the lord keeps his own relation, and the leader keeps his.
        Facts(Release(Free, Free, Hero("lord"), null, Hero("leader")));

        Released();

        CollectionAssert.AreEqual(new[] { ("lord", 5), ("leader", 2) }, _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
    }

    [TestMethod]
    public void OnPrisonerReleased_TheSpouseSharesTheLordsOwner_OnlyTheLordsFiveIsGranted()
    {
        Facts(Release(Free, Free, Hero("lord", "leader"), Hero("wife", "leader"), Hero("leader", "leader")));

        Released();

        Assert.AreEqual(("lord", 5), (_f.Grants.Single().HeroId, _f.Grants.Single().Delta));
    }

    [TestMethod]
    public void OnPrisonerReleased_SpouseAndLeaderShareAnOwner_TheStrongerGrantWins()
    {
        _f.Configure(Defaults with { ReleaseSpouseAmount = 2, ReleaseLeaderAmount = 8 });
        Facts(Release(Free, Free, Hero("lord"), Hero("wife", "boss"), Hero("leader", "boss")));

        Released();

        CollectionAssert.AreEqual(new[] { ("lord", 5), ("leader", 8) }, _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
    }

    [TestMethod]
    public void OnPrisonerReleased_SpouseAndLeaderShareAnOwnerAndTie_TheSpouseListedFirstIsKept()
    {
        Facts(Release(Free, Free, Hero("lord"), Hero("wife", "boss"), Hero("leader", "boss")));

        Released();

        CollectionAssert.AreEqual(new[] { ("lord", 5), ("wife", 2) }, _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
    }

    [TestMethod]
    public void OnPrisonerReleased_ALargerLeaderAmountThanTheLords_TheLeaderWinsTheSharedOwner()
    {
        _f.Configure(Defaults with { ReleaseLordAmount = 1, ReleaseLeaderAmount = 9 });
        Facts(Release(Free, Free, Hero("lord", "leader"), null, Hero("leader", "leader")));

        Released();

        Assert.AreEqual(("leader", 9), (_f.Grants.Single().HeroId, _f.Grants.Single().Delta));
    }

    // --- the spouse and the clan ------------------------------------------------------------------------------------------

    [TestMethod]
    public void OnPrisonerReleased_NoSpouse_SkipsTheSpouseGrant()
    {
        Facts(Release(Free, Free, Hero("lord"), null, Hero("leader")));

        Released();

        CollectionAssert.DoesNotContain(_f.Grants.Select(g => g.HeroId).ToArray(), "wife");
        Assert.AreEqual(2, _f.Grants.Count);
    }

    [TestMethod]
    public void OnPrisonerReleased_ADeadSpouse_SkipsTheSpouseGrant()
    {
        Facts(Release(Free, Free, Hero("lord"), Hero("wife", alive: false), Hero("leader")));

        Released();

        CollectionAssert.AreEqual(new[] { "lord", "leader" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    [TestMethod]
    public void OnPrisonerReleased_ALivingSpouse_IsGrantedTwo()
    {
        Facts(Release(Free, Free, Hero("lord"), Hero("wife"), null));

        Released();

        CollectionAssert.AreEqual(new[] { ("lord", 5), ("wife", 2) }, _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
    }

    [TestMethod]
    public void OnPrisonerReleased_TheSpouseIsThePlayer_SkipsTheSpouseGrant()
    {
        Facts(Release(Free, Free, Hero("lord"), Hero("me", isPlayer: true), Hero("leader")));

        Released();

        CollectionAssert.AreEqual(new[] { "lord", "leader" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    [TestMethod]
    public void OnPrisonerReleased_ALordWithNoClan_GrantsOnlyTheLordAndHisSpouse()
    {
        Facts(Release(Free, SideIds.None, Hero("lord"), Hero("wife"), null));

        Released();

        CollectionAssert.AreEqual(new[] { "lord", "wife" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    [TestMethod]
    public void OnPrisonerReleased_ADeadClanLeader_SkipsTheLeaderGrant()
    {
        Facts(Release(Free, Free, Hero("lord"), null, Hero("leader", alive: false)));

        Released();

        Assert.AreEqual("lord", _f.Grants.Single().HeroId);
    }

    [TestMethod]
    public void OnPrisonerReleased_TheClanLeaderIsThePlayer_SkipsTheLeaderGrant()
    {
        Facts(Release(Free, Free, Hero("lord"), null, Hero("me", isPlayer: true)));

        Released();

        Assert.AreEqual("lord", _f.Grants.Single().HeroId);
    }

    [TestMethod]
    public void OnPrisonerReleased_TheLordIsDead_IsNotGranted()
    {
        Facts(Release(Free, Free, Hero("lord", alive: false), Hero("wife"), Hero("leader")));

        Released();

        CollectionAssert.AreEqual(new[] { "wife", "leader" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    [TestMethod]
    public void OnPrisonerReleased_ThePrisonerIsNotALord_GrantsNothing()
    {
        Facts(Release(Free, Free, Hero("lord"), Hero("wife"), Hero("leader"), isLord: false));

        Assert.AreEqual(0, Released());
        Assert.AreEqual(0, _f.Grants.Count);
    }

    // --- the releases that are ignored ------------------------------------------------------------------------------------

    [TestMethod]
    public void OnPrisonerReleased_NotReleasedByChoice_IsIgnoredWithoutReadingAnything()
    {
        // A ransom, a peace and an escape all arrive with detail other than ReleasedByChoice.
        Facts(Release(Free, Free));

        var landed = _f.Service.OnPrisonerReleased("lord", false, FromMain);

        Assert.AreEqual(0, landed);
        Assert.AreEqual(0, _f.Grants.Count);
        _f.Heroes.DidNotReceive().ReadRelease(Arg.Any<string>());
    }

    [TestMethod]
    public void OnPrisonerReleased_ReleasedByAnAiParty_IsIgnoredWithoutReadingAnything()
    {
        Facts(Release(Free, Free));

        var landed = _f.Service.OnPrisonerReleased("lord", true, ReleaseSource.None);

        Assert.AreEqual(0, landed);
        Assert.AreEqual(0, _f.Grants.Count);
        _f.Heroes.DidNotReceive().ReadRelease(Arg.Any<string>());
    }

    // The three releases that are the player's: from the player's own party (a conversation), from the party screen (the event
    // arrives with no party while the player's own party screen is open), and the "let go" talk in a dungeon the player's clan
    // owns. The adapter reads each as a raw fact; whether it pays is decided here. A release by a player-clan mobile party other
    // than the main party, an AI party, or a null party with no player screen open is none of the three.
    [DataTestMethod]
    [DataRow(true, false, false, true, DisplayName = "the main party")]
    [DataRow(false, true, false, true, DisplayName = "no party, the player's party screen open with the main party as owner")]
    [DataRow(false, false, true, true, DisplayName = "a settlement the player's clan owns")]
    [DataRow(false, false, false, false, DisplayName = "no party and no player screen, another screen owner, an AI clan's settlement or a player-clan mobile party")]
    public void OnPrisonerReleased_PaysOnlyWhenOneOfTheThreeSourceFactsIsTrue(bool mainParty, bool partyScreen, bool dungeon, bool pays)
    {
        Facts(Release(Free, Free));

        var landed = _f.Service.OnPrisonerReleased("lord", true, new ReleaseSource(mainParty, partyScreen, dungeon));

        Assert.AreEqual(pays ? 1 : 0, landed);
        Assert.AreEqual(pays ? 1 : 0, _f.Grants.Count);
        if (!pays)
            _f.Heroes.DidNotReceive().ReadRelease(Arg.Any<string>());
    }

    [TestMethod]
    public void OnPrisonerReleased_ANotByChoiceDetail_NeverPaysWhateverTheSource()
    {
        Facts(Release(Free, Free));

        var landed = _f.Service.OnPrisonerReleased("lord", false, new ReleaseSource(true, true, true));

        Assert.AreEqual(0, landed);
        _f.Heroes.DidNotReceive().ReadRelease(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void OnPrisonerReleased_ABlankPrisonerId_IsIgnored(string? id)
    {
        var landed = _f.Service.OnPrisonerReleased(id, true, FromMain);

        Assert.AreEqual(0, landed);
        _f.Heroes.DidNotReceive().ReadRelease(Arg.Any<string>());
    }

    [TestMethod]
    public void OnPrisonerReleased_TheHeroCannotBeRead_GrantsNothing()
    {
        _f.Heroes.ReadRelease("lord").Returns((ReleaseFacts?)null);

        Assert.AreEqual(0, Released());
        Assert.AreEqual(0, _f.Grants.Count);
    }

    [TestMethod]
    public void OnPrisonerReleased_TheMasterSwitchIsOff_IsIgnoredWithoutReadingAnything()
    {
        _f.Configure(Defaults with { Enabled = false });
        Facts(Release(Free, Free));

        Assert.AreEqual(0, Released());
        _f.Heroes.DidNotReceive().ReadRelease(Arg.Any<string>());
    }

    [TestMethod]
    public void OnPrisonerReleased_TheReleaseSwitchIsOff_IsIgnoredWithoutReadingAnything()
    {
        _f.Configure(Defaults with { ReleaseEnabled = false });
        Facts(Release(Free, Free));

        Assert.AreEqual(0, Released());
        _f.Heroes.DidNotReceive().ReadRelease(Arg.Any<string>());
    }

    [TestMethod]
    public void OnPrisonerReleased_OtherSwitchesOff_DoNotStopARelease()
    {
        _f.Configure(Defaults with { DonationEnabled = false, TournamentEnabled = false });
        Facts(Release(Free, Free));

        Assert.AreEqual(1, Released());
    }

    // --- co-op and the dedicated server ------------------------------------------------------------------------------------

    [TestMethod]
    public void OnPrisonerReleased_ACoopPeerThatDefersToTheHost_GrantsNothing()
    {
        _f.Coop.ShouldDeferToHost.Returns(true);
        Facts(Release(Free, Free));

        Assert.AreEqual(0, Released());
        Assert.AreEqual(0, _f.Grants.Count);
        _f.Heroes.DidNotReceive().ReadRelease(Arg.Any<string>());
    }

    [TestMethod]
    public void OnPrisonerReleased_ADedicatedServer_GrantsNothing()
    {
        _f.Server.IsDedicatedServer.Returns(true);
        Facts(Release(Free, Free));

        Assert.AreEqual(0, Released());
        Assert.AreEqual(0, _f.Grants.Count);
        _f.Heroes.DidNotReceive().ReadRelease(Arg.Any<string>());
    }

    [TestMethod]
    public void OnPrisonerReleased_TheHostOfASoloSession_GrantsAsUsual()
    {
        _f.Coop.ShouldDeferToHost.Returns(false);
        _f.Coop.IsAuthority.Returns(true);
        Facts(Release(Free, Free));

        Assert.AreEqual(1, Released());
    }
}
