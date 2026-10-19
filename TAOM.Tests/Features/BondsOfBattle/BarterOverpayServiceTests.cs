using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.BondsOfBattle;
using TAOM.Features.Enlistment;
using TAOM.Features.InitialChildGeneration;

namespace TAOM.Tests.Features.BondsOfBattle;

[TestClass]
public class BarterOverpayServiceTests
{
    private static readonly BondsOfBattleSettings Defaults = new(true, 1, false, 5, true, 25f, true, 15);

    private IBondsOfBattleSettingsProvider _settings = null!;
    private IEnlistmentStateQuery _enlistment = null!;
    private IRandomSource _random = null!;
    private BarterOverpayService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IBondsOfBattleSettingsProvider>();
        _settings.Get().Returns(Defaults);
        _enlistment = Substitute.For<IEnlistmentStateQuery>();
        _enlistment.IsEnlisted.Returns(false);
        _random = Substitute.For<IRandomSource>();
        _random.NextDouble().Returns(0.99);
        _sut = new BarterOverpayService(_settings, _enlistment, _random);
    }

    [TestMethod]
    public void CalculateRelationIncrease_UsesTheCapFromTheSettings()
    {
        Assert.AreEqual(5, _sut.CalculateRelationIncrease(0, 10000f, 0f));

        _settings.Get().Returns(Defaults with { OverpayRelationCap = 3 });

        Assert.AreEqual(3, _sut.CalculateRelationIncrease(0, 10000f, 0f));
    }

    [TestMethod]
    public void CalculateRelationIncrease_AppliesTheTributeBonusItIsGiven()
    {
        Assert.AreEqual(8, _sut.CalculateRelationIncrease(0, 10000f, 0.5f));
    }

    [TestMethod]
    public void CalculateRelationIncrease_StartsFromTheRelationItIsGiven()
    {
        // From relation 10 a point costs 11000, so 10000 buys no whole point and the roll of 0.99 buys no part of one.
        Assert.AreEqual(0, _sut.CalculateRelationIncrease(10, 10000f, 0f));
    }

    [TestMethod]
    public void CalculateRelationIncrease_RollsThroughTheRandomSeam()
    {
        _random.NextDouble().Returns(0.0);

        Assert.AreEqual(6, _sut.CalculateRelationIncrease(0, 10000f, 0f));
    }

    [TestMethod]
    public void CalculateRelationIncrease_FeatureOff_DefersToVanilla()
    {
        _settings.Get().Returns(Defaults with { Enabled = false });

        Assert.IsNull(_sut.CalculateRelationIncrease(0, 10000f, 0f));
    }

    [TestMethod]
    public void CalculateRelationIncrease_Enlisted_DefersToVanilla()
    {
        _enlistment.IsEnlisted.Returns(true);

        Assert.IsNull(_sut.CalculateRelationIncrease(0, 10000f, 0f));
    }
}
