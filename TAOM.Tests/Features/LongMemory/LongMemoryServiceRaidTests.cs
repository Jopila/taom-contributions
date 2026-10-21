using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.LongMemory;
using TAOM.Features.LongMemory.Models;
using TAOM.Features.Relations;
using static TAOM.Tests.Features.LongMemory.LongMemoryFixture;

namespace TAOM.Tests.Features.LongMemory;

/// <summary>
/// A raid the player led on a village another clan owns: relation lost with the owner's clan leader and every notable, scaled by
/// the damage the raid did (the hit points it took on the village's 0 to 1 health scale, no floor) and by how the player's side
/// stands to the owner's, de-duplicated per relation owner, and one line shown only when something landed.
/// </summary>
[TestClass]
public class LongMemoryServiceRaidTests
{
    private LongMemoryFixture _f = null!;

    [TestInitialize]
    public void Setup() => _f = new LongMemoryFixture();

    private int Raid(VillageHostileActionFacts? facts) => _f.Service.OnRaidCompleted(_f.Read(facts));

    // Pins the whole (HeroId, Delta) sequence, so an empty grant list fails (All() over an empty list is true).
    private void AssertEveryHeroLost(int delta) =>
        CollectionAssert.AreEqual(new[] { ("owner", delta), ("n1", delta), ("n2", delta) },
            _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());

    [TestMethod]
    public void OnRaidCompleted_AFullRaidOnASameSideVillage_LosesTheFullAmountWithTheOwnerAndEveryNotable()
    {
        var landed = Raid(Facts());

        CollectionAssert.AreEqual(new[] { ("owner", -50), ("n1", -50), ("n2", -50) },
            _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
        Assert.AreEqual(3, landed);
    }

    [TestMethod]
    public void OnRaidCompleted_EachGrantIsLoggedWithTheReason()
    {
        Raid(Facts());

        Assert.AreEqual(3, _f.Grants.Count, "the owner and two notables");
        Assert.IsTrue(_f.Grants.All(g => g.Reason == "long memory: raid"));
    }

    [TestMethod]
    public void OnRaidCompleted_NoFacts_LosesNothing()
    {
        Assert.AreEqual(0, Raid(null));
        Assert.AreEqual(0, _f.Grants.Count);
    }

    // --- who is raiding, and what ------------------------------------------------------------------------------------------

    [TestMethod]
    public void OnRaidCompleted_TheLeaderIsNotThePlayer_LosesNothing()
    {
        // An AI lord's raid, a raid inside another lord's army, a raid an enlisted player joins under a commander (an act the enlisted player starts personally counts): the leader party is not the player's own.
        Assert.AreEqual(0, Raid(Facts(playerLeads: false)));
        Assert.AreEqual(0, _f.Grants.Count);
    }

    [TestMethod]
    public void OnRaidCompleted_TheVillageBelongsToThePlayersClan_LosesNothing()
    {
        Assert.AreEqual(0, Raid(Facts(playerOwns: true)));
        Assert.AreEqual(0, _f.Grants.Count);
    }

    [TestMethod]
    public void OnRaidCompleted_NotAVillage_LosesNothing()
    {
        Assert.AreEqual(0, Raid(Facts(isVillage: false)));
        Assert.AreEqual(0, _f.Grants.Count);
    }

    // --- the scale ---------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void OnRaidCompleted_AMilitiaFightThatDidNoDamage_LosesNothing()
    {
        // The battle against the militia is itself a raid map event and its finalize fires the event; the attackers may have
        // won it. Winning is not the rule: the damage is, and an event that took no hit points pays 0.
        Assert.AreEqual(0, Raid(Facts(damage: 0f)));
        Assert.AreEqual(0, _f.Grants.Count);
    }

    [TestMethod]
    public void OnRaidCompleted_ANegativeDamage_LosesNothing()
    {
        Assert.AreEqual(0, Raid(Facts(damage: -0.5f)));
        Assert.AreEqual(0, _f.Grants.Count);
    }

    [TestMethod]
    public void OnRaidCompleted_ALittleDamage_LosesAProportionalAmount_NotAFloor()
    {
        Raid(Facts(damage: 0.02f));

        // 50 * 0.02 = 1.
        AssertEveryHeroLost(-1);
    }

    [TestMethod]
    public void OnRaidCompleted_ADamageThatScalesToLessThanHalfARelationPoint_LosesNothing()
    {
        // 50 * 0.005 = 0.25 rounds to 0: no floor lifts it.
        Assert.AreEqual(0, Raid(Facts(damage: 0.005f)));
        Assert.AreEqual(0, _f.Grants.Count);
    }

    [TestMethod]
    public void OnRaidCompleted_TheEventsOfOneInterruptedRaid_AddUpToAtMostAFullRaid()
    {
        // A raid cut short pays per event, and the engine's RaidDamage of the events of one raid sums to at most about one hit (0.05) more than 1.
        Raid(Facts(damage: 0.3f));
        Raid(Facts(damage: 0.7f));

        var perHero = _f.Grants.GroupBy(g => g.HeroId).Select(g => g.Sum(x => x.Delta)).ToArray();
        CollectionAssert.AreEqual(new[] { -50, -50, -50 }, perHero);
    }

    [TestMethod]
    public void OnRaidCompleted_HalfTheVillage_LosesHalfTheAmount()
    {
        Raid(Facts(damage: 0.5f));

        AssertEveryHeroLost(-25);
    }

    [TestMethod]
    public void OnRaidCompleted_ANonFiniteDamage_LosesNothing()
    {
        Assert.AreEqual(0, Raid(Facts(damage: float.NaN)));
        Assert.AreEqual(0, _f.Grants.Count);
    }

    // --- scaling by side ---------------------------------------------------------------------------------------------------

    [TestMethod]
    public void OnRaidCompleted_TheOwnersRealmIsOnTheOppositeSide_LosesATenth()
    {
        Raid(Facts(player: Evil, owner: Free));

        // 50 * 0.1 = 5 for the owner and for each notable: one pairing for the whole village, the owner's side.
        CollectionAssert.AreEqual(new[] { ("owner", -5), ("n1", -5), ("n2", -5) },
            _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
    }

    [TestMethod]
    public void OnRaidCompleted_AnEvilPlayerRaidingAnEvilVillage_LosesTheFullAmount()
    {
        Raid(Facts(player: Evil, owner: Evil));

        AssertEveryHeroLost(-50);
    }

    [TestMethod]
    public void OnRaidCompleted_AnUnalignedOwnersRealm_LosesHalf()
    {
        Raid(Facts(owner: Unaligned));

        AssertEveryHeroLost(-25);
    }

    [TestMethod]
    public void OnRaidCompleted_AnUnalignedPlayer_LosesHalf()
    {
        Raid(Facts(player: Unaligned, owner: Evil));

        AssertEveryHeroLost(-25);
    }

    [TestMethod]
    public void OnRaidCompleted_TheMultiplierAndTheDamageCompose()
    {
        Raid(Facts(player: Free, owner: Evil, damage: 0.5f));

        // 50 * 0.1 * 0.5 = 2.5 rounds away from zero to 3.
        AssertEveryHeroLost(-3);
    }

    [TestMethod]
    public void OnRaidCompleted_TheOwnerAndTheNotablesHaveTheirOwnAmounts()
    {
        _f.Configure(Defaults with { RaidOwnerAmount = 30, RaidNotableAmount = 80 });

        Raid(Facts());

        CollectionAssert.AreEqual(new[] { ("owner", -30), ("n1", -80), ("n2", -80) },
            _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
    }

    [TestMethod]
    public void OnRaidCompleted_AnAmountOfZero_SkipsThatGroupOnly()
    {
        _f.Configure(Defaults with { RaidOwnerAmount = 0 });

        Raid(Facts());

        CollectionAssert.AreEqual(new[] { "n1", "n2" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    [TestMethod]
    public void OnRaidCompleted_AMultiplierOfZeroForThePairing_LosesNothing()
    {
        _f.Configure(Defaults with { Multipliers = new SideMultipliers(1f, 0.5f, 0f) });

        Assert.AreEqual(0, Raid(Facts(player: Evil, owner: Free)));
        Assert.AreEqual(0, _f.Grants.Count);
    }

    // --- who is asked ------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void OnRaidCompleted_TheVillageHasNoOwnerLeader_LosesWithTheNotablesOnly()
    {
        Raid(Facts(noOwnerLeader: true));

        CollectionAssert.AreEqual(new[] { "n1", "n2" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    [TestMethod]
    public void OnRaidCompleted_AVillageWithNoNotables_LosesWithTheOwnerOnly()
    {
        Raid(Facts(notables: new RelationTarget[0]));

        CollectionAssert.AreEqual(new[] { "owner" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    [TestMethod]
    public void OnRaidCompleted_ADeadOwnerLeaderOrNotable_IsSkipped()
    {
        Raid(Facts(ownerLeader: Hero("owner", alive: false), notables: new[] { Hero("n1", alive: false), Hero("n2") }));

        CollectionAssert.AreEqual(new[] { "n2" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    [TestMethod]
    public void OnRaidCompleted_ANotableWhoIsThePlayer_IsSkipped()
    {
        Raid(Facts(notables: new[] { Hero("me", isPlayer: true), Hero("n1") }));

        CollectionAssert.AreEqual(new[] { "owner", "n1" }, _f.Grants.Select(g => g.HeroId).ToArray());
    }

    // --- one change per relation owner -------------------------------------------------------------------------------------

    [TestMethod]
    public void OnRaidCompleted_TwoHeroesWhoShareARelationOwner_LoseOnceByTheStrongerHit()
    {
        // Two targets that resolve to one relation owner would stack on one person, so the shared call grants the stronger once
        // (RelationGrantServiceExtensionsTests pins the rule). Notables belong to no clan, so in the live game each is its own
        // owner and this only guards.
        _f.Configure(Defaults with { RaidOwnerAmount = 20, RaidNotableAmount = 50 });

        var landed = Raid(Facts(ownerLeader: Hero("owner", owner: "boss"), notables: new[] { Hero("n1", owner: "boss"), Hero("n2") }));

        CollectionAssert.AreEqual(new[] { ("n1", -50), ("n2", -50) }, _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
        Assert.AreEqual(2, landed);
    }

    // --- counted by what landed --------------------------------------------------------------------------------------------

    [TestMethod]
    public void OnRaidCompleted_OnlyAChangeThatLanded_IsCounted()
    {
        _f.GrantService.Grant("n1", Arg.Any<int>(), Arg.Any<string>()).Returns(0);

        var landed = Raid(Facts());

        Assert.AreEqual(2, landed);
        _f.Notice.Received(1).ShowRaid("Dol Amroth Farm");
    }

    [TestMethod]
    public void OnRaidCompleted_NothingLanded_ShowsNoLine()
    {
        _f.GrantService.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(0);

        Assert.AreEqual(0, Raid(Facts()));

        _f.Notice.DidNotReceiveWithAnyArgs().ShowRaid(default!);
    }

    [TestMethod]
    public void OnRaidCompleted_SomethingLanded_ShowsOneRaidLine_AndNoCoercionLine()
    {
        Raid(Facts(name: "Pelargir Hamlet"));

        _f.Notice.Received(1).ShowRaid("Pelargir Hamlet");
        _f.Notice.DidNotReceiveWithAnyArgs().ShowCoercion(default!);
    }

    [TestMethod]
    public void OnRaidCompleted_AnythingLeftOut_ShowsNoLine()
    {
        Raid(Facts(playerOwns: true));

        _f.Notice.DidNotReceiveWithAnyArgs().ShowRaid(default!);
    }

    // --- switches and authority --------------------------------------------------------------------------------------------

    [TestMethod]
    public void OnRaidCompleted_TheMasterSwitchOff_LosesNothing()
    {
        _f.Configure(Defaults with { Enabled = false });

        Assert.AreEqual(0, Raid(Facts()));
        Assert.AreEqual(0, _f.Grants.Count);
        Assert.AreEqual(0, _f.Reads, "the gates come before the read");
    }

    [TestMethod]
    public void OnRaidCompleted_TheRaidSwitchOff_LosesNothing()
    {
        _f.Configure(Defaults with { RaidEnabled = false });

        Assert.AreEqual(0, Raid(Facts()));
        Assert.AreEqual(0, _f.Grants.Count);
        Assert.AreEqual(0, _f.Reads, "the gates come before the read");
    }

    [TestMethod]
    public void OnRaidCompleted_TheCoercionSwitchOff_StillLosesForARaid()
    {
        _f.Configure(Defaults with { CoercionEnabled = false });

        Assert.AreEqual(3, Raid(Facts()));
    }

    [TestMethod]
    public void OnRaidCompleted_ADedicatedServer_LosesNothing()
    {
        _f.Server.IsDedicatedServer.Returns(true);

        Assert.AreEqual(0, Raid(Facts()));
        Assert.AreEqual(0, _f.Grants.Count);
        Assert.AreEqual(0, _f.Reads, "the gates come before the read");
    }

    [TestMethod]
    public void OnRaidCompleted_ACoopPeerThatDefersToTheHost_LosesNothing()
    {
        _f.Coop.ShouldDeferToHost.Returns(true);

        Assert.AreEqual(0, Raid(Facts()));
        Assert.AreEqual(0, _f.Grants.Count);
        Assert.AreEqual(0, _f.Reads, "the gates come before the read");
    }

    [TestMethod]
    public void OnRaidCompleted_EveryGatePassed_ReadsTheVillageOnce()
    {
        Raid(Facts());

        Assert.AreEqual(1, _f.Reads);
    }

    [TestMethod]
    public void OnRaidCompleted_TheReaderAnswersNull_LosesNothingAfterOneRead()
    {
        Assert.AreEqual(0, Raid(null));

        Assert.AreEqual(0, _f.Grants.Count);
        Assert.AreEqual(1, _f.Reads);
    }
}
