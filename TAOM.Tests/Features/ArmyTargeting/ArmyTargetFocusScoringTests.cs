using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.ArmyTargeting;

namespace TAOM.Tests.Features.ArmyTargeting;

/// <summary>
/// The army focus inside <c>ArmyTargetingService.ApplyTargetScoreModifiers</c> (#765, M4): it multiplies a
/// positive score on top of the priority, theater and home-defence terms, and never lifts vanilla's zero.
/// </summary>
[TestClass]
public class ArmyTargetFocusScoringTests
{
    private IArmyTargetingSettingsProvider _settings = null!;
    private IArmyTargetingConfigProvider _configProvider = null!;
    private ArmyTargetFocusRegistry _focus = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IArmyTargetingSettingsProvider>();
        _settings.EnableArmyStrategicIntelligence.Returns(true);
        _settings.EnableWarTheaters.Returns(true);
        _settings.CommitmentMultiplier.Returns(4.0f);
        _settings.MaxPriorityBoost.Returns(3.0f);
        _settings.EvilAggressionScale.Returns(1.0f);
        _settings.BorderRescueRadiusInTownGaps.Returns(3.2f);
        _settings.DefenderPriorityMultiplier.Returns(1.6f);

        _configProvider = Substitute.For<IArmyTargetingConfigProvider>();
        _configProvider.GetConfig().Returns(new ArmyTargetingConfig
        {
            FactionPriorityTargets = new Dictionary<string, List<string>>(),
            FactionAggressionMultipliers = new Dictionary<string, float>(),
            Theaters = new List<string>(),
            KingdomTheaters = new Dictionary<string, List<string>>(),
        });

        _focus = new ArmyTargetFocusRegistry();
        _focus.Replace(new[] { new ArmyTargetFocus("town_V2", new[] { "isengard" }, "vlandia", 1.5f) });
    }

    private ArmyTargetingService CreateSut() =>
        new ArmyTargetingService(_settings, _configProvider, Substitute.For<IModLogger>(), _focus);

    private static TargetScoreContext Ctx(float baseScore, ArmyTargetingMission mission, string faction, string target = "town_V2") =>
        new TargetScoreContext
        {
            BaseScore = baseScore,
            Mission = mission,
            FactionId = faction,
            TargetFactionId = "vlandia",
            TargetSettlementId = target,
        };

    [TestMethod]
    public void ApplyTargetScoreModifiers_AttackerBesiegerOnTheFocusTarget_MultipliesTheScore()
    {
        Assert.AreEqual(150f, CreateSut().ApplyTargetScoreModifiers(Ctx(100f, ArmyTargetingMission.Besieger, "isengard")), 0.001f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_DefenderArmyOfTheDefender_MultipliesOnTopOfHomeDefence()
    {
        Assert.AreEqual(240f, CreateSut().ApplyTargetScoreModifiers(Ctx(100f, ArmyTargetingMission.Defender, "vlandia")), 0.001f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_AnotherTarget_IsUnchanged()
    {
        Assert.AreEqual(100f, CreateSut().ApplyTargetScoreModifiers(Ctx(100f, ArmyTargetingMission.Besieger, "isengard", "town_V1")), 0.001f);
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-5f)]
    public void ApplyTargetScoreModifiers_NonPositiveBase_IsNeverLifted(float baseScore)
    {
        Assert.AreEqual(baseScore, CreateSut().ApplyTargetScoreModifiers(Ctx(baseScore, ArmyTargetingMission.Besieger, "isengard")));
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_NaNBase_ReturnsItUnchanged()
    {
        Assert.IsTrue(float.IsNaN(CreateSut().ApplyTargetScoreModifiers(Ctx(float.NaN, ArmyTargetingMission.Besieger, "isengard"))));
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_StrategicIntelligenceOff_IgnoresTheFocus()
    {
        _settings.EnableArmyStrategicIntelligence.Returns(false);

        Assert.AreEqual(100f, CreateSut().ApplyTargetScoreModifiers(Ctx(100f, ArmyTargetingMission.Besieger, "isengard")), 0.001f);
    }

    [TestMethod]
    public void ApplyTargetScoreModifiers_FocusClearedAfterConstruction_ReadsTheLiveRegistry()
    {
        var sut = CreateSut();

        _focus.Clear();

        Assert.AreEqual(100f, sut.ApplyTargetScoreModifiers(Ctx(100f, ArmyTargetingMission.Besieger, "isengard")), 0.001f);
    }
}
