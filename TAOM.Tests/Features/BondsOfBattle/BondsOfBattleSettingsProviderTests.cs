using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.BondsOfBattle;

namespace TAOM.Tests.Features.BondsOfBattle;

/// <summary>
/// The MCM group is user-editable and MCM deserializes a hand-edited json verbatim (the slider range is a UI hint),
/// so every value is clamped here and a non-finite float reverts to the compiled default.
/// </summary>
[TestClass]
public class BondsOfBattleSettingsProviderTests
{
    [TestMethod]
    public void From_NoSettingsYet_IsTheCompiledDefaults()
    {
        var s = BondsOfBattleSettingsProvider.From(null);

        Assert.AreEqual(new BondsOfBattleSettings(true, 1, false, 5, true, 25f, true, 15), s);
    }

    [TestMethod]
    public void From_TheMcmDefaults_MatchTheNoSettingsFallback()
    {
        Assert.AreEqual(BondsOfBattleSettingsProvider.From(null), BondsOfBattleSettingsProvider.From(new TaomSettings()));
    }

    [TestMethod]
    public void From_ValuesInRange_PassThrough()
    {
        var s = BondsOfBattleSettingsProvider.From(new TaomSettings
        {
            BondsOfBattleEnabled = false,
            BondsAllyBonus = 3,
            BondsEnemyPenaltyEnabled = true,
            BondsEnemyPenalty = 7,
            BondsBanditGratitudeEnabled = false,
            BondsBanditGratitudeRadius = 40f,
            BondsTroopHandoverEnabled = false,
            BondsOverpayRelationCap = 20,
        });

        Assert.AreEqual(new BondsOfBattleSettings(false, 3, true, 7, false, 40f, false, 20), s);
    }

    [DataTestMethod]
    [DataRow(-3, 0)]
    [DataRow(0, 0)]
    [DataRow(5, 5)]
    [DataRow(99, 5)]
    [DataRow(int.MinValue, 0)]
    [DataRow(int.MaxValue, 5)]
    public void From_AllyBonus_IsClampedToZeroToFive(int value, int expected)
    {
        Assert.AreEqual(expected, BondsOfBattleSettingsProvider.From(new TaomSettings { BondsAllyBonus = value }).AllyBonus);
    }

    [DataTestMethod]
    [DataRow(-1, 0)]
    [DataRow(10, 10)]
    [DataRow(500, 10)]
    public void From_EnemyPenalty_IsClampedToZeroToTen(int value, int expected)
    {
        Assert.AreEqual(expected, BondsOfBattleSettingsProvider.From(new TaomSettings { BondsEnemyPenalty = value }).EnemyPenalty);
    }

    [DataTestMethod]
    [DataRow(-5, 3)]
    [DataRow(0, 3)]
    [DataRow(2, 3)]
    [DataRow(3, 3)]
    [DataRow(30, 30)]
    [DataRow(31, 30)]
    [DataRow(int.MaxValue, 30)]
    public void From_OverpayRelationCap_IsClampedToThreeToThirty(int value, int expected)
    {
        Assert.AreEqual(expected, BondsOfBattleSettingsProvider.From(new TaomSettings { BondsOverpayRelationCap = value }).OverpayRelationCap);
    }

    [DataTestMethod]
    [DataRow(-10f, 5f)]
    [DataRow(0f, 5f)]
    [DataRow(4.9f, 5f)]
    [DataRow(60f, 60f)]
    [DataRow(61f, 60f)]
    [DataRow(1e9f, 60f)]
    public void From_Radius_IsClampedToFiveToSixty(float value, float expected)
    {
        Assert.AreEqual(expected, BondsOfBattleSettingsProvider.From(new TaomSettings { BondsBanditGratitudeRadius = value }).BanditGratitudeRadius);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void From_ANonFiniteRadius_RevertsToTheDefault(float value)
    {
        Assert.AreEqual(25f, BondsOfBattleSettingsProvider.From(new TaomSettings { BondsBanditGratitudeRadius = value }).BanditGratitudeRadius);
    }

    [TestMethod]
    public void Get_WithoutMcm_IsTheCompiledDefaults()
    {
        // TaomSettings.Instance is null in a test host.
        Assert.AreEqual(BondsOfBattleSettingsProvider.From(null), new BondsOfBattleSettingsProvider().Get());
    }

    [TestMethod]
    public void Get_ReadsThroughTheInjectedSettingsLive()
    {
        var settings = new TaomSettings { BondsAllyBonus = 2 };
        var provider = new BondsOfBattleSettingsProvider(settings);

        Assert.AreEqual(2, provider.Get().AllyBonus);

        settings.BondsAllyBonus = 4;

        Assert.AreEqual(4, provider.Get().AllyBonus);
    }
}
