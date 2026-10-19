using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;
using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// The gate of the horse stand: defender side, foot to stand with, the enemy's melee cavalry a
/// large enough share of its army, and the enemy outnumbering us. Every comparison is a positive
/// requirement, so a NaN ratio from the engine fails the gate (csharp-architecture.md,
/// "Engine-Float Decision Gates").
/// </summary>
[TestClass]
public class HorseStandTests
{
    private static readonly HorseStandTunables Tunables = new HorseStandTunables(enemyCavalryShare: 0.5f, outnumberedRatio: 1f, polearmFavor: 2f);

    private static TeamQuerySnapshot Army(
        bool defender = true, bool hasInfantry = true, float enemyCavalry = 0.6f, int members = 200, int enemies = 300, int? allies = null) =>
        new TeamQuerySnapshot(
            memberCount: members, enemyUnitCount: enemies, infantryRatio: 0.7f, rangedRatio: 0.2f, cavalryRatio: 0.1f,
            rangedCavalryRatio: 0f, remainingPowerRatio: 1f, notEngagingAdvantage: 1f,
            hasInfantry: hasInfantry, hasArchers: true, hasCavalry: true, isDefenseApplicable: true, ringFits: true,
            isDefender: defender, infantryCount: 140, throwingRatio: 0f, hasVanguard: false, infantryTotal: 140,
            enemyCavalryRatio: enemyCavalry, allyUnitCount: allies ?? members);

    [TestMethod]
    public void Applies_DefenderWithFootAgainstOutnumberingHorse_IsTrue()
    {
        var s = Army();
        Assert.IsTrue(HorseStand.Applies(in s, in Tunables));
    }

    [TestMethod]
    public void Applies_Attacker_IsFalse()
    {
        var s = Army(defender: false);
        Assert.IsFalse(HorseStand.Applies(in s, in Tunables));
    }

    [TestMethod]
    public void Applies_NoInfantry_IsFalse()
    {
        var s = Army(hasInfantry: false);
        Assert.IsFalse(HorseStand.Applies(in s, in Tunables));
    }

    [TestMethod]
    public void Applies_EnemyCavalryShareAtTheThreshold_IsTrue()
    {
        var s = Army(enemyCavalry: 0.5f);
        Assert.IsTrue(HorseStand.Applies(in s, in Tunables));
    }

    [TestMethod]
    public void Applies_EnemyCavalryShareJustBelowTheThreshold_IsFalse()
    {
        var s = Army(enemyCavalry: 0.49f);
        Assert.IsFalse(HorseStand.Applies(in s, in Tunables));
    }

    [TestMethod]
    public void Applies_NaNEnemyCavalryRatio_IsFalse()
    {
        var s = Army(enemyCavalry: float.NaN);
        Assert.IsFalse(HorseStand.Applies(in s, in Tunables));
    }

    [TestMethod]
    public void Applies_InfiniteEnemyCavalryRatio_IsFalse()
    {
        var s = Army(enemyCavalry: float.PositiveInfinity);
        Assert.IsFalse(HorseStand.Applies(in s, in Tunables));
    }

    [TestMethod]
    public void Applies_EnemyExactlyAsManyAsUs_IsFalse()
    {
        // Strictly more than members * ratio: parity is not outnumbered.
        var s = Army(members: 200, enemies: 200);
        Assert.IsFalse(HorseStand.Applies(in s, in Tunables));
    }

    [TestMethod]
    public void Applies_EnemyOneMoreThanUs_IsTrue()
    {
        var s = Army(members: 200, enemies: 201);
        Assert.IsTrue(HorseStand.Applies(in s, in Tunables));
    }

    [TestMethod]
    public void Applies_FewerEnemiesThanUs_IsFalse()
    {
        var s = Army(members: 300, enemies: 200);
        Assert.IsFalse(HorseStand.Applies(in s, in Tunables));
    }

    [TestMethod]
    public void Applies_AlliesCoverTheEnemyHostSideForSide_IsFalse()
    {
        // Our own team is 50 men, but the side (us and allies) is 250 against 200: not outnumbered.
        var s = Army(members: 50, allies: 250, enemies: 200);
        Assert.IsFalse(HorseStand.Applies(in s, in Tunables));
    }

    [TestMethod]
    public void Applies_EnemyOutnumbersTheWholeSide_IsTrue()
    {
        var s = Army(members: 50, allies: 150, enemies: 200);
        Assert.IsTrue(HorseStand.Applies(in s, in Tunables));
    }

    [TestMethod]
    public void PolicyFor_SwitchOff_IsNull()
        => Assert.IsNull(HorseStand.PolicyFor(enabled: false, new HorseStandPolicy(true), isDefender: true, isPlayerTeam: false));

    [TestMethod]
    public void PolicyFor_PlayerTeam_IsNull()
        => Assert.IsNull(HorseStand.PolicyFor(enabled: true, new HorseStandPolicy(true), isDefender: true, isPlayerTeam: true));

    [TestMethod]
    public void PolicyFor_Attacker_IsNull()
        => Assert.IsNull(HorseStand.PolicyFor(enabled: true, new HorseStandPolicy(true), isDefender: false, isPlayerTeam: false));

    [TestMethod]
    public void PolicyFor_AiDefenderWithTheSwitchOn_IsTheCulturesPolicy()
    {
        var policy = new HorseStandPolicy(dismount: false);
        Assert.AreSame(policy, HorseStand.PolicyFor(enabled: true, policy, isDefender: true, isPlayerTeam: false));
    }

    [TestMethod]
    public void PolicyFor_CultureWithoutABlock_IsNull()
        => Assert.IsNull(HorseStand.PolicyFor(enabled: true, null, isDefender: true, isPlayerTeam: false));

    [TestMethod]
    public void Applies_OutnumberedRatioScaledEdge_FlipsPastTheEdge()
    {
        var wide = new HorseStandTunables(enemyCavalryShare: 0.5f, outnumberedRatio: 1.5f, polearmFavor: 2f);
        var atEdge = Army(members: 200, enemies: 300);
        var past = Army(members: 200, enemies: 301);

        Assert.IsFalse(HorseStand.Applies(in atEdge, in wide));
        Assert.IsTrue(HorseStand.Applies(in past, in wide));
    }

    [TestMethod]
    public void Applies_LowerShareTunable_AdmitsASmallerCavalryShare()
    {
        var low = new HorseStandTunables(enemyCavalryShare: 0.2f, outnumberedRatio: 1f, polearmFavor: 2f);
        var s = Army(enemyCavalry: 0.25f);

        Assert.IsFalse(HorseStand.Applies(in s, in Tunables));
        Assert.IsTrue(HorseStand.Applies(in s, in low));
    }

    [TestMethod]
    public void Applies_SnapshotBuiltWithoutTheRatio_IsFalse()
    {
        var s = new TeamQuerySnapshot(
            memberCount: 200, enemyUnitCount: 300, infantryRatio: 0.7f, rangedRatio: 0.2f, cavalryRatio: 0.1f,
            rangedCavalryRatio: 0f, remainingPowerRatio: 1f, notEngagingAdvantage: 1f,
            hasInfantry: true, hasArchers: true, hasCavalry: true, isDefenseApplicable: true, ringFits: true, isDefender: true);

        Assert.AreEqual(0f, s.EnemyCavalryRatio);
        Assert.IsFalse(HorseStand.Applies(in s, in Tunables));
    }
}
