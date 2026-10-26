using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem;
using TAOM.Features.CulturalFeats;
using TAOM.Features.CulturalFeats.Models;
using TAOM.Features.SmithsMeasure;

namespace TAOM.Tests.Features.SmithsMeasure;

/// <summary>
/// What <c>TaomSmithingModel</c> does with the engine's smelting output (#910), through its internal seam. The engine call
/// itself needs a weapon design and is a game check; the cap is the same <see cref="SmeltingYieldCap"/> the other tests cover.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TaomSmithingModelCapTests
{
    private ISmithsMeasureSettingsProvider _settings = null!;
    private TaomSmithingModel _model = null!;

    [TestInitialize]
    public void Init()
    {
        _settings = Substitute.For<ISmithsMeasureSettingsProvider>();
        _settings.CapSmelting.Returns(true);
        _settings.YieldCap.Returns(1);
        _model = new TaomSmithingModel(Substitute.For<ICulturalFeatsService>(), Substitute.For<ICareerPassiveService>(), _settings);
    }

    [TestMethod]
    public void WithTheCapOn_TheOutputIsCappedAtTheSetValue()
    {
        var result = _model.CapSmelting(new[] { 0, 0, 4, 0, 0, 0, 0, 3, -1 });

        CollectionAssert.AreEqual(new[] { 0, 0, 1, 0, 0, 0, 0, 0, -1 }, result);
    }

    [TestMethod]
    public void WithACapOfThree_ThreeGetThrough()
    {
        _settings.YieldCap.Returns(3);

        var result = _model.CapSmelting(new[] { 0, 0, 4, 0, 0, 0, 0, 3, -1 });

        CollectionAssert.AreEqual(new[] { 0, 0, 3, 0, 0, 0, 0, 0, -1 }, result);
    }

    [TestMethod]
    public void WithTheCapOff_TheEnginesOutputIsReturnedUntouched()
    {
        _settings.CapSmelting.Returns(false);
        var output = new[] { 0, 0, 4, 0, 0, 0, 0, 3, -1 };

        var result = _model.CapSmelting(output);

        Assert.AreSame(output, result);
    }
}
