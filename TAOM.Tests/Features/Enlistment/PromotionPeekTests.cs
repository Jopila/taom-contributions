using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The read-only half of the promotion chokepoint, and the single-key pick that makes an otherwise
/// invisible five-gate ladder legible.
///
/// Promotion needs FIVE simultaneous thresholds. Listing all five turns a status board into a
/// spreadsheet; listing none — which is what shipped — makes the ladder read to the player as a
/// broken feature. So the evaluator names exactly one, and it has to be the one that actually
/// gates them, deterministically, every time.
/// </summary>
[TestClass]
public class PromotionMostBindingTests
{
    /// <summary>The compiled default ladder itself, so these tests cannot drift from it. It includes the
    /// deliberately negative first MinTrust, the case that would divide by zero.</summary>
    private static List<PromotionRequirement> Ladder() => EnlistmentContentConfigProvider.BuildDefaults().Promotions;

    [TestMethod]
    public void Evaluate_EqualRelativeGaps_PicksTheEarlierKey()
    {
        // A fresh recruit is 7-of-7 days and 100-of-100 XP short: both are a 100% shortfall, so the
        // tie-break — the evaluator's own declaration order — decides. Pinned because the board's
        // value-equality throttle needs the pick to be stable for identical input.
        var eval = PromotionEvaluator.Evaluate(new ServiceProgressSnapshot { Rank = ServiceRank.Private }, Ladder());

        Assert.AreEqual("days", eval.MostBindingUnmetKey);
        Assert.AreEqual(7, eval.MostBindingUnmetTarget);
    }

    [TestMethod]
    public void Evaluate_SmallAbsoluteGapButTotalRelativeGap_OutranksALargeAbsoluteGap()
    {
        // The whole reason the pick is relative. 400 XP short of 800 is half the bar; 5 duties short
        // of 5 is the entire bar. Naming the 400 just because 400 > 5 would send the player to grind
        // XP while the gate that actually holds them is untouched.
        var eval = PromotionEvaluator.Evaluate(
            new ServiceProgressSnapshot
            {
                Rank = ServiceRank.Sergeant, DaysServed = 60, ServiceXp = 400,
                LeadershipSkill = 5, DutySuccesses = 0, Trust = 6,
            },
            Ladder());

        Assert.AreEqual("dutySuccesses", eval.MostBindingUnmetKey);
        Assert.AreEqual(5, eval.MostBindingUnmetTarget);
    }

    [TestMethod]
    public void Evaluate_NegativeThreshold_BindsWithoutDividingByZero()
    {
        // MinTrust is -10 at the first rank step, and a threshold of 0 is legal too (the first step
        // requires no Leadership at all). The comparison is integer cross-multiplication with a
        // scale floored at 1, so neither can produce a divide — nor a float that could be NaN.
        var eval = PromotionEvaluator.Evaluate(
            new ServiceProgressSnapshot { Rank = ServiceRank.Private, DaysServed = 7, ServiceXp = 100, Trust = -12 },
            Ladder());

        Assert.AreEqual("trust", eval.MostBindingUnmetKey);
        Assert.AreEqual(-10, eval.MostBindingUnmetTarget);
    }

    [TestMethod]
    public void Evaluate_BelowAForgivingFloor_OutranksASmallRelativeGap()
    {
        // Trust -12 against a floor of -10 degenerates to an absolute shortfall of 2, which beats
        // 5-of-100 XP. That is the intended reading: being distrusted by the commander who has to
        // promote you is more binding than a nearly-closed XP gap.
        var eval = PromotionEvaluator.Evaluate(
            new ServiceProgressSnapshot { Rank = ServiceRank.Private, DaysServed = 7, ServiceXp = 95, Trust = -12 },
            Ladder());

        Assert.AreEqual("trust", eval.MostBindingUnmetKey);
    }

    [TestMethod]
    public void Evaluate_EveryThresholdMet_NoBindingKey()
    {
        var eval = PromotionEvaluator.Evaluate(
            new ServiceProgressSnapshot { Rank = ServiceRank.Private, DaysServed = 8, ServiceXp = 120 }, Ladder());

        Assert.IsTrue(eval.Promote);
        Assert.IsNull(eval.MostBindingUnmetKey);
        Assert.AreEqual(0, eval.MostBindingUnmetTarget);
    }

    [TestMethod]
    public void Evaluate_AtTopRank_NoBindingKey()
    {
        var eval = PromotionEvaluator.Evaluate(
            new ServiceProgressSnapshot { Rank = ServiceRank.SergeantMajor }, Ladder());

        Assert.IsTrue(eval.AtTopRank);
        Assert.IsNull(eval.MostBindingUnmetKey);
    }

    [TestMethod]
    public void Evaluate_NullInputs_NoBindingKey()
    {
        Assert.IsNull(PromotionEvaluator.Evaluate(null, Ladder()).MostBindingUnmetKey);
        Assert.IsNull(PromotionEvaluator.Evaluate(new ServiceProgressSnapshot(), null).MostBindingUnmetKey);
    }

    [TestMethod]
    public void Evaluate_UnmetThresholds_StillListsEveryGap()
    {
        // The single-key pick is ADDITIVE. UnmetRequirementKeys is the promotion verdict's own
        // input (Promote == list is empty), so narrowing it would have changed who gets promoted.
        var eval = PromotionEvaluator.Evaluate(
            new ServiceProgressSnapshot
            {
                Rank = ServiceRank.StaffSergeant, DaysServed = 30, ServiceXp = 400,
                LeadershipSkill = 4, DutySuccesses = 1, Trust = 0,
            },
            Ladder());

        Assert.IsFalse(eval.Promote);
        CollectionAssert.AreEquivalent(
            new[] { "days", "xp", "leadership", "dutySuccesses", "trust" },
            eval.UnmetRequirementKeys);
        Assert.IsNotNull(eval.MostBindingUnmetKey);
    }
}

/// <summary>
/// Peek() is the status board's read. Its one job beyond returning the evaluation is to leave the
/// record exactly as it found it — a render path that can promote is a render path that promotes
/// on a draw call.
/// </summary>
[TestClass]
public class PromotionServicePeekTests
{
    private EnlistmentStore _store;
    private EnlistmentContentStore _content;
    private IEnlistmentContentConfigProvider _config;
    private IHeroSkillXpAdapter _skillXp;
    private PromotionService _sut;

    [TestInitialize]
    public void Setup()
    {
        var logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(logger);
        _content = new EnlistmentContentStore(logger);
        _config = Substitute.For<IEnlistmentContentConfigProvider>();
        _config.GetConfig().Returns(EnlistmentContentConfigProvider.BuildDefaults());
        _skillXp = Substitute.For<IHeroSkillXpAdapter>();

        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1";

        _sut = new PromotionService(_content, _config, _skillXp, _store, logger);
    }

    [TestMethod]
    public void Peek_PromotionDue_LeavesTheRankAlone()
    {
        _content.Record.DaysServed = 8;
        _content.Record.ServiceXp = 120;

        var evaluation = _sut.Peek();

        Assert.IsTrue(evaluation.Promote, "the ladder verdict itself is unchanged");
        Assert.AreEqual(ServiceRank.Private, _content.Record.Rank, "Peek must not promote");
    }

    [TestMethod]
    public void Peek_ThenEvaluateAndApply_AgreeOnTheRank()
    {
        // The two share one evaluator call precisely so the numbers a player reads cannot drift
        // from the numbers that grant the rank — the donor's 12-evaluation-site bug.
        _content.Record.DaysServed = 8;
        _content.Record.ServiceXp = 120;

        var peeked = _sut.Peek();
        var applied = _sut.EvaluateAndApply();

        Assert.AreEqual(peeked.ToRank, applied.NewRank);
        Assert.IsTrue(applied.Promoted);
        Assert.AreEqual(ServiceRank.Specialist, _content.Record.Rank);
    }

    [TestMethod]
    public void Peek_ShortOfTheLadder_ReportsTheBindingGate()
    {
        // Staff Sergeant -> Sergeant First Class: 120 days / 1800 XP / Leadership 10 / 8 duties / trust 10.
        _content.Record.Rank = ServiceRank.StaffSergeant;
        _content.Record.DaysServed = 119;
        _content.Record.ServiceXp = 1000;
        _content.Record.DutySuccesses = 8;
        _content.Record.Trust = 10;
        _skillXp.GetSkillValue("main_hero", "Leadership").Returns(9);

        var evaluation = _sut.Peek();

        Assert.IsFalse(evaluation.Promote);
        Assert.AreEqual(ServiceRank.SergeantFirstClass, evaluation.ToRank);
        Assert.AreEqual("xp", evaluation.MostBindingUnmetKey);
        Assert.AreEqual(1800, evaluation.MostBindingUnmetTarget);
    }

    [TestMethod]
    public void Peek_ReadsLeadershipFromTheEnlistedHero()
    {
        // Leadership is the one threshold not held in the content record; if the id it reads ever
        // drifts from the enlisted hero, the ladder silently stalls at Leadership 0.
        _sut.Peek();

        _skillXp.Received().GetSkillValue("main_hero", "Leadership");
    }
}
