using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.KeepWardens;

namespace TAOM.Tests.Features.KeepWardens;

/// <summary>The security bonus and the AI Guard House build pick (#889). The bonus and the pick are both pure.</summary>
[TestClass]
public class CastlePatrolSecurityAndBuildTests
{
    private static CastlePatrolService Service(bool buildPriority = true, float chance = 0.6f)
    {
        var config = Substitute.For<ICastlePatrolConfigProvider>();
        config.SpawnDays.Returns(new[] { 10f, 8f, 6f });
        config.SizeLimits.Returns(new[] { 25, 40, 56 });
        config.SecurityPerDay.Returns(new[] { 1f, 1.5f, 2f });
        config.GuardHousePriorityChance.Returns(chance);
        var settings = Substitute.For<ICastlePatrolSettingsProvider>();
        settings.Enabled.Returns(true);
        settings.BuildPriorityEnabled.Returns(buildPriority);
        return new CastlePatrolService(config, settings);
    }

    private static CastlePatrolSnapshot Castle(int level, bool patrolAlive, bool isCastle = true) =>
        new(IsCastle: isCastle, OwnerIsRebel: false, GuardHouseLevel: level,
            HasWeakTemplate: true, HasModerateTemplate: true, HasStrongTemplate: true, PatrolAlive: patrolAlive);

    [DataTestMethod]
    [DataRow(1, 1f)]
    [DataRow(2, 1.5f)]
    [DataRow(3, 2f)]
    [DataRow(9, 2f)]
    public void SecurityPerDay_ACastleWithALivePatrol_GainsTheLevelsConfiguredAmount(int level, float expected)
    {
        Assert.AreEqual(expected, Service().SecurityPerDay(Castle(level, patrolAlive: true)));
    }

    [TestMethod]
    public void SecurityPerDay_NoLivePatrol_IsZero_AtEveryLevel()
    {
        foreach (var level in new[] { 1, 2, 3 })
            Assert.AreEqual(0f, Service().SecurityPerDay(Castle(level, patrolAlive: false)), "level " + level);
    }

    [TestMethod]
    public void SecurityPerDay_NoGuardHouse_IsZero()
    {
        Assert.AreEqual(0f, Service().SecurityPerDay(Castle(0, patrolAlive: true)));
    }

    [TestMethod]
    public void SecurityPerDay_ATownOrNothing_IsZero_SoVanillasOwnLineIsNeverDoubled()
    {
        Assert.AreEqual(0f, Service().SecurityPerDay(Castle(3, patrolAlive: true, isCastle: false)));
        Assert.AreEqual(0f, Service().SecurityPerDay(default));
    }

    [TestMethod]
    public void PicksGuardHouse_ALevelZeroGuardHouse_IsAlwaysPicked_WhateverTheRoll()
    {
        Assert.IsTrue(Service().PicksGuardHouse(new GuardHouseCandidate(Eligible: true, Level: 0, Roll: 0.99f)));
        Assert.IsTrue(Service(chance: 0f).PicksGuardHouse(new GuardHouseCandidate(true, 0, 0.99f)));
    }

    [DataTestMethod]
    [DataRow(1, 0.0f, true)]
    [DataRow(2, 0.59f, true)]
    [DataRow(2, 0.6f, false)]
    [DataRow(1, 0.99f, false)]
    public void PicksGuardHouse_ABuiltGuardHouse_IsPickedWhenTheRollIsUnderTheChance(int level, float roll, bool expected)
    {
        Assert.AreEqual(expected, Service(chance: 0.6f).PicksGuardHouse(new GuardHouseCandidate(true, level, roll)));
    }

    [TestMethod]
    public void PicksGuardHouse_AChanceOfOne_AlwaysPicks_AChanceOfZero_NeverAtLevelTwo()
    {
        Assert.IsTrue(Service(chance: 1f).PicksGuardHouse(new GuardHouseCandidate(true, 2, 0.999f)));
        Assert.IsFalse(Service(chance: 0f).PicksGuardHouse(new GuardHouseCandidate(true, 2, 0f)));
    }

    [TestMethod]
    public void PicksGuardHouse_NotEligible_IsNeverPicked()
    {
        Assert.IsFalse(Service().PicksGuardHouse(new GuardHouseCandidate(Eligible: false, Level: 0, Roll: 0f)));
        Assert.IsFalse(Service().PicksGuardHouse(default));
    }

    [TestMethod]
    public void PicksGuardHouse_WithTheSettingOff_IsNeverPicked()
    {
        Assert.IsFalse(Service(buildPriority: false).PicksGuardHouse(new GuardHouseCandidate(true, 0, 0f)));
    }
}
