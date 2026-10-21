using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.HardQuestions;
using TAOM.Features.HardQuestions.Domain;

namespace TAOM.Tests.Features.HardQuestions;

[TestClass]
public class HardQuestionsPenaltyApplierTests
{
    private HardQuestionsFixture _f = null!;
    private readonly LordRef _victim = HardQuestionsFixture.Lord("victim");

    [TestInitialize]
    public void Setup()
    {
        _f = new HardQuestionsFixture();
    }

    private static IEnumerable<KeyValuePair<LordRef, float>> Hits(params (string id, float delta)[] hits)
    {
        foreach (var (id, delta) in hits)
            yield return new KeyValuePair<LordRef, float>(HardQuestionsFixture.Lord(id), delta);
    }

    private void Sides(FactionSide player, FactionSide victim, bool enemies)
    {
        _f.Alignment.ResolveSide("k_player", "c_player").Returns(player);
        _f.Alignment.ResolveSide("k_victim", "c_victim").Returns(victim);
        _f.Alignment.AreEnemyAlignments(player, victim).Returns(enemies);
    }

    [TestMethod]
    public void ApplyRelations_AsksTheAlignmentRuleWithThePlayerTheVictimAndTheEvaluator()
    {
        _f.Penalties.ApplyRelations(_victim, Hits(("ally", -12f)));

        _f.Relations.Received().GetRelationModifier(
            Arg.Is<SideIds>(p => p.FactionId == "k_player" && p.CultureId == "c_player"),
            Arg.Is<SideIds>(p => p.FactionId == "k_victim" && p.CultureId == "c_victim"),
            Arg.Is<SideIds>(p => p.FactionId == "k_ally" && p.CultureId == "c_ally"),
            -12, false);
        _f.Actions.Received().ChangeRelation("ally", -12);
    }

    [TestMethod]
    public void ApplyRelations_ExecutorIsThePlayersRealmPair()
    {
        // #957: the executor is the player's clan map faction with that faction's culture, never the realm's id
        // zipped with the player hero's own culture.
        _f.Player.CultureId.Returns("c_hero");
        _f.Player.Side.Returns(new SideIds("k_player", "c_realm"));

        _f.Penalties.ApplyRelations(_victim, Hits(("ally", -12f)));

        _f.Relations.Received().GetRelationModifier(
            Arg.Is<SideIds>(p => p.FactionId == "k_player" && p.CultureId == "c_realm"),
            Arg.Any<SideIds>(), Arg.Any<SideIds>(), -12, false);
    }

    [TestMethod]
    public void ApplyRelations_UsesTheModifiedDelta()
    {
        _f.Relations.GetRelationModifier(default, default, default, default, default)
            .ReturnsForAnyArgs(new ExecutionRelationResult(-18, false));

        _f.Penalties.ApplyRelations(_victim, Hits(("kin", -12f)));

        _f.Actions.Received().ChangeRelation("kin", -18);
    }

    [TestMethod]
    public void ApplyRelations_ALordListedTwice_TheStrongestDeltaWins()
    {
        _f.Penalties.ApplyRelations(_victim, Hits(("kin", -10f), ("kin", -12f)));
        _f.Penalties.ApplyRelations(_victim, Hits(("other", -12f), ("other", -10f)));

        _f.Actions.Received(1).ChangeRelation("kin", -12);
        _f.Actions.Received(1).ChangeRelation("other", -12);
        _f.Actions.Received(2).ChangeRelation(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void ApplyRelations_TwoRefsOfOneRelationOwner_GiveExactlyOneCallWithTheStrongestDelta()
    {
        var hits = new List<KeyValuePair<LordRef, float>>
        {
            new(HardQuestionsFixture.Lord("son", "chief"), -10f),
            new(HardQuestionsFixture.Lord("chief"), -20f),
            new(HardQuestionsFixture.Lord("daughter", "chief"), -5f),
        };

        _f.Penalties.ApplyRelations(_victim, hits);

        _f.Actions.Received(1).ChangeRelation("chief", -20);
        _f.Actions.Received(1).ChangeRelation(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void ApplyRelations_TheOrderOfTheHitsDoesNotMatter()
    {
        var hits = new List<KeyValuePair<LordRef, float>>
        {
            new(HardQuestionsFixture.Lord("chief"), -20f),
            new(HardQuestionsFixture.Lord("son", "chief"), -10f),
        };
        hits.Reverse();

        _f.Penalties.ApplyRelations(_victim, hits);

        _f.Actions.Received(1).ChangeRelation("chief", -20);
    }

    [TestMethod]
    public void ApplyRelations_NeutralVictimWithTheRealRules_CostsHisOwnRelationOwnerAndNoThirdParty()
    {
        var config = Substitute.For<IAlignmentConfigProvider>();
        config.LoadAlignments().Returns(new Dictionary<string, string>
        {
            ["empire_w"] = "Free",
            ["umbar"] = "Neutral",
            ["sturgia"] = "Neutral",
        });
        var alignment = new AlignmentService(config, Substitute.For<IModLogger>());
        var player = Substitute.For<IArmouryPlayerAdapter>();
        player.KingdomId.Returns("empire_w");
        player.CultureId.Returns("gondor");
        player.Side.Returns(new SideIds("empire_w", "gondor"));
        var actions = Substitute.For<IHardQuestionsActionAdapter>();
        actions.ChangeRelation(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        var applier = new HardQuestionsPenaltyApplier(alignment, new ExecutionRelationService(alignment), player,
            actions, Substitute.For<IPlayerTraitAdapter>(), _f.ConfigProvider, Substitute.For<IDedicatedServerProvider>());
        var victim = new LordRef("captive", "umbar", new SideIds("umbar", "umbar"), "umbar_chief");
        var hits = new List<KeyValuePair<LordRef, float>>
        {
            new(victim, -100f),
            new(new LordRef("cousin", "umbar", new SideIds("umbar", "umbar"), "umbar_chief"), -15f),
            new(new LordRef("other_clan", "umbar", new SideIds("umbar", "umbar"), "other_chief"), -15f),
            new(new LordRef("stranger", "sturgia", new SideIds("sturgia", "sturgia"), "stranger"), -10f),
        };

        applier.ApplyRelations(victim, hits);

        actions.Received(1).ChangeRelation("umbar_chief", -100);
        actions.DidNotReceive().ChangeRelation("other_chief", Arg.Any<int>());
        actions.DidNotReceive().ChangeRelation("stranger", Arg.Any<int>());
        actions.Received(1).ChangeRelation(Arg.Any<string>(), Arg.Any<int>());
        actions.Received(1).ShowRelationsHurt(1);
    }

    [TestMethod]
    public void ApplyRelations_NeutralVictimUnderPersonalRelations_CostsHisClanAndNoOtherClan()
    {
        var config = Substitute.For<IAlignmentConfigProvider>();
        config.LoadAlignments().Returns(new Dictionary<string, string>
        {
            ["empire_w"] = "Free",
            ["umbar"] = "Neutral",
        });
        var alignment = new AlignmentService(config, Substitute.For<IModLogger>());
        var player = Substitute.For<IArmouryPlayerAdapter>();
        player.KingdomId.Returns("empire_w");
        player.CultureId.Returns("gondor");
        player.Side.Returns(new SideIds("empire_w", "gondor"));
        var actions = Substitute.For<IHardQuestionsActionAdapter>();
        actions.ChangeRelation(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        var applier = new HardQuestionsPenaltyApplier(alignment, new ExecutionRelationService(alignment), player,
            actions, Substitute.For<IPlayerTraitAdapter>(), _f.ConfigProvider, Substitute.For<IDedicatedServerProvider>());
        var victim = new LordRef("captive", "umbar", new SideIds("umbar", "umbar"), clanId: "clan_umbar");
        var hits = new List<KeyValuePair<LordRef, float>>
        {
            new(victim, -100f),
            new(new LordRef("umbar_chief", "umbar", new SideIds("umbar", "umbar"), clanId: "clan_umbar"), -20f),
            new(new LordRef("cousin", "umbar", new SideIds("umbar", "umbar"), clanId: "clan_umbar"), -20f),
            new(new LordRef("other_clan", "umbar", new SideIds("umbar", "umbar"), clanId: "clan_other"), -15f),
        };

        applier.ApplyRelations(victim, hits);

        actions.Received(1).ChangeRelation("captive", -100);
        actions.Received(1).ChangeRelation("umbar_chief", -20);
        actions.Received(1).ChangeRelation("cousin", -20);
        actions.DidNotReceive().ChangeRelation("other_clan", Arg.Any<int>());
        actions.Received(3).ChangeRelation(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void ApplyRelations_KinslayingKeepsTheLargerModifiedDeltaOverTheFloor()
    {
        _f.Relations.GetRelationModifier(default, default, default, default, default)
            .ReturnsForAnyArgs(new ExecutionRelationResult(-30, false));

        _f.Penalties.ApplyRelations(_victim, Hits(("victim", -20f)));

        _f.Actions.Received().ChangeRelation("victim", -30);
    }

    [TestMethod]
    public void ApplyRelations_AThirdPartyZeroedByTheRule_ChangesNothing()
    {
        _f.Relations.GetRelationModifier(default, default, default, default, default)
            .ReturnsForAnyArgs(new ExecutionRelationResult(0, false));

        _f.Penalties.ApplyRelations(_victim, Hits(("third_party", -12f)));

        _f.Actions.DidNotReceiveWithAnyArgs().ChangeRelation(default!, default);
    }

    [TestMethod]
    public void ApplyRelations_RepeatedAndZeroedOwners_ShowsTheChargedOwnerCount()
    {
        _f.Relations.GetRelationModifier(default, default, default, default, default)
            .ReturnsForAnyArgs(call => new ExecutionRelationResult(
                call.ArgAt<SideIds>(2).FactionId == "k_zeroed" ? 0 : call.ArgAt<int>(3), false));

        _f.Penalties.ApplyRelations(_victim, Hits(("a", -10f), ("a", -12f), ("b", -5f), ("zeroed", -9f)));

        _f.Actions.Received(1).ShowRelationsHurt(2);
    }

    [TestMethod]
    public void ApplyRelations_NothingCharged_ShowsNoSummary()
    {
        _f.Relations.GetRelationModifier(default, default, default, default, default)
            .ReturnsForAnyArgs(new ExecutionRelationResult(0, false));

        _f.Penalties.ApplyRelations(_victim, Hits(("third_party", -12f)));

        _f.Actions.DidNotReceiveWithAnyArgs().ShowRelationsHurt(default);
    }

    [TestMethod]
    public void ApplyRelations_AnOwnerTheEngineCannotFind_IsNotCounted()
    {
        _f.Actions.ChangeRelation("x", Arg.Any<int>()).Returns(false);

        _f.Penalties.ApplyRelations(_victim, Hits(("x", -10f), ("y", -10f)));

        _f.Actions.Received(1).ShowRelationsHurt(1);
    }

    [TestMethod]
    public void ApplyRelations_OnADedicatedServer_ShowsNoSummary()
    {
        _f.Server.IsDedicatedServer.Returns(true);

        _f.Penalties.ApplyRelations(_victim, Hits(("kin", -10f)));

        _f.Actions.Received(1).ChangeRelation("kin", -10);
        _f.Actions.DidNotReceiveWithAnyArgs().ShowRelationsHurt(default);
    }

    [TestMethod]
    public void ApplyTraitXp_SameSide_UsesTheSameSideMultiplier()
    {
        Sides(FactionSide.Free, FactionSide.Free, enemies: false);

        _f.Penalties.ApplyTraitXp(_victim, "Honor", -100f);

        _f.Traits.Received().AddTraitXp("Honor", -150);
    }

    [TestMethod]
    public void ApplyTraitXp_EnemySide_UsesTheEnemyMultiplier()
    {
        Sides(FactionSide.Free, FactionSide.Evil, enemies: true);

        _f.Penalties.ApplyTraitXp(_victim, "Honor", -100f);

        _f.Traits.Received().AddTraitXp("Honor", -50);
    }

    [TestMethod]
    public void ApplyTraitXp_ANeutralPlayerOrVictim_UsesTheNeutralMultiplier()
    {
        Sides(FactionSide.Neutral, FactionSide.Evil, enemies: true);
        _f.Penalties.ApplyTraitXp(_victim, "Honor", -100f);

        Sides(FactionSide.Free, FactionSide.Neutral, enemies: true);
        _f.Penalties.ApplyTraitXp(_victim, "Honor", -100f);

        _f.Traits.Received(2).AddTraitXp("Honor", -100);
    }

    [TestMethod]
    public void ApplyTraitXp_PlayerOfNeutralRealmWithFreeCulture_UsesNeutralMultiplier()
    {
        // #957: a Neutral realm's player whose own culture is Free is Neutral, so the multiplier is the Neutral one
        // (1.0), not the same-side 1.5 that the realm id zipped with the hero's culture would pick.
        var config = Substitute.For<IAlignmentConfigProvider>();
        config.LoadAlignments().Returns(new Dictionary<string, string>
        {
            ["empire_w"] = "Free",
            ["gondor"] = "Free",
            ["umbar"] = "Neutral",
        });
        var alignment = new AlignmentService(config, Substitute.For<IModLogger>());
        var player = Substitute.For<IArmouryPlayerAdapter>();
        player.KingdomId.Returns("umbar");
        player.CultureId.Returns("gondor");
        player.Side.Returns(new SideIds("umbar", "umbar"));
        var traits = Substitute.For<IPlayerTraitAdapter>();
        var applier = new HardQuestionsPenaltyApplier(alignment, _f.Relations, player, Substitute.For<IHardQuestionsActionAdapter>(),
            traits, _f.ConfigProvider, Substitute.For<IDedicatedServerProvider>());
        var victim = new LordRef("captive", "empire_w", new SideIds("empire_w", "gondor"));

        applier.ApplyTraitXp(victim, "Honor", -100f);

        traits.Received().AddTraitXp("Honor", -100);
    }

    [TestMethod]
    public void ApplyTraitXp_AScaledAmountOfZero_IsNotSent()
    {
        _f.Config.NeutralTraitMultiplier = 0f;

        _f.Penalties.ApplyTraitXp(_victim, "Honor", -100f);

        _f.Traits.DidNotReceiveWithAnyArgs().AddTraitXp(default!, default);
    }

    [TestMethod]
    public void ApplyCompanionLosses_ByMercy_ScalesTheCapByTheLevelOutOfTwo()
    {
        var companions = new List<CompanionRef> { new("soft", 1, 2), new("gentle", 2, 0), new("cold", 0, 2), new("cruel", -2, 2) };

        var losers = _f.Penalties.ApplyCompanionLosses(companions, false, -2f);

        CollectionAssert.AreEqual(new[] { "soft", "gentle" }, new List<string>(losers));
        _f.Actions.Received().ChangeRelation("soft", -1);
        _f.Actions.Received().ChangeRelation("gentle", -2);
    }

    [TestMethod]
    public void ApplyCompanionLosses_ByHonor_HalfTheCapAtLevelOneAndTheWholeCapAtLevelTwo()
    {
        var companions = new List<CompanionRef> { new("proud", 0, 1), new("noble", 0, 2), new("soft", 2, 0) };

        var losers = _f.Penalties.ApplyCompanionLosses(companions, true, -10f);

        CollectionAssert.AreEqual(new[] { "proud", "noble" }, new List<string>(losers));
        _f.Actions.Received().ChangeRelation("proud", -5);
        _f.Actions.Received().ChangeRelation("noble", -10);
    }

    [TestMethod]
    public void ApplyCompanionLosses_ALevelBeyondTheTraitMaximum_NeverExceedsTheCap()
    {
        _f.Penalties.ApplyCompanionLosses(new List<CompanionRef> { new("odd", 0, 7) }, true, -10f);

        _f.Actions.Received().ChangeRelation("odd", -10);
    }

    [TestMethod]
    public void ApplyCompanionLosses_ACapOfZero_ChargesNobody()
    {
        var losers = _f.Penalties.ApplyCompanionLosses(new List<CompanionRef> { new("soft", 2, 2) }, false, 0f);

        Assert.AreEqual(0, losers.Count);
        _f.Actions.DidNotReceiveWithAnyArgs().ChangeRelation(default!, default);
    }

    [TestMethod]
    public void ApplyCompanionLosses_ACompanionTheEngineCannotFind_IsNotCounted()
    {
        _f.Actions.ChangeRelation("ghost", Arg.Any<int>()).Returns(false);

        var losers = _f.Penalties.ApplyCompanionLosses(new List<CompanionRef> { new("ghost", 2, 2) }, false, -2f);

        Assert.AreEqual(0, losers.Count);
    }
}
