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
/// Winning a town's tournament: relation with the owner's clan leader (not when the player owns the town) and with every living
/// notable, scaled by each one's own side, de-duplicated per relation owner, and one green line only when something landed.
/// </summary>
[TestClass]
public class GoodDeedsServiceTournamentTests
{
    private GoodDeedsFixture _f = null!;

    [TestInitialize]
    public void Setup() => _f = new GoodDeedsFixture();

    private static DeedNotable Notable(string id, SideIds? side = null, string? owner = null, bool alive = true) =>
        new(Hero(id, owner, alive), side ?? Free);

    private void Town(RelationTarget? owner, bool playerOwns, SideIds ownerSide, params DeedNotable[] notables) =>
        _f.Heroes.ReadTournament("town_1").Returns(new TournamentFacts("Minas Tirith", Free, playerOwns, owner, ownerSide, notables));

    private int Won() => _f.Service.OnTournamentFinished(true, "town_1");

    [TestMethod]
    public void OnTournamentFinished_PlayerWins_GrantsOneToTheOwnersLeaderAndEveryNotable()
    {
        Town(Hero("owner"), false, Free, Notable("n1"), Notable("n2"), Notable("n3"));

        var landed = Won();

        CollectionAssert.AreEqual(new[] { ("owner", 1), ("n1", 1), ("n2", 1), ("n3", 1) },
            _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
        Assert.AreEqual(4, landed);
    }

    [TestMethod]
    public void OnTournamentFinished_ThePlayersClanOwnsTheTown_SkipsTheOwnerButGrantsTheNotables()
    {
        Town(Hero("me", isPlayer: true), true, Free, Notable("n1"), Notable("n2"));

        Won();

        CollectionAssert.AreEqual(new[] { "n1", "n2" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    [TestMethod]
    public void OnTournamentFinished_ThePlayersClanOwnsTheTownWithAnotherLeader_StillSkipsTheOwner()
    {
        // The skip is the owning clan being the player's, not who leads it.
        Town(Hero("regent"), true, Free, Notable("n1"));

        Won();

        CollectionAssert.AreEqual(new[] { "n1" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    [TestMethod]
    public void OnTournamentFinished_TheOwnerHasNoLeader_GrantsTheNotablesOnly()
    {
        Town(null, false, Free, Notable("n1"));

        Won();

        CollectionAssert.AreEqual(new[] { "n1" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    [TestMethod]
    public void OnTournamentFinished_ADeadOwnerLeaderOrNotable_IsSkipped()
    {
        Town(Hero("owner", alive: false), false, Free, Notable("n1", alive: false), Notable("n2"));

        Won();

        CollectionAssert.AreEqual(new[] { "n2" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    [TestMethod]
    public void OnTournamentFinished_ANotableWhoIsThePlayer_IsSkipped()
    {
        Town(Hero("owner"), false, Free, new DeedNotable(Hero("me", isPlayer: true), Free), Notable("n1"));

        Won();

        CollectionAssert.AreEqual(new[] { "owner", "n1" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    [TestMethod]
    public void OnTournamentFinished_ATownWithNoNotables_GrantsTheOwnerOnly()
    {
        Town(Hero("owner"), false, Free);

        Won();

        CollectionAssert.AreEqual(new[] { "owner" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    // --- scaling by side ------------------------------------------------------------------------------------------------

    [TestMethod]
    public void OnTournamentFinished_AnOwnerOfTheOppositeSide_IsSkippedWhileNotablesOfTheOwnSideAreGranted()
    {
        Town(Hero("owner"), false, Evil, Notable("n1", Free), Notable("n2", Evil));

        Won();

        CollectionAssert.AreEqual(new[] { "n1" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    [TestMethod]
    public void OnTournamentFinished_EachOneIsScaledByHisOwnSide()
    {
        _f.Configure(Defaults with { TournamentAmount = 4, Tournament = new SideMultipliers(1f, 0.5f, 0.25f) });
        Town(Hero("owner"), false, Unaligned, Notable("same", Free), Notable("far", Evil), Notable("none", Unaligned));

        Won();

        // Owner neutral 4 * 0.5 = 2; same 4; opposite 4 * 0.25 = 1; neutral 2.
        CollectionAssert.AreEqual(new[] { ("owner", 2), ("same", 4), ("far", 1), ("none", 2) },
            _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
    }

    [TestMethod]
    public void OnTournamentFinished_AHalfPoint_RoundsAwayFromZero()
    {
        _f.Configure(Defaults with { TournamentAmount = 3, Tournament = new SideMultipliers(0.5f, 1f, 0f) });
        Town(Hero("owner"), false, Free);

        Won();

        Assert.AreEqual(2, _f.Grants.Single().Delta, "3 * 0.5 = 1.5 rounds to 2");
    }

    [TestMethod]
    public void OnTournamentFinished_ANotableWithNoFaction_IsNeutral()
    {
        _f.Configure(Defaults with { Tournament = new SideMultipliers(1f, 3f, 0f) });
        Town(null, false, Free, new DeedNotable(Hero("n1"), SideIds.None));

        Won();

        Assert.AreEqual(3, _f.Grants.Single().Delta);
    }

    [TestMethod]
    public void OnTournamentFinished_ASettingsAmountOfZero_GrantsNothing()
    {
        _f.Configure(Defaults with { TournamentAmount = 0 });
        Town(Hero("owner"), false, Free, Notable("n1"));

        Assert.AreEqual(0, Won());
        Assert.AreEqual(0, _f.Grants.Count);
    }

    [TestMethod]
    public void OnTournamentFinished_ReadsEachSideFromItsOwnFaction()
    {
        Town(Hero("owner"), false, Evil, Notable("n1", Free));

        Won();

        _f.Alignment.Received().ResolveSide("k_free", "c_free");
        _f.Alignment.Received().ResolveSide("k_evil", "c_evil");
    }

    // --- de-duplication ------------------------------------------------------------------------------------------------

    [TestMethod]
    public void OnTournamentFinished_ANotableSharesTheOwnersRelationOwner_IsOneGrant()
    {
        Town(Hero("owner", "owner"), false, Free, Notable("n1", owner: "owner"), Notable("n2"));

        var landed = Won();

        CollectionAssert.AreEqual(new[] { "owner", "n2" }, _f.Grants.Select(g => g.HeroId).ToArray());
        Assert.AreEqual(2, landed);
    }

    [TestMethod]
    public void OnTournamentFinished_NotablesWithDifferentOwners_AreEachGranted()
    {
        Town(Hero("owner"), false, Free, Notable("n1", owner: "o1"), Notable("n2", owner: "o2"));

        Won();

        Assert.AreEqual(3, _f.Grants.Count);
    }

    [TestMethod]
    public void OnTournamentFinished_TheStrongerScaledGrantWinsAShareOwner()
    {
        _f.Configure(Defaults with { TournamentAmount = 4, Tournament = new SideMultipliers(1f, 1f, 0.5f) });
        Town(Hero("owner", "boss"), false, Evil, Notable("n1", Free, "boss"));

        Won();

        Assert.AreEqual(("n1", 4), (_f.Grants.Single().HeroId, _f.Grants.Single().Delta));
    }

    // --- the notice --------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void OnTournamentFinished_SomethingLanded_ShowsOneLineWithTheTownName()
    {
        Town(Hero("owner"), false, Free, Notable("n1"), Notable("n2"));

        Won();

        _f.Notice.Received(1).ShowTournamentHonor("Minas Tirith");
    }

    [TestMethod]
    public void OnTournamentFinished_NothingLanded_ShowsNoLine()
    {
        _f.GrantService.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(0);
        Town(Hero("owner"), false, Free, Notable("n1"));

        Assert.AreEqual(0, Won());

        _f.Notice.DidNotReceive().ShowTournamentHonor(Arg.Any<string>());
    }

    [TestMethod]
    public void OnTournamentFinished_NobodyQualifies_ShowsNoLine()
    {
        _f.Configure(Defaults with { Tournament = new SideMultipliers(0f, 0f, 0f) });
        Town(Hero("owner"), false, Free, Notable("n1"));

        Won();

        _f.Notice.DidNotReceive().ShowTournamentHonor(Arg.Any<string>());
    }

    [TestMethod]
    public void OnTournamentFinished_OnlyOneOfSeveralLanded_StillShowsTheLine()
    {
        _f.GrantService.Grant("n1", Arg.Any<int>(), Arg.Any<string>()).Returns(0);
        Town(Hero("owner"), false, Free, Notable("n1"));

        Assert.AreEqual(1, Won());

        _f.Notice.Received(1).ShowTournamentHonor("Minas Tirith");
    }

    [TestMethod]
    public void OnTournamentFinished_NamesTheDeedInTheLogReason()
    {
        Town(Hero("owner"), false, Free);

        Won();

        StringAssert.Contains(_f.Grants.Single().Reason, "tournament");
    }

    // --- the tournaments that earn nothing --------------------------------------------------------------------------------

    [TestMethod]
    public void OnTournamentFinished_ThePlayerLostOrSomeoneElseWon_GrantsNothingWithoutReadingAnything()
    {
        Town(Hero("owner"), false, Free, Notable("n1"));

        var landed = _f.Service.OnTournamentFinished(false, "town_1");

        Assert.AreEqual(0, landed);
        Assert.AreEqual(0, _f.Grants.Count);
        _f.Heroes.DidNotReceive().ReadTournament(Arg.Any<string>());
        _f.Notice.DidNotReceive().ShowTournamentHonor(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void OnTournamentFinished_NoTown_GrantsNothing(string? townId)
    {
        Assert.AreEqual(0, _f.Service.OnTournamentFinished(true, townId));
        _f.Heroes.DidNotReceive().ReadTournament(Arg.Any<string>());
    }

    [TestMethod]
    public void OnTournamentFinished_TheTownCannotBeRead_GrantsNothing()
    {
        _f.Heroes.ReadTournament("town_1").Returns((TournamentFacts?)null);

        Assert.AreEqual(0, Won());
        _f.Notice.DidNotReceive().ShowTournamentHonor(Arg.Any<string>());
    }

    [TestMethod]
    public void OnTournamentFinished_TheMasterSwitchIsOff_IsIgnored()
    {
        _f.Configure(Defaults with { Enabled = false });
        Town(Hero("owner"), false, Free);

        Assert.AreEqual(0, Won());
        _f.Heroes.DidNotReceive().ReadTournament(Arg.Any<string>());
    }

    [TestMethod]
    public void OnTournamentFinished_TheTournamentSwitchIsOff_IsIgnored()
    {
        _f.Configure(Defaults with { TournamentEnabled = false });
        Town(Hero("owner"), false, Free);

        Assert.AreEqual(0, Won());
        _f.Heroes.DidNotReceive().ReadTournament(Arg.Any<string>());
    }

    [TestMethod]
    public void OnTournamentFinished_OtherSwitchesOff_DoNotStopATournamentReward()
    {
        _f.Configure(Defaults with { ReleaseEnabled = false, DonationEnabled = false });
        Town(Hero("owner"), false, Free);

        Assert.AreEqual(1, Won());
    }

    [TestMethod]
    public void OnTournamentFinished_ACoopPeerThatDefersToTheHost_GrantsNothing()
    {
        _f.Coop.ShouldDeferToHost.Returns(true);
        Town(Hero("owner"), false, Free);

        Assert.AreEqual(0, Won());
        _f.Heroes.DidNotReceive().ReadTournament(Arg.Any<string>());
    }

    [TestMethod]
    public void OnTournamentFinished_ADedicatedServer_GrantsNothing()
    {
        _f.Server.IsDedicatedServer.Returns(true);
        Town(Hero("owner"), false, Free);

        Assert.AreEqual(0, Won());
        _f.Heroes.DidNotReceive().ReadTournament(Arg.Any<string>());
        _f.Notice.DidNotReceive().ShowTournamentHonor(Arg.Any<string>());
    }
}
