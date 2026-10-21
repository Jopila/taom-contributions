using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.Execution;
using TAOM.Features.FireAndTribute;

namespace TAOM.Tests.Features.FireAndTribute;

[TestClass]
public class FireAndTributeMathTests
{
    private static readonly FactionSide[] FreeOnly = { FactionSide.Free };
    private static readonly FactionSide[] FreeAndEvil = { FactionSide.Free, FactionSide.Evil };
    private static readonly string[] NoPreference = Array.Empty<string>();

    private static TargetCandidate K(string id, FactionSide side, float strength) => new(id, side, strength);

    // ---- target picker ---------------------------------------------------------------------------------------------

    [TestMethod]
    public void PickTarget_NoCandidates_IsNull()
    {
        Assert.IsNull(FireAndTributeMath.PickTarget(new TargetCandidate[0], FreeOnly, NoPreference));
    }

    [TestMethod]
    public void PickTarget_NullCandidates_IsNull()
    {
        Assert.IsNull(FireAndTributeMath.PickTarget(null!, FreeOnly, NoPreference));
    }

    [TestMethod]
    public void PickTarget_NoCandidateOfATargetSide_IsNull()
    {
        var kingdoms = new[] { K("mordor", FactionSide.Evil, 900f), K("umbar", FactionSide.Neutral, 500f) };

        Assert.IsNull(FireAndTributeMath.PickTarget(kingdoms, FreeOnly, NoPreference));
    }

    [TestMethod]
    public void PickTarget_NoPreference_IsTheStrongestOfTheTargetSides()
    {
        var kingdoms = new[] { K("rohan", FactionSide.Free, 300f), K("gondor", FactionSide.Free, 800f), K("mordor", FactionSide.Evil, 9000f) };

        Assert.AreEqual("gondor", FireAndTributeMath.PickTarget(kingdoms, FreeOnly, NoPreference));
    }

    [TestMethod]
    public void PickTarget_EqualStrength_BreaksTheTieByOrdinalId()
    {
        var kingdoms = new[] { K("rohan", FactionSide.Free, 500f), K("erebor", FactionSide.Free, 500f), K("gondor", FactionSide.Free, 500f) };

        Assert.AreEqual("erebor", FireAndTributeMath.PickTarget(kingdoms, FreeOnly, NoPreference));
    }

    [TestMethod]
    public void PickTarget_TheTieBreakDoesNotDependOnListOrder()
    {
        var a = new[] { K("b", FactionSide.Free, 5f), K("a", FactionSide.Free, 5f) };
        var b = a.Reverse().ToArray();

        Assert.AreEqual("a", FireAndTributeMath.PickTarget(a, FreeOnly, NoPreference));
        Assert.AreEqual("a", FireAndTributeMath.PickTarget(b, FreeOnly, NoPreference));
    }

    [TestMethod]
    public void PickTarget_PreferredBeatsStrongest_InTheListedOrder()
    {
        var kingdoms = new[] { K("rohan", FactionSide.Free, 100f), K("gondor", FactionSide.Free, 800f), K("erebor", FactionSide.Free, 50f) };

        Assert.AreEqual("erebor", FireAndTributeMath.PickTarget(kingdoms, FreeOnly, new[] { "erebor", "rohan" }));
        Assert.AreEqual("rohan", FireAndTributeMath.PickTarget(kingdoms, FreeOnly, new[] { "dale", "rohan", "erebor" }));
    }

    [TestMethod]
    public void PickTarget_APreferredKingdomNotInTheCandidates_IsSkipped()
    {
        var kingdoms = new[] { K("rohan", FactionSide.Free, 100f), K("gondor", FactionSide.Free, 800f) };

        Assert.AreEqual("gondor", FireAndTributeMath.PickTarget(kingdoms, FreeOnly, new[] { "erebor" }));
    }

    [TestMethod]
    public void PickTarget_APreferredKingdomOfAnotherSide_IsIgnored()
    {
        var kingdoms = new[] { K("mordor", FactionSide.Evil, 100f), K("gondor", FactionSide.Free, 800f) };

        Assert.AreEqual("gondor", FireAndTributeMath.PickTarget(kingdoms, FreeOnly, new[] { "mordor" }));
    }

    [TestMethod]
    public void PickTarget_ANeutralHost_ChoosesAcrossBothTargetSides()
    {
        var kingdoms = new[] { K("mordor", FactionSide.Evil, 9000f), K("gondor", FactionSide.Free, 800f), K("umbar", FactionSide.Neutral, 99999f) };

        Assert.AreEqual("mordor", FireAndTributeMath.PickTarget(kingdoms, FreeAndEvil, NoPreference));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(-1f)]
    [DataRow(float.PositiveInfinity)]
    public void PickTarget_AnUnreadableStrength_CountsAsNone_AndNeverBeatsARealOne(float bad)
    {
        var kingdoms = new[] { K("aaa", FactionSide.Free, bad), K("zzz", FactionSide.Free, 1f) };

        Assert.AreEqual("zzz", FireAndTributeMath.PickTarget(kingdoms, FreeOnly, NoPreference));
    }

    [TestMethod]
    public void PickTarget_AllStrengthsUnreadable_StillPicksOne_ByIdOrder()
    {
        var kingdoms = new[] { K("b", FactionSide.Free, float.NaN), K("a", FactionSide.Free, -1f) };

        Assert.AreEqual("a", FireAndTributeMath.PickTarget(kingdoms, FreeOnly, NoPreference));
    }

    // ---- raidable villages -----------------------------------------------------------------------------------------

    private static VillageReading V(string id, string? kingdom, float x = 0f, float y = 0f, float hearth = 400f, bool normal = true) =>
        new VillageReading(true, kingdom, normal, hearth, id, x, y);

    [TestMethod]
    public void IsRaidable_ANormalVillageOfTheKingdomAtOrAboveTheMinimumHearth_IsRaidable()
    {
        Assert.IsTrue(FireAndTributeMath.IsRaidable(V("v", "gondor", hearth: 150f), "gondor", 150f), "the edge counts");
        Assert.IsTrue(FireAndTributeMath.IsRaidable(V("v", "gondor", hearth: 900f), "gondor", 150f));
    }

    [TestMethod]
    public void IsRaidable_EveryDisqualifier_MakesItNotRaidable()
    {
        Assert.IsFalse(FireAndTributeMath.IsRaidable(VillageReading.Missing, "gondor", 150f), "a village the engine no longer has");
        Assert.IsFalse(FireAndTributeMath.IsRaidable(V("v", "gondor", normal: false), "gondor", 150f), "looted, being raided or forced");
        Assert.IsFalse(FireAndTributeMath.IsRaidable(V("v", "rohan"), "gondor", 150f), "another kingdom's");
        Assert.IsFalse(FireAndTributeMath.IsRaidable(V("v", null), "gondor", 150f), "no kingdom at all");
        Assert.IsFalse(FireAndTributeMath.IsRaidable(V("v", "gondor", hearth: 149.9f), "gondor", 150f), "below the hearth floor");
        Assert.IsFalse(FireAndTributeMath.IsRaidable(V("v", "gondor", hearth: float.NaN), "gondor", 150f), "an unreadable hearth");
        Assert.IsFalse(FireAndTributeMath.IsRaidable(V("v", "gondor", hearth: float.PositiveInfinity), "gondor", 150f), "a non-finite hearth");
    }

    [TestMethod]
    public void KingdomsWithRaidable_IsTheSetOfKingdomsThatOwnAtLeastOneRaidableVillage()
    {
        var villages = new[]
        {
            V("a", "gondor"), V("b", "gondor"), V("c", "rohan", normal: false), V("d", "mordor", hearth: 10f),
            V("e", null), V("f", "harad", hearth: float.NaN), V("g", "erebor", hearth: 150f),
        };

        var set = FireAndTributeMath.KingdomsWithRaidable(villages, 150f);

        CollectionAssert.AreEquivalent(new[] { "gondor", "erebor" }, set.ToArray());
    }

    [TestMethod]
    public void KingdomsWithRaidable_NoVillages_IsEmpty()
    {
        Assert.AreEqual(0, FireAndTributeMath.KingdomsWithRaidable(new VillageReading[0], 150f).Count);
        Assert.AreEqual(0, FireAndTributeMath.KingdomsWithRaidable(null!, 150f).Count);
    }

    [TestMethod]
    public void NearestRaidable_IsNearestFirst_ByTheSquaredDistance()
    {
        var villages = new[] { V("far", "gondor", 30f, 0f), V("near", "gondor", 3f, 4f), V("mid", "gondor", 0f, 12f) };

        var ids = FireAndTributeMath.NearestRaidable(villages, "gondor", 0f, 0f, 150f, 3);

        CollectionAssert.AreEqual(new[] { "near", "mid", "far" }, ids.ToArray());
    }

    [TestMethod]
    public void NearestRaidable_EqualDistances_BreakByOrdinalId_NotByListOrder()
    {
        var a = new[] { V("b", "gondor", 5f, 0f), V("a", "gondor", 0f, 5f), V("C", "gondor", -5f, 0f) };
        var b = a.Reverse().ToArray();

        CollectionAssert.AreEqual(new[] { "C", "a", "b" }, FireAndTributeMath.NearestRaidable(a, "gondor", 0f, 0f, 150f, 3).ToArray(), "ordinal: capitals first");
        CollectionAssert.AreEqual(new[] { "C", "a", "b" }, FireAndTributeMath.NearestRaidable(b, "gondor", 0f, 0f, 150f, 3).ToArray());
    }

    [TestMethod]
    public void NearestRaidable_TakesAtMostCount_AndNoneForANonPositiveCount()
    {
        var villages = Enumerable.Range(1, 6).Select(i => V("v" + i, "gondor", i, 0f)).ToArray();

        Assert.AreEqual(3, FireAndTributeMath.NearestRaidable(villages, "gondor", 0f, 0f, 150f, 3).Count);
        Assert.AreEqual(6, FireAndTributeMath.NearestRaidable(villages, "gondor", 0f, 0f, 150f, 99).Count);
        Assert.AreEqual(0, FireAndTributeMath.NearestRaidable(villages, "gondor", 0f, 0f, 150f, 0).Count);
        Assert.AreEqual(0, FireAndTributeMath.NearestRaidable(villages, "gondor", 0f, 0f, 150f, -2).Count);
    }

    [TestMethod]
    public void NearestRaidable_SkipsANaNPosition_AndAnUnreadableQueryPoint()
    {
        var villages = new[] { V("nan_x", "gondor", float.NaN, 0f), V("nan_y", "gondor", 0f, float.NaN), V("inf", "gondor", float.PositiveInfinity, 0f), V("ok", "gondor", 9f, 0f) };

        CollectionAssert.AreEqual(new[] { "ok" }, FireAndTributeMath.NearestRaidable(villages, "gondor", 0f, 0f, 150f, 5).ToArray());
        Assert.AreEqual(0, FireAndTributeMath.NearestRaidable(villages, "gondor", float.NaN, 0f, 150f, 5).Count);
    }

    [TestMethod]
    public void NearestRaidable_LeavesOutLootedLowHearthAndOtherKingdoms()
    {
        var villages = new[]
        {
            V("looted", "gondor", 1f, 0f, normal: false), V("poor", "gondor", 2f, 0f, hearth: 10f), V("rohan", "rohan", 3f, 0f),
            V("nobody", null, 4f, 0f), V("ok", "gondor", 50f, 0f),
        };

        CollectionAssert.AreEqual(new[] { "ok" }, FireAndTributeMath.NearestRaidable(villages, "gondor", 0f, 0f, 150f, 3).ToArray());
    }

    [TestMethod]
    public void NearestRaidable_NoVillages_IsEmpty()
    {
        Assert.AreEqual(0, FireAndTributeMath.NearestRaidable(new VillageReading[0], "gondor", 0f, 0f, 150f, 3).Count);
        Assert.AreEqual(0, FireAndTributeMath.NearestRaidable(null!, "gondor", 0f, 0f, 150f, 3).Count);
    }

    // ---- size ------------------------------------------------------------------------------------------------------

    private static readonly HostTroop[] Troops =
    {
        new("snaga", 0.5f), new("grunt", 0.3f), new("lurker", 0.2f),
    };

    private static float Power(string id) => id switch { "snaga" => 1f, "grunt" => 2f, "lurker" => 4f, _ => float.NaN };

    private static int Men(IReadOnlyList<(string TroopId, int Count)> plan) => plan.Sum(p => p.Count);

    [TestMethod]
    public void PlanHost_SmallTarget_IsLiftedToExactlyMinTroops_ByShare()
    {
        var plan = FireAndTributeMath.PlanHost(Troops, 10f, 0.012f, 80, 200, Power);

        Assert.AreEqual(80, Men(plan));
        Assert.AreEqual(40, plan.Single(p => p.TroopId == "snaga").Count);
        Assert.AreEqual(24, plan.Single(p => p.TroopId == "grunt").Count);
        Assert.AreEqual(16, plan.Single(p => p.TroopId == "lurker").Count);
    }

    [TestMethod]
    public void PlanHost_TheFloorKeepsEveryCountPositive_EvenForATinyShare()
    {
        var troops = new HostTroop[] { new("snaga", 0.98f), new("lurker", 0.02f) };

        var plan = FireAndTributeMath.PlanHost(troops, 0f, 0.012f, 10, 200, Power);

        Assert.AreEqual(10, Men(plan));
        Assert.IsTrue(plan.All(p => p.Count > 0));
    }

    [TestMethod]
    public void PlanHost_TheFloorUsesTheLargestRemaindersAndSumsExactly()
    {
        var troops = new HostTroop[] { new("snaga", 1f / 3f), new("grunt", 1f / 3f), new("lurker", 1f / 3f) };

        var plan = FireAndTributeMath.PlanHost(troops, 0f, 0.012f, 10, 10, Power);

        Assert.AreEqual(10, Men(plan));
        CollectionAssert.AreEquivalent(new[] { 4, 3, 3 }, plan.Select(p => p.Count).ToArray());
    }

    [TestMethod]
    public void PlanHost_MidTarget_SplitsTheStrengthByShare()
    {
        // 12000 * 0.012 = 144 strength: snaga 72 men, grunt ceil(43.2 / 2) = 22, lurker ceil(28.8 / 4) = 8: 102 men.
        var plan = FireAndTributeMath.PlanHost(Troops, 12000f, 0.012f, 80, 200, Power);

        Assert.AreEqual(72, plan.Single(p => p.TroopId == "snaga").Count);
        Assert.AreEqual(22, plan.Single(p => p.TroopId == "grunt").Count);
        Assert.AreEqual(8, plan.Single(p => p.TroopId == "lurker").Count);
        Assert.AreEqual(102, Men(plan));
    }

    [TestMethod]
    public void PlanHost_HugeTarget_IsCappedAtMaxTroops()
    {
        var plan = FireAndTributeMath.PlanHost(Troops, 4_000_000f, 0.012f, 80, 200, Power);

        Assert.AreEqual(200, Men(plan));
        Assert.IsTrue(plan.All(p => p.Count > 0));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-500f)]
    [DataRow(0f)]
    public void PlanHost_AnUnreadableTargetStrength_GivesTheMinimumHost(float strength)
    {
        var plan = FireAndTributeMath.PlanHost(Troops, strength, 0.012f, 80, 200, Power);

        Assert.AreEqual(80, Men(plan));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(-1f)]
    [DataRow(0f)]
    public void PlanHost_AnUnreadableShare_GivesTheMinimumHost(float share)
    {
        var plan = FireAndTributeMath.PlanHost(Troops, 12000f, share, 80, 200, Power);

        Assert.AreEqual(80, Men(plan));
    }

    [TestMethod]
    public void PlanHost_ATroopWithoutAUsablePower_IsLeftOut()
    {
        var troops = new HostTroop[] { new("snaga", 0.5f), new("ghost", 0.5f) };

        var plan = FireAndTributeMath.PlanHost(troops, 10f, 0.012f, 20, 200, Power);

        Assert.AreEqual(1, plan.Count);
        Assert.AreEqual("snaga", plan[0].TroopId);
        Assert.AreEqual(20, plan[0].Count);
    }

    [TestMethod]
    public void PlanHost_NoTroopWithAUsablePower_IsEmpty()
    {
        var plan = FireAndTributeMath.PlanHost(new HostTroop[] { new("ghost", 1f) }, 12000f, 0.012f, 80, 200, Power);

        Assert.AreEqual(0, plan.Count);
    }

    [TestMethod]
    public void PlanHost_NoTroops_IsEmpty()
    {
        Assert.AreEqual(0, FireAndTributeMath.PlanHost(new HostTroop[0], 12000f, 0.012f, 80, 200, Power).Count);
        Assert.AreEqual(0, FireAndTributeMath.PlanHost(null!, 12000f, 0.012f, 80, 200, Power).Count);
    }

    [DataTestMethod]
    [DataRow(0, 10)]
    [DataRow(-5, 10)]
    [DataRow(50, 10)]
    public void PlanHost_BadBounds_AreEmpty(int min, int max)
    {
        Assert.AreEqual(0, FireAndTributeMath.PlanHost(Troops, 12000f, 0.012f, min, max, Power).Count);
    }

    [TestMethod]
    public void PlanHost_MinEqualsMax_GivesExactlyThatMany()
    {
        Assert.AreEqual(90, Men(FireAndTributeMath.PlanHost(Troops, 12000f, 0.012f, 90, 90, Power)));
        Assert.AreEqual(90, Men(FireAndTributeMath.PlanHost(Troops, 10f, 0.012f, 90, 90, Power)));
    }

    [TestMethod]
    public void PlanHost_AHostBetweenTheBounds_StaysBetweenThem_ForManyTargets()
    {
        foreach (float target in new[] { 0f, 500f, 2000f, 5000f, 12000f, 25000f, 47000f, 90000f })
        {
            int men = Men(FireAndTributeMath.PlanHost(Troops, target, 0.012f, 80, 200, Power));
            Assert.IsTrue(men >= 80 && men <= 200, $"target {target}: {men} men");
        }
    }

    // ---- quota -----------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Quota_SmallKingdom_IsTheFloor()
    {
        Assert.AreEqual(6, FireAndTributeMath.Quota(1800f, 2000f, 0, 6, 15));
    }

    [TestMethod]
    public void Quota_RoundsTheRatioAwayFromZeroAtHalf_ThenAddsTheExtra()
    {
        Assert.AreEqual(8, FireAndTributeMath.Quota(15000f, 2000f, 0, 6, 15));
        Assert.AreEqual(10, FireAndTributeMath.Quota(15000f, 2000f, 2, 6, 15));
    }

    [TestMethod]
    public void Quota_HugeKingdom_IsTheCap()
    {
        Assert.AreEqual(15, FireAndTributeMath.Quota(47000f, 2000f, 3, 6, 15));
        Assert.AreEqual(15, FireAndTributeMath.Quota(float.MaxValue, 0.0001f, 100, 6, 15));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(-100f)]
    public void Quota_AnUnreadableStrength_CountsNoRaidsAtAll_SoTheExtraAloneIsClampedToTheFloor(float strength)
    {
        Assert.AreEqual(6, FireAndTributeMath.Quota(strength, 2000f, 0, 6, 15));
        Assert.AreEqual(6, FireAndTributeMath.Quota(strength, 2000f, 2, 6, 15));
        Assert.AreEqual(9, FireAndTributeMath.Quota(strength, 2000f, 9, 6, 15));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(0f)]
    [DataRow(-5f)]
    [DataRow(float.PositiveInfinity)]
    public void Quota_AnUnusableStrengthPerRaid_GivesTheFloor(float perRaid)
    {
        Assert.AreEqual(6, FireAndTributeMath.Quota(15000f, perRaid, 0, 6, 15));
    }

    [TestMethod]
    public void Quota_ANegativeExtra_IsIgnored()
    {
        Assert.AreEqual(8, FireAndTributeMath.Quota(15000f, 2000f, -4, 6, 15));
    }

    [TestMethod]
    public void Quota_BadBounds_NeverGoBelowOne()
    {
        Assert.AreEqual(1, FireAndTributeMath.Quota(15000f, 2000f, 0, 0, 0));
        Assert.AreEqual(9, FireAndTributeMath.Quota(15000f, 2000f, 0, 9, 3));
    }

    // ---- spawn window ----------------------------------------------------------------------------------------------

    [TestMethod]
    public void SpawnWindow_BeforeFullWar_IsClosed_HoweverLateTheDay()
    {
        Assert.IsFalse(FireAndTributeMath.SpawnWindowOpen(true, false, 44, -1, 133, 45, 89));
        Assert.IsFalse(FireAndTributeMath.SpawnWindowOpen(true, false, 44, -1, 5000, 45, 89));
    }

    [TestMethod]
    public void SpawnWindow_InFullWar_OpensOnTheEffectiveDayPlusTheDelay()
    {
        Assert.IsFalse(FireAndTributeMath.SpawnWindowOpen(true, true, 44, 44, 88, 45, 500));
        Assert.IsTrue(FireAndTributeMath.SpawnWindowOpen(true, true, 44, 44, 89, 45, 500));
        Assert.IsTrue(FireAndTributeMath.SpawnWindowOpen(true, true, 44, 44, 200, 45, 500));
    }

    [TestMethod]
    public void SpawnWindow_AfterTheWarEnded_StaysOpen()
    {
        Assert.IsTrue(FireAndTributeMath.SpawnWindowOpen(true, true, 44, 44, 300, 45, 500));
    }

    [TestMethod]
    public void SpawnWindow_ASaveFirstSeenLateInFullWar_CountsTheDelayFromThatDay()
    {
        Assert.IsFalse(FireAndTributeMath.SpawnWindowOpen(true, true, 44, 300, 344, 45, 500));
        Assert.IsTrue(FireAndTributeMath.SpawnWindowOpen(true, true, 44, 300, 345, 45, 500));
    }

    [TestMethod]
    public void SpawnWindow_NoEffectiveDayAndNothingSeen_OpensAfterTheDelayAlone()
    {
        Assert.IsFalse(FireAndTributeMath.SpawnWindowOpen(true, true, null, -1, 44, 45, 500));
        Assert.IsTrue(FireAndTributeMath.SpawnWindowOpen(true, true, null, -1, 45, 45, 500));
    }

    [TestMethod]
    public void SpawnWindow_WithTheWarOfTheRingOff_UsesTheWarlessDayAlone()
    {
        Assert.IsFalse(FireAndTributeMath.SpawnWindowOpen(false, false, null, -1, 88, 45, 89));
        Assert.IsTrue(FireAndTributeMath.SpawnWindowOpen(false, false, null, -1, 89, 45, 89));
        Assert.IsTrue(FireAndTributeMath.SpawnWindowOpen(false, true, 44, 44, 89, 45, 89), "the phase is not read with the war off");
    }

    [TestMethod]
    public void SpawnWindow_ANegativeDelay_IsTreatedAsZero_AndHugeDaysDoNotOverflow()
    {
        Assert.IsTrue(FireAndTributeMath.SpawnWindowOpen(true, true, 44, 44, 44, -10, 500));
        Assert.IsFalse(FireAndTributeMath.SpawnWindowOpen(true, true, int.MaxValue, int.MaxValue, 10, 3650, 500));
    }

    // ---- cooldown --------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CooldownEnd_IsTodayPlusTheCooldown()
    {
        Assert.AreEqual(220, FireAndTributeMath.CooldownEnd(100, 120));
        Assert.AreEqual(100, FireAndTributeMath.CooldownEnd(100, 0));
        Assert.AreEqual(100, FireAndTributeMath.CooldownEnd(100, -7));
    }

    [TestMethod]
    public void CooldownEnd_NeverOverflows()
    {
        Assert.AreEqual(int.MaxValue, FireAndTributeMath.CooldownEnd(int.MaxValue - 5, 120));
    }
}
