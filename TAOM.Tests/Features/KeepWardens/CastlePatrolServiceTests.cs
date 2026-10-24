using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.KeepWardens;

namespace TAOM.Tests.Features.KeepWardens;

/// <summary>The castle patrol decision matrix (#888). The service is pure: it sees only a snapshot and the two providers.</summary>
[TestClass]
public class CastlePatrolServiceTests
{
    private static CastlePatrolService Service(bool enabled = true, float[]? days = null, int[]? sizes = null)
    {
        var config = Substitute.For<ICastlePatrolConfigProvider>();
        config.SpawnDays.Returns(days ?? new[] { 10f, 8f, 6f });
        config.SizeLimits.Returns(sizes ?? new[] { 15, 20, 25 });
        var settings = Substitute.For<ICastlePatrolSettingsProvider>();
        settings.Enabled.Returns(enabled);
        return new CastlePatrolService(config, settings);
    }

    private static CastlePatrolSnapshot Castle(int level = 1, bool rebel = false,
        bool weak = true, bool moderate = true, bool strong = true) =>
        new CastlePatrolSnapshot(IsCastle: true, OwnerIsRebel: rebel, GuardHouseLevel: level,
            HasWeakTemplate: weak, HasModerateTemplate: moderate, HasStrongTemplate: strong);

    [TestMethod]
    public void Plan_ATownOrVillage_IsNeverTheServicesToAnswer()
    {
        var notCastle = new CastlePatrolSnapshot(false, false, 3, true, true, true);

        Assert.AreEqual(default(CastlePatrolPlan), Service().Plan(notCastle, naval: false), "towns keep vanilla's model");
        Assert.AreEqual(default(CastlePatrolPlan), Service().Plan(default, naval: false));
    }

    [DataTestMethod]
    [DataRow(1, 1, 10f)]
    [DataRow(2, 2, 8f)]
    [DataRow(3, 3, 6f)]
    [DataRow(4, 3, 6f)]
    [DataRow(9, 3, 6f)]
    public void Plan_ACastleWithAGuardHouse_IsAllowed_WithStrengthClampedTo3_AndTheConfiguredDays(int level, int strength, float days)
    {
        var plan = Service().Plan(Castle(level), naval: false);

        Assert.IsTrue(plan.Allowed);
        Assert.AreEqual(strength, plan.Strength);
        Assert.AreEqual(days, plan.SpawnDays);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Plan_ACastleWithoutAGuardHouse_IsNotAllowed(int level) =>
        Assert.IsFalse(Service().Plan(Castle(level), naval: false).Allowed);

    [TestMethod]
    public void Plan_Level0Castle_NamesTheWeakTemplate_ButIsNotAllowed()
    {
        var plan = Service().Plan(Castle(0), naval: false);

        Assert.IsFalse(plan.Allowed);
        Assert.AreEqual(1, plan.Strength);
    }

    [TestMethod]
    public void Plan_RebelOwner_KeepsTemplateStrength_ButIsNotAllowed()
    {
        var plan = Service().Plan(Castle(3, rebel: true), naval: false);

        Assert.IsFalse(plan.Allowed);
        Assert.AreEqual(3, plan.Strength);
    }

    [TestMethod]
    public void Plan_SettingOff_KeepsTemplateStrength_ButIsNotAllowed()
    {
        var plan = Service(enabled: false).Plan(Castle(2), naval: false);

        Assert.IsFalse(plan.Allowed);
        Assert.AreEqual(2, plan.Strength, "a live patrol still needs its template when the switch is off");
    }

    [TestMethod]
    public void Plan_ANavalRequest_IsNotAllowed_AndHasNoStrength()
    {
        var plan = Service().Plan(Castle(2), naval: true);

        Assert.IsFalse(plan.Allowed);
        Assert.AreEqual(0, plan.Strength);
    }

    [TestMethod]
    public void Plan_ATown_HasNoStrength() =>
        Assert.AreEqual(0, Service().Plan(new CastlePatrolSnapshot(false, false, 3, true, true, true), naval: false).Strength);

    [TestMethod]
    public void Plan_AMissingTemplate_ForTheClampedStrength_IsNotAllowed_AndHasNoStrength()
    {
        foreach (var snapshot in new[] { Castle(1, weak: false), Castle(2, moderate: false), Castle(3, strong: false), Castle(7, strong: false) })
        {
            var plan = Service().Plan(snapshot, naval: false);
            Assert.IsFalse(plan.Allowed, snapshot.ToString());
            Assert.AreEqual(0, plan.Strength, snapshot + " (level 7 clamps to the strong template)");
        }

        Assert.IsTrue(Service().Plan(Castle(1, moderate: false, strong: false), naval: false).Allowed, "only the clamped tier matters");
    }

    [TestMethod]
    public void Plan_ReadsTheConfiguredDays()
    {
        var plan = Service(days: new[] { 12f, 9f, 3f }).Plan(Castle(2), naval: false);

        Assert.AreEqual(9f, plan.SpawnDays);
    }

    [DataTestMethod]
    [DataRow(1, 15)]
    [DataRow(2, 20)]
    [DataRow(3, 25)]
    [DataRow(0, 15)]
    [DataRow(8, 25)]
    public void SizeLimitOverride_ReplacesAZeroBase_ByClampedLevel(int level, int expected) =>
        Assert.AreEqual(expected, Service().SizeLimitOverride(level, baseResult: 0f));

    [TestMethod]
    public void SizeLimitOverride_LeavesANonZeroBaseAlone_AndNonCastlePatrols()
    {
        Assert.AreEqual(0, Service().SizeLimitOverride(2, baseResult: 20f), "a base that already counts is not replaced");
        Assert.AreEqual(0, Service().SizeLimitOverride(CastlePatrolSnapshot.NotACastlePatrol, baseResult: 0f));
    }

    [TestMethod]
    public void SizeLimitOverride_NaNBase_IsReplaced_ForACastlePatrol_AndZeroForAnyOther()
    {
        // NaN is not above 0, so the 0-base test does not mistake it for a counted limit.
        Assert.AreEqual(20, Service().SizeLimitOverride(2, float.NaN));
        Assert.AreEqual(0, Service().SizeLimitOverride(CastlePatrolSnapshot.NotACastlePatrol, float.NaN));
    }

    [TestMethod]
    public void SizeLimitOverride_IgnoresTheSetting_ALivePatrolNeverDropsToZero() =>
        Assert.AreEqual(20, Service(enabled: false).SizeLimitOverride(2, baseResult: 0f));

    [TestMethod]
    public void SizeLimitOverride_UsesTheConfiguredLimits() =>
        Assert.AreEqual(40, Service(sizes: new[] { 30, 40, 50 }).SizeLimitOverride(2, baseResult: 0f));
}
