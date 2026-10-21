using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.LongMemory;
using TAOM.Features.LongMemory.Models;
using static TAOM.Tests.Features.LongMemory.LongMemoryFixture;

namespace TAOM.Tests.Features.LongMemory;

/// <summary>
/// Forcing a village to give goods or recruits. Two routes: the villagers resist, a fight follows and the engine's completed
/// event reports who won (the attackers' win pays); or they give way, which fires no event, and the "Continue" option of the
/// no-resist menu is the only signal (the two pinned option ids pay once; every other option pays nothing). The village is read
/// only after every gate, so each gate test also counts the reader's calls.
/// </summary>
[TestClass]
public class LongMemoryServiceCoercionTests
{
    private const string SuppliesContinue = "force_supplies_no_resist_continue";
    private const string VolunteersContinue = "force_volunteers_no_resist_continue";

    private LongMemoryFixture _f = null!;

    [TestInitialize]
    public void Setup() => _f = new LongMemoryFixture();

    private int Coerce(bool attackersWon, VillageHostileActionFacts? facts) =>
        _f.Service.OnCoercionCompleted(attackersWon, _f.Read(facts));

    private int Menu(string? optionId, VillageHostileActionFacts? facts) =>
        _f.Service.OnMenuOptionSelected(optionId, _f.Read(facts));

    // --- the resist route: the completed event -----------------------------------------------------------------------------

    [TestMethod]
    public void OnCoercionCompleted_TheAttackersWon_LosesTheCoercionAmountsWithTheOwnerAndEveryNotable()
    {
        var landed = Coerce(true, Facts());

        CollectionAssert.AreEqual(new[] { ("owner", -20), ("n1", -25), ("n2", -25) },
            _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
        Assert.AreEqual(3, landed);
        Assert.IsTrue(_f.Grants.All(g => g.Reason == "long memory: forced taking"));
    }

    [TestMethod]
    public void OnCoercionCompleted_TheDefendersWon_LosesNothing()
    {
        Assert.AreEqual(0, Coerce(false, Facts()));
        Assert.AreEqual(0, _f.Grants.Count);
    }

    [TestMethod]
    public void OnCoercionCompleted_TheDefendersWon_NeverReadsTheVillage()
    {
        Coerce(false, Facts());

        Assert.AreEqual(0, _f.Reads);
    }

    [TestMethod]
    public void OnCoercionCompleted_NoFacts_LosesNothing()
    {
        Assert.AreEqual(0, Coerce(true, null));
        Assert.AreEqual(0, _f.Grants.Count);
    }

    [TestMethod]
    public void OnCoercionCompleted_IsNotScaledByRaidDamage()
    {
        Coerce(true, Facts(damage: 0f));

        Assert.AreEqual(3, _f.Grants.Count);
        Assert.AreEqual(-20, _f.Grants[0].Delta);
    }

    [TestMethod]
    public void OnCoercionCompleted_TheVillageBelongsToThePlayersClan_LosesNothing()
    {
        Assert.AreEqual(0, Coerce(true, Facts(playerOwns: true)));
    }

    [TestMethod]
    public void OnCoercionCompleted_TheLeaderIsNotThePlayer_LosesNothing()
    {
        Assert.AreEqual(0, Coerce(true, Facts(playerLeads: false)));
    }

    [TestMethod]
    public void OnCoercionCompleted_NotAVillage_LosesNothing()
    {
        Assert.AreEqual(0, Coerce(true, Facts(isVillage: false)));
    }

    [TestMethod]
    public void OnCoercionCompleted_ScalesByTheOwnersSide()
    {
        Coerce(true, Facts(player: Evil, owner: Free));

        // 20 * 0.1 = 2 for the owner, 25 * 0.1 = 2.5 rounds away from zero to 3 for each notable.
        CollectionAssert.AreEqual(new[] { ("owner", -2), ("n1", -3), ("n2", -3) },
            _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
    }

    [TestMethod]
    public void OnCoercionCompleted_SharingARelationOwner_LosesOnceByTheStrongerHit()
    {
        // Two targets that resolve to one relation owner would stack on one person, so the shared call grants the stronger once
        // (RelationGrantServiceExtensionsTests pins the rule). Notables belong to no clan, so in the live game each is its own
        // owner and this only guards.
        Coerce(true, Facts(ownerLeader: Hero("owner", owner: "boss"), notables: new[] { Hero("n1", owner: "boss") }));

        CollectionAssert.AreEqual(new[] { ("n1", -25) }, _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
    }

    [TestMethod]
    public void OnCoercionCompleted_SomethingLanded_ShowsOneCoercionLine_AndNoRaidLine()
    {
        Coerce(true, Facts(name: "Pelargir Hamlet"));

        _f.Notice.Received(1).ShowCoercion("Pelargir Hamlet");
        _f.Notice.DidNotReceiveWithAnyArgs().ShowRaid(default!);
    }

    [TestMethod]
    public void OnCoercionCompleted_NothingLanded_ShowsNoLine()
    {
        _f.GrantService.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(0);

        Assert.AreEqual(0, Coerce(true, Facts()));

        _f.Notice.DidNotReceiveWithAnyArgs().ShowCoercion(default!);
    }

    [TestMethod]
    public void OnCoercionCompleted_TheCoercionSwitchOff_LosesNothing()
    {
        _f.Configure(Defaults with { CoercionEnabled = false });

        Assert.AreEqual(0, Coerce(true, Facts()));
        Assert.AreEqual(0, _f.Reads, "the gates come before the read");
    }

    [TestMethod]
    public void OnCoercionCompleted_TheMasterSwitchOff_LosesNothing()
    {
        _f.Configure(Defaults with { Enabled = false });

        Assert.AreEqual(0, Coerce(true, Facts()));
        Assert.AreEqual(0, _f.Reads, "the gates come before the read");
    }

    [TestMethod]
    public void OnCoercionCompleted_TheRememberRaidsSwitchOff_StillLosesForAForcedTaking()
    {
        _f.Configure(Defaults with { RaidEnabled = false });

        Assert.AreEqual(3, Coerce(true, Facts()));
    }

    [TestMethod]
    public void OnCoercionCompleted_ADedicatedServer_LosesNothing()
    {
        _f.Server.IsDedicatedServer.Returns(true);

        Assert.AreEqual(0, Coerce(true, Facts()));
        Assert.AreEqual(0, _f.Grants.Count);
        Assert.AreEqual(0, _f.Reads, "the gates come before the read");
    }

    [TestMethod]
    public void OnCoercionCompleted_ACoopPeerThatDefersToTheHost_LosesNothing()
    {
        _f.Coop.ShouldDeferToHost.Returns(true);

        Assert.AreEqual(0, Coerce(true, Facts()));
        Assert.AreEqual(0, _f.Grants.Count);
        Assert.AreEqual(0, _f.Reads, "the gates come before the read");
    }

    [TestMethod]
    public void OnCoercionCompleted_EveryGatePassed_ReadsTheVillageOnce()
    {
        Coerce(true, Facts());

        Assert.AreEqual(1, _f.Reads);
    }

    // --- the no-resist route: the menu option -----------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(SuppliesContinue)]
    [DataRow(VolunteersContinue)]
    public void OnMenuOptionSelected_ANoResistContinue_LosesOnceWithTheRightAmounts(string optionId)
    {
        var landed = Menu(optionId, Facts());

        CollectionAssert.AreEqual(new[] { ("owner", -20), ("n1", -25), ("n2", -25) },
            _f.Grants.Select(g => (g.HeroId, g.Delta)).ToArray());
        Assert.AreEqual(3, landed);
        _f.Notice.Received(1).ShowCoercion("Dol Amroth Farm");
    }

    [DataTestMethod]
    [DataRow("force_supplies_village_continue")]
    [DataRow("force_supplies_resist_continue")]
    [DataRow("force_volunteers_resist_continue")]
    [DataRow("force_supplies_no_resist_leave")]
    [DataRow("raid_village_no_resist_continue")]
    [DataRow("raid_village")]
    [DataRow("")]
    [DataRow(null)]
    public void OnMenuOptionSelected_AnyOtherOption_LosesNothing_AndNeverReadsAVillage(string? optionId)
    {
        Assert.AreEqual(0, Menu(optionId, Facts()));

        Assert.AreEqual(0, _f.Grants.Count);
        Assert.AreEqual(0, _f.Reads);
    }

    [TestMethod]
    public void OnMenuOptionSelected_NoVillageToRead_LosesNothing()
    {
        Assert.AreEqual(0, Menu(SuppliesContinue, null));
        Assert.AreEqual(0, _f.Grants.Count);
    }

    [TestMethod]
    public void OnMenuOptionSelected_AVillageThePlayersClanOwns_LosesNothing()
    {
        Assert.AreEqual(0, Menu(VolunteersContinue, Facts(playerOwns: true)));
    }

    [TestMethod]
    public void OnMenuOptionSelected_ThePlayerDoesNotLead_LosesNothing()
    {
        Assert.AreEqual(0, Menu(VolunteersContinue, Facts(playerLeads: false)));
    }

    [TestMethod]
    public void OnMenuOptionSelected_ScalesByTheOwnersSide()
    {
        Menu(SuppliesContinue, Facts(owner: Unaligned));

        // 20 * 0.5 = 10, 25 * 0.5 = 12.5 rounds away from zero to 13.
        CollectionAssert.AreEqual(new[] { -10, -13, -13 }, _f.Grants.Select(g => g.Delta).ToArray());
    }

    [TestMethod]
    public void OnMenuOptionSelected_TheRememberRaidsSwitchOff_StillLosesForAForcedTaking()
    {
        _f.Configure(Defaults with { RaidEnabled = false });

        Assert.AreEqual(3, Menu(SuppliesContinue, Facts()));
    }

    [TestMethod]
    public void OnMenuOptionSelected_EveryGatePassed_ReadsTheVillageOnce()
    {
        Menu(SuppliesContinue, Facts());

        Assert.AreEqual(1, _f.Reads);
    }

    [TestMethod]
    public void OnMenuOptionSelected_TheCoercionSwitchOff_NeverReadsTheVillage()
    {
        _f.Configure(Defaults with { CoercionEnabled = false });

        Assert.AreEqual(0, Menu(SuppliesContinue, Facts()));
        Assert.AreEqual(0, _f.Reads);
    }

    [TestMethod]
    public void OnMenuOptionSelected_TheMasterSwitchOff_NeverReadsTheVillage()
    {
        _f.Configure(Defaults with { Enabled = false });

        Assert.AreEqual(0, Menu(SuppliesContinue, Facts()));
        Assert.AreEqual(0, _f.Reads);
    }

    [TestMethod]
    public void OnMenuOptionSelected_ADedicatedServer_NeverReadsTheVillage()
    {
        _f.Server.IsDedicatedServer.Returns(true);

        Assert.AreEqual(0, Menu(SuppliesContinue, Facts()));
        Assert.AreEqual(0, _f.Reads);
    }

    [TestMethod]
    public void OnMenuOptionSelected_ACoopPeerThatDefersToTheHost_NeverReadsTheVillage()
    {
        _f.Coop.ShouldDeferToHost.Returns(true);

        Assert.AreEqual(0, Menu(SuppliesContinue, Facts()));
        Assert.AreEqual(0, _f.Reads);
    }
}
