using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.UnlookedForAid;

namespace TAOM.Tests.Features.UnlookedForAid;

/// <summary>The Help Unlooked-For decision matrix (#872). The service is pure: facts in, verdict out.</summary>
[TestClass]
public class UnlookedForAidServiceTests
{
    private const float Ratio = 0.5f;

    private IUnlookedForAidSettingsProvider _settings = null!;
    private UnlookedForAidService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IUnlookedForAidSettingsProvider>();
        _settings.Enabled.Returns(true);
        var config = Substitute.For<IUnlookedForAidConfigProvider>();
        config.MinStrengthRatio.Returns(Ratio);
        _sut = new UnlookedForAidService(_settings, config);
    }

    private static SiegeAvoidanceFacts Besieger(float own, float camp, bool lord = true, bool playerLed = false) =>
        new SiegeAvoidanceFacts(lord, targetIsBesieger: true, targetInBesiegedSettlement: false, playerLed, own, camp);

    private static SiegeAvoidanceFacts Inside(bool lord = true, bool playerLed = false, float own = 0f, float camp = 0f) =>
        new SiegeAvoidanceFacts(lord, targetIsBesieger: false, targetInBesiegedSettlement: true, playerLed, own, camp);

    [TestMethod]
    public void FeatureOff_KeepsVanillasAnswer()
    {
        _settings.Enabled.Returns(false);

        Assert.IsTrue(_sut.Decide(Besieger(100f, 100f)));
        Assert.IsTrue(_sut.Decide(Inside()));
    }

    [TestMethod]
    public void ANonLordParty_KeepsVanillasAnswer()
    {
        Assert.IsTrue(_sut.Decide(Besieger(100f, 100f, lord: false)));
        Assert.IsTrue(_sut.Decide(Inside(lord: false)));
    }

    [TestMethod]
    public void APlayerLedSiege_KeepsVanillasAvoid_AgainstTheBesieger()
    {
        Assert.IsTrue(_sut.Decide(Besieger(100f, 100f, playerLed: true)));
        Assert.IsTrue(_sut.Decide(Besieger(100f, 0f, playerLed: true)));
    }

    [TestMethod]
    public void APlayerLedSiege_KeepsVanillasAvoid_ForATargetInsideTheSettlement()
    {
        Assert.IsTrue(_sut.Decide(Inside(playerLed: true)));
        Assert.IsFalse(_sut.Decide(Inside(playerLed: false)), "control: the same target is lifted when the player does not lead the siege");
    }

    [TestMethod]
    public void Besieger_RatioAboveTheThreshold_NoLongerAvoided()
        => Assert.IsFalse(_sut.Decide(Besieger(60f, 100f)));

    [TestMethod]
    public void Besieger_RatioExactlyAtTheThreshold_NoLongerAvoided()
        => Assert.IsFalse(_sut.Decide(Besieger(50f, 100f)));

    [TestMethod]
    public void Besieger_RatioBelowTheThreshold_StillAvoided()
        => Assert.IsTrue(_sut.Decide(Besieger(49f, 100f)));

    [TestMethod]
    public void Besieger_ZeroOwnStrength_StillAvoided()
        => Assert.IsTrue(_sut.Decide(Besieger(0f, 100f)));

    [TestMethod]
    public void Besieger_ZeroCamp_NoLongerAvoided()
        => Assert.IsFalse(_sut.Decide(Besieger(0f, 0f)));

    [TestMethod]
    public void Besieger_NegativeCamp_NoLongerAvoided()
        => Assert.IsFalse(_sut.Decide(Besieger(10f, -5f)));

    [DataTestMethod]
    [DataRow(float.NaN, 100f)]
    [DataRow(100f, float.NaN)]
    [DataRow(float.PositiveInfinity, 100f)]
    [DataRow(100f, float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity, 100f)]
    [DataRow(float.NaN, float.NaN)]
    public void Besieger_NonFiniteStrength_KeepsVanillasAnswer(float own, float camp)
        => Assert.IsTrue(_sut.Decide(Besieger(own, camp)));

    [TestMethod]
    public void TargetInsideABesiegedSettlement_NoLongerAvoided_WhateverTheStrengths()
    {
        Assert.IsFalse(_sut.Decide(Inside()));
        Assert.IsFalse(_sut.Decide(Inside(own: 1f, camp: 1000f)));
    }

    [TestMethod]
    public void AnUnrelatedTarget_KeepsVanillasAnswer()
        => Assert.IsTrue(_sut.Decide(new SiegeAvoidanceFacts(true, false, false, false, 100f, 0f)));

    [TestMethod]
    public void TheRatio_IsReadOnceAtConstruction()
    {
        var config = Substitute.For<IUnlookedForAidConfigProvider>();
        config.MinStrengthRatio.Returns(0.9f);
        var strict = new UnlookedForAidService(_settings, config);
        config.MinStrengthRatio.Returns(0.1f);

        Assert.IsTrue(strict.Decide(Besieger(80f, 100f)), "0.8 is under the 0.9 the service was built with");
        _ = config.Received(1).MinStrengthRatio;
    }

    [TestMethod]
    public void TheSwitch_IsReadThroughTheProvider_OnEveryCall()
    {
        Assert.IsFalse(_sut.Decide(Besieger(60f, 100f)));

        _settings.Enabled.Returns(false);

        Assert.IsTrue(_sut.Decide(Besieger(60f, 100f)));
    }
}
