using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.PosternGate;
using TAOM.Features.TaomBuildings;

namespace TAOM.Tests.Features.PosternGate;

[TestClass]
public class PosternGateServiceTests
{
    private static PosternGateService Service(bool enabled = true, ITownBuildingsAdapter? towns = null)
    {
        var config = Substitute.For<IPosternGateConfigProvider>();
        config.Reductions.Returns(new[] { 0.50f, 0.67f, 0.75f });
        config.Costs.Returns(new[] { 400, 700, 1000 });
        var settings = Substitute.For<IPosternGateSettingsProvider>();
        settings.CutEnabled.Returns(enabled);
        return new PosternGateService(config, settings, towns ?? Substitute.For<ITownBuildingsAdapter>(),
            new TaomBuildingCatalog(new[] { PosternGateDefinition.Create(config.Costs, config.Reductions, settings) }));
    }

    private static ITownBuildingsAdapter TownAtLevel(int level)
    {
        var towns = Substitute.For<ITownBuildingsAdapter>();
        towns.GetLevel("fief", Arg.Any<IReadOnlyCollection<string>>()).Returns(level);
        return towns;
    }

    [TestMethod]
    public void Level0_IsZero() => Assert.AreEqual(0f, Service().FactorFor(0, 0f));

    [TestMethod]
    public void Levels_Are_50_67_75()
    {
        var sut = Service();

        Assert.AreEqual(-0.50f, sut.FactorFor(1, 0f), 1e-5f);
        Assert.AreEqual(-0.67f, sut.FactorFor(2, 0f), 1e-5f);
        Assert.AreEqual(-0.75f, sut.FactorFor(3, 0f), 1e-5f);
    }

    [TestMethod]
    public void Multiplicative_WithImproviser()
    {
        // Improviser is a -0.25 factor; the engine adds factors, so the cut is taken of what remains.
        Assert.AreEqual(-0.5625f, Service().FactorFor(3, -0.25f), 1e-5f);
    }

    [TestMethod]
    public void Disabled_ReturnsZero() => Assert.AreEqual(0f, Service(enabled: false).FactorFor(3, 0f));

    [TestMethod]
    public void OutOfRangeLevel_IsZero()
    {
        var sut = Service();

        Assert.AreEqual(0f, sut.FactorFor(-1, 0f));
        Assert.AreEqual(0f, sut.FactorFor(4, 0f));
    }

    [TestMethod]
    public void CutFor_BuildingAtLevel0_IsZero() =>
        Assert.AreEqual(0f, Service(towns: TownAtLevel(0)).CutFor("fief", 0f));

    [TestMethod]
    public void CutFor_SwitchOff_IsZero() =>
        Assert.AreEqual(0f, Service(enabled: false, towns: TownAtLevel(3)).CutFor("fief", 0f));

    [TestMethod]
    public void CutFor_Level2_WithImproviser_IsMultiplicative() =>
        Assert.AreEqual(-0.67f * 0.75f, Service(towns: TownAtLevel(2)).CutFor("fief", -0.25f), 1e-5f);

    [TestMethod]
    public void CutFor_NullSettlement_IsZero()
    {
        var towns = TownAtLevel(3);

        Assert.AreEqual(0f, Service(towns: towns).CutFor(null, 0f));
        towns.DidNotReceiveWithAnyArgs().GetLevel(default!, default!);
    }
}
