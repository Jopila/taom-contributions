using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ArmyTargeting.Hooks;

namespace TAOM.Tests.Features.ArmyTargeting;

/// <summary>
/// Patch49 swallows a <see cref="NullReferenceException"/> from vanilla
/// <c>Army.FindBestGatheringSettlementAndMoveTheLeader</c> only in its two proven dead ends (v1.5.5
/// Army.cs:654-731): a kingdomless army with an active leader (<c>Kingdom.Settlements</c>, :661), and no
/// gathering settlement with a null nearest-fortification fallback (<c>settlement.GatePosition</c>, :728,
/// after vanilla set <c>AiBehaviorObject</c> to that null at :727). Any other NRE is another fault and
/// propagates (NRE guard audit 2026-10-27).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class Patch49GatheringNreRuleTests
{
    [TestMethod]
    public void IsKnownDeadEnd_KingdomlessArmyWithActiveLeader_IsTrue() =>
        Assert.IsTrue(Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.IsKnownDeadEnd(
            kingdomMissing: true, leaderActive: true, objectiveMissing: false, fallbackMissing: () => false));

    [TestMethod]
    public void IsKnownDeadEnd_KingdomlessArmyWithInactiveLeaderAndAFallback_IsFalse() =>
        Assert.IsFalse(Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.IsKnownDeadEnd(
            kingdomMissing: true, leaderActive: false, objectiveMissing: true, fallbackMissing: () => false));

    [TestMethod]
    public void IsKnownDeadEnd_NoObjectiveAndNoFallback_IsTrue() =>
        Assert.IsTrue(Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.IsKnownDeadEnd(
            kingdomMissing: false, leaderActive: true, objectiveMissing: true, fallbackMissing: () => true));

    [TestMethod]
    public void IsKnownDeadEnd_NoObjectiveButAFallbackExists_IsFalse() =>
        Assert.IsFalse(Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.IsKnownDeadEnd(
            kingdomMissing: false, leaderActive: true, objectiveMissing: true, fallbackMissing: () => false));

    [TestMethod]
    public void IsKnownDeadEnd_ObjectiveSet_IsFalse_AndSkipsTheFallbackSearch()
    {
        var searched = false;

        var result = Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.IsKnownDeadEnd(
            kingdomMissing: false, leaderActive: true, objectiveMissing: false,
            fallbackMissing: () => { searched = true; return true; });

        Assert.IsFalse(result);
        Assert.IsFalse(searched, "The fallback search runs only when the objective is missing.");
    }

    [TestMethod]
    public void Finalizer_NreWhoseArmyCannotBeRead_PropagatesSameInstance()
    {
        var nre = new NullReferenceException("not a proven dead end");

        var returned = Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.Finalizer(nre, null!, null!);

        Assert.AreSame(nre, returned, "On any doubt the NRE goes back; it is no longer swallowed by type alone.");
    }
}
