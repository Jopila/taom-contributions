using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Domain;
using TAOM.Features.FarWatch;
using TAOM.Features.UnderTheEaves;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Core.Domain;

/// <summary>
/// The composition rules behind <c>TaomMapVisibilityModel.GetPartySpottingRange</c> (docs/features/far-watch.md "The
/// seam"): factors from every contributor share one sum, a penalty is allowed but all penalties together stop at -0.5, a
/// non-finite value adds nothing, and a contributor may lift vanilla's cap by at most <see cref="SpottingContributorComposer.MaxCapBonus"/>
/// (the largest bonus wins, they never add). The <c>Compose</c> rows are pure data and run anywhere. The <c>Apply</c> rows build an
/// engine <c>ExplainedNumber</c> and carry <c>RequiresGame</c>; the first of them are the lookout's behaviour written and run green
/// against the old contributor loop before it was replaced (a positive factor gives exactly the number it always gave), and must not
/// move. The one old behaviour that changed on purpose, a negative factor (once ignored, now a penalty), has its own rows.
/// </summary>
[TestClass]
public class SpottingContributorComposerTests
{
    private const float Cap = 60f;

    private static IPartySpottingContributor Contributor(float factor, float capBonus = 0f)
    {
        var c = Substitute.For<IPartySpottingContributor>();
        c.GetSpottingRangeBonusFactor(Arg.Any<MobileParty>()).Returns(factor);
        c.GetSpottingRangeCapBonus(Arg.Any<MobileParty>()).Returns(capBonus);
        return c;
    }

    private static SpottingAdjustment Compose(params IPartySpottingContributor[] contributors) =>
        new SpottingContributorComposer(contributors).Compose(null!);

    // ---- Compose: pure data

    [TestMethod]
    public void NoContributors_AdjustNothing()
    {
        var adj = Compose();

        Assert.AreEqual(0f, adj.Factor);
        Assert.AreEqual(0f, adj.CapBonus);
    }

    [TestMethod]
    public void OnePositiveFactor_PassesThroughUnchanged_AsTheLookoutHasAlwaysDone() =>
        Assert.AreEqual(0.7f, Compose(Contributor(0.7f)).Factor);

    [TestMethod]
    public void APositiveFactorHasNoUpperCutoff() =>
        Assert.AreEqual(6.2f, Compose(Contributor(6.2f)).Factor, 0.0001f);

    [TestMethod]
    public void PositiveFactors_Add() =>
        Assert.AreEqual(0.5f, Compose(Contributor(0.3f), Contributor(0.2f)).Factor, 0.0001f);

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void ANonFiniteFactor_AddsNothing_OnItsOwnAndBesideAGoodOne(float bad)
    {
        Assert.AreEqual(0f, Compose(Contributor(bad)).Factor);
        Assert.AreEqual(0.4f, Compose(Contributor(bad), Contributor(0.4f)).Factor, 0.0001f);
        Assert.AreEqual(0.4f, Compose(Contributor(0.4f), Contributor(bad)).Factor, 0.0001f);
    }

    [TestMethod]
    public void OnePenalty_Counts() =>
        Assert.AreEqual(-0.35f, Compose(Contributor(-0.35f)).Factor, 0.0001f);

    [TestMethod]
    public void APenaltyAndABonus_Net()
    {
        // A scout away on a settlement ride with a lookout raised: +0.7 and -0.35.
        Assert.AreEqual(0.35f, Compose(Contributor(0.7f), Contributor(-0.35f)).Factor, 0.0001f);
    }

    [TestMethod]
    public void AllPenaltiesTogether_StopAtMinusHalf()
    {
        Assert.AreEqual(-0.5f, Compose(Contributor(-0.35f), Contributor(-0.35f)).Factor, 0.0001f);
        Assert.AreEqual(-0.5f, Compose(Contributor(-9f)).Factor, 0.0001f);
    }

    [TestMethod]
    public void TheFloor_AppliesToThePenaltiesOnly_NotToTheBonuses()
    {
        // 0.7 - 0.9 would net -0.2, but the penalties alone floor at -0.5 first: 0.7 - 0.5.
        Assert.AreEqual(0.2f, Compose(Contributor(0.7f), Contributor(-0.9f)).Factor, 0.0001f);
    }

    [TestMethod]
    public void APenaltyExactlyAtTheFloor_Passes() =>
        Assert.AreEqual(-0.5f, Compose(Contributor(-0.5f)).Factor, 0.0001f);

    [TestMethod]
    public void ASumThatOverflows_IsRefused_NotHandedToTheEngine()
    {
        Assert.AreEqual(0f, Compose(Contributor(float.MaxValue), Contributor(float.MaxValue)).Factor);
    }

    [TestMethod]
    public void CapBonuses_TakeTheLargest_NeverTheSum() =>
        Assert.AreEqual(0.2f, Compose(Contributor(0f, 0.1f), Contributor(0f, 0.2f)).CapBonus, 0.0001f);

    [TestMethod]
    public void ACapBonus_StopsAtTheCeiling()
    {
        Assert.AreEqual(SpottingContributorComposer.MaxCapBonus, Compose(Contributor(0f, 2.5f)).CapBonus);
        Assert.AreEqual(SpottingContributorComposer.MaxCapBonus, Compose(Contributor(0f, 0.5f)).CapBonus, "the scout asks for 0.5 and gets the ceiling");
        Assert.AreEqual(SpottingContributorComposer.MaxCapBonus, Compose(Contributor(0f, SpottingContributorComposer.MaxCapBonus)).CapBonus);
    }

    /// <summary>Pins the value of the ceiling only (0.29, a cap of 77.4); the reason for it is a judgement, with an in-game A/B owed.</summary>
    [TestMethod]
    public void TheCeiling_IsZeroPointTwoNine_SoTheCapIsSeventySevenPointFour()
    {
        Assert.AreEqual(0.29f, SpottingContributorComposer.MaxCapBonus);
        Assert.AreEqual(77.4f, Cap * (1f + SpottingContributorComposer.MaxCapBonus), 0.001f);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-0.5f)]
    [DataRow(0f)]
    public void ACapBonusThatIsNotAFinitePositive_IsIgnored(float bad)
    {
        Assert.AreEqual(0f, Compose(Contributor(0f, bad)).CapBonus);
        Assert.AreEqual(0.25f, Compose(Contributor(0f, bad), Contributor(0f, 0.25f)).CapBonus, 0.0001f);
    }

    [TestMethod]
    public void TheFactorAndTheCapBonus_AreIndependentMembers()
    {
        var adj = Compose(Contributor(0.5f, 0.5f), Contributor(-0.35f));

        Assert.AreEqual(0.15f, adj.Factor, 0.0001f);
        Assert.AreEqual(SpottingContributorComposer.MaxCapBonus, adj.CapBonus, 0.0001f);
    }

    // ---- Under the Eaves (#849): the forest penalty beside the other contributors, Compose rows are pure data

    private static IPartySpottingContributor Forest() => Contributor(UnderTheEavesRule.Factor(true, false, false));

    private static IPartySpottingContributor ForestWithKeenSight() => Contributor(UnderTheEavesRule.Factor(true, false, true));

    [TestMethod]
    public void TheForestPenalty_WithTheLookout_Nets_PlusFourTenths() =>
        Assert.AreEqual(0.4f, Compose(Contributor(0.7f), Forest()).Factor, 0.0001f);

    [TestMethod]
    public void TheForestPenalty_WithFarWatchsSurroundingsRide_Nets_PlusTwoTenths() =>
        Assert.AreEqual(0.2f, Compose(Contributor(FarWatchMath.RampMax), Forest()).Factor, 0.0001f);

    [TestMethod]
    public void TheForestPenalty_WithFarWatchsSettlementRide_IsFlooredAtMinusHalf()
    {
        // -0.35 and -0.3 would be -0.65; the penalties alone stop at -0.5, in either order.
        Assert.AreEqual(-0.5f, Compose(Contributor(FarWatchMath.SettlementPenalty), Forest()).Factor, 0.0001f);
        Assert.AreEqual(-0.5f, Compose(Forest(), Contributor(FarWatchMath.SettlementPenalty)).Factor, 0.0001f);
    }

    [TestMethod]
    public void TheKeenSightForestPenalty_WithFarWatchsSettlementRide_LandsExactlyOnTheFloor()
    {
        // -0.15 and -0.35: the two penalties sum to the floor without being trimmed.
        float sum = UnderTheEavesRule.Factor(true, false, true) + FarWatchMath.SettlementPenalty;

        Assert.AreEqual(SpottingContributorComposer.MaxPenalty, sum, 0.00001f);
        Assert.AreEqual(SpottingContributorComposer.MaxPenalty,
            Compose(ForestWithKeenSight(), Contributor(FarWatchMath.SettlementPenalty)).Factor, 0.00001f);
    }

    // ---- The per-query path allocates nothing

    /// <summary>A hand-written contributor: an NSubstitute substitute records every call, which allocates by itself.</summary>
    private sealed class FixedContributor : IPartySpottingContributor
    {
        public float GetSpottingRangeBonusFactor(MobileParty party) => 0.7f;

        public float GetSpottingRangeCapBonus(MobileParty party) => 0.5f;
    }

    [TestMethod]
    public void Compose_AllocatesNothing_PerQuery()
    {
        var counter = typeof(System.GC).GetMethod("GetAllocatedBytesForCurrentThread", System.Type.EmptyTypes);
        Assert.IsNotNull(counter, "the runtime must expose GC.GetAllocatedBytesForCurrentThread");
        var allocatedBytes = (System.Func<long>)System.Delegate.CreateDelegate(typeof(System.Func<long>), counter!);
        var sut = new SpottingContributorComposer(new IPartySpottingContributor[] { new FixedContributor(), new FixedContributor() });
        for (int i = 0; i < 5; i++) sut.Compose(null!);

        long before = allocatedBytes();
        float sink = 0f;
        for (int i = 0; i < 1000; i++) sink += sut.Compose(null!).Factor;
        long allocated = allocatedBytes() - before;

        Assert.AreEqual(0L, allocated);
        Assert.AreEqual(1400f, sink, 1f, "and it did the work");
    }

    // ---- The model hands the number to the composer

    [TestMethod]
    public void TheModel_AppliesTheComposer_AndKeepsNoContributorLoopOfItsOwn()
    {
        var src = RepoPaths.ReadSource("Main/Features/CareerSystem/Models/TaomMapVisibilityModel.cs", stripComments: true);

        StringAssert.Contains(src, "_composer.Apply(ref result, party, MaximumSeeingRange(), includeDescriptions)");
        StringAssert.Contains(src, "new SpottingContributorComposer(");
        Assert.IsFalse(src.Contains("GetSpottingRangeBonusFactor"), "a second loop over the contributors would skip the penalty floor and the cap");
        Assert.IsFalse(src.Contains("AddFactor("), "the model adds nothing to the number itself");
    }

    // ---- Apply: what the model does with the adjustment (builds an engine ExplainedNumber)

    private static ExplainedNumber VanillaNumber(float baseRange, float perkFactors)
    {
        var n = new ExplainedNumber(baseRange);
        n.AddFactor(perkFactors);
        n.LimitMax(Cap);
        return n;
    }

    private static float Apply(ExplainedNumber vanilla, params IPartySpottingContributor[] contributors)
    {
        new SpottingContributorComposer(contributors).Apply(ref vanilla, null!, Cap, includeDescriptions: false);
        return vanilla.ResultNumber;
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_NoContributors_LeavesVanillasNumberAlone() =>
        Assert.AreEqual(25.2f, Apply(VanillaNumber(21f, 0.2f)), 0.001f);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_OnePositiveContributor_AddsItsFactorToTheSharedSum() =>
        Assert.AreEqual(39.9f, Apply(VanillaNumber(21f, 0.2f), Contributor(0.7f)), 0.001f);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_TwoPositiveContributors_AddTogether() =>
        Assert.AreEqual(21f * 1.5f, Apply(VanillaNumber(21f, 0f), Contributor(0.3f), Contributor(0.2f)), 0.001f);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_APositiveContributorThatPushesPastSixty_StaysClampedToVanillasCap() =>
        Assert.AreEqual(60f, Apply(VanillaNumber(30f, 0.65f), Contributor(1.7f)), 0.001f);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_ANanOrInfiniteFactor_AddsNothing()
    {
        Assert.AreEqual(25.2f, Apply(VanillaNumber(21f, 0.2f), Contributor(float.NaN)), 0.001f);
        Assert.AreEqual(25.2f, Apply(VanillaNumber(21f, 0.2f), Contributor(float.PositiveInfinity)), 0.001f);
        Assert.AreEqual(25.2f, Apply(VanillaNumber(21f, 0.2f), Contributor(float.NegativeInfinity)), 0.001f);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_AZeroFactor_AddsNothing() =>
        Assert.AreEqual(25.2f, Apply(VanillaNumber(21f, 0.2f), Contributor(0f)), 0.001f);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_ANegativeFactor_LowersTheNumber_AndTwoStopAtMinusHalf()
    {
        // The scout's -0.35 on a settlement ride: 21 x (1 + 0.2 - 0.35) = 17.85.
        Assert.AreEqual(17.85f, Apply(VanillaNumber(21f, 0.2f), Contributor(-0.35f)), 0.001f);
        // Two penalties together cannot take more than half of the base: 21 x (1 + 0.2 - 0.5) = 14.7.
        Assert.AreEqual(14.7f, Apply(VanillaNumber(21f, 0.2f), Contributor(-0.35f), Contributor(-0.35f)), 0.001f);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_SightBelowTheCap_GetsTheScoutsWholeHalf()
    {
        // 21 x (1 + 0.2 + 0.5) = 35.7: the ceiling on the cap never trims a factor that stays under it.
        Assert.AreEqual(35.7f, Apply(VanillaNumber(21f, 0.2f), Contributor(0.5f, capBonus: 0.5f)), 0.001f);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_ACapBonus_RaisesVanillasCap_ToTheCeilingAtMost()
    {
        // Scout 300 with every day perk: 30 x (1 + 0.65 + 0.5) = 64.5, past 60, under 60 x 1.29 = 77.4.
        Assert.AreEqual(64.5f, Apply(VanillaNumber(30f, 0.65f), Contributor(0.5f, capBonus: 0.5f)), 0.001f);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_TheRaisedCap_StillClamps_AtSeventySevenPointFour() =>
        Assert.AreEqual(77.4f, Apply(VanillaNumber(30f, 2.35f), Contributor(0.5f, capBonus: 0.5f)), 0.001f);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_WithNoCapBonus_TheCapStaysAtVanillas() =>
        Assert.AreEqual(60f, Apply(VanillaNumber(30f, 2.35f), Contributor(0.5f)), 0.001f);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_TheSameBonusWithoutTheCap_StaysAtSixtyEvenWhenTheSumIsHigher()
    {
        // The cap eats the buff exactly as the design's table says, until the cap is raised.
        Assert.AreEqual(60f, Apply(VanillaNumber(30f, 0.65f), Contributor(0.5f)), 0.001f);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_TheCapFollowsTheModelsMaximum_NotAConstant()
    {
        var n = VanillaNumber(30f, 2.35f);
        new SpottingContributorComposer(new[] { Contributor(0f, 0.25f) }).Apply(ref n, null!, 40f, includeDescriptions: false);

        Assert.AreEqual(50f, n.ResultNumber, 0.001f, "40 x 1.25");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_ADescribedNumber_KeepsItsExplanation_AndTheNewCap()
    {
        var n = new ExplainedNumber(30f, includeDescriptions: true);
        n.AddFactor(2.35f);
        n.LimitMax(Cap);

        new SpottingContributorComposer(new[] { Contributor(0.5f, 0.5f) }).Apply(ref n, null!, Cap, includeDescriptions: true);

        Assert.IsTrue(n.IncludeDescriptions);
        Assert.AreEqual(77.4f, n.ResultNumber, 0.001f);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_ADescribedNumber_NamesTheRaisedCapLineWithVanillasLimitLabel()
    {
        var n = new ExplainedNumber(30f, includeDescriptions: true);
        n.AddFactor(2.35f);
        n.LimitMax(Cap, new TaleWorlds.Localization.TextObject("{=6qv6Hdww}Limit"));

        new SpottingContributorComposer(new[] { Contributor(0f, 0.5f) }).Apply(ref n, null!, Cap, includeDescriptions: true);

        var lines = n.GetLines();
        var cap = lines[lines.Count - 1];
        Assert.AreEqual("Limit", cap.name, "the tooltip keeps vanilla's own name for the line");
        Assert.AreEqual(77.4f - 100.5f, cap.number, 0.001f, "and it clamps at the raised cap");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_TheForestPenalty_WithTheLookout_GivesTwentyFivePointTwo() =>
        Assert.AreEqual(25.2f, Apply(VanillaNumber(18f, 0f), Contributor(0.7f), Forest()), 0.001f);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_TheForestPenalty_AloneOnAScoutingHundredParty_GivesTwelvePointSix() =>
        Assert.AreEqual(12.6f, Apply(VanillaNumber(18f, 0f), Forest()), 0.001f);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_TheForestPenalty_WithFarWatchsSettlementRide_FloorsAtHalfTheBase() =>
        Assert.AreEqual(9f, Apply(VanillaNumber(18f, 0f), Contributor(FarWatchMath.SettlementPenalty), Forest()), 0.001f);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_TheForestPenalty_WithASurroundingsRide_NetsPlusTwoTenths() =>
        Assert.AreEqual(21.6f, Apply(VanillaNumber(18f, 0f), Contributor(FarWatchMath.RampMax), Forest()), 0.001f);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_TheKeenSightForestPenalty_AloneGivesFifteenPointThree() =>
        Assert.AreEqual(15.3f, Apply(VanillaNumber(18f, 0f), ForestWithKeenSight()), 0.001f);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_TheKeenSightForestPenalty_WithFarWatchsSettlementRide_LandsOnHalfTheBase() =>
        Assert.AreEqual(9f, Apply(VanillaNumber(18f, 0f), ForestWithKeenSight(), Contributor(FarWatchMath.SettlementPenalty)), 0.001f);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_TheForestPenalty_ASumPastSixty_StaysAtSixty()
    {
        // 30 x (1 + 0.65 + 1.0 - 0.3) = 70.5, past the cap, so the cap absorbs the forest cut.
        Assert.AreEqual(60f, Apply(VanillaNumber(30f, 0.65f), Contributor(1.0f), Forest()), 0.001f);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_NoContributors_WritesTheSameCapVanillaWrote()
    {
        var n = VanillaNumber(30f, 2.35f);
        new SpottingContributorComposer(System.Array.Empty<IPartySpottingContributor>()).Apply(ref n, null!, Cap, includeDescriptions: false);

        Assert.AreEqual(60f, n.LimitMaxValue);
    }
}
