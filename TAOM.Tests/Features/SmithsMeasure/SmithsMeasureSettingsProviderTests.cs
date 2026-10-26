using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.SmithsMeasure;

namespace TAOM.Tests.Features.SmithsMeasure;

/// <summary>
/// The "Smithing" MCM values of Smith's Measure (#910), clamped. A cap outside 1 to 10 falls back to the default with one
/// warning, and a missing settings object (MCM not up) reads the defaults.
/// </summary>
[TestClass]
public class SmithsMeasureSettingsProviderTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Init() => _logger = Substitute.For<IModLogger>();

    private SmithsMeasureSettingsProvider With(TaomSettings settings) => new SmithsMeasureSettingsProvider(settings, _logger);

    [TestMethod]
    public void TheDefaults_AreCapOn_CapOne_MakeToolsOn()
    {
        var sut = With(new TaomSettings());

        Assert.IsTrue(sut.CapSmelting);
        Assert.AreEqual(1, sut.YieldCap);
        Assert.IsTrue(sut.MakeToolsEnabled);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void TheCompiledConstants_MatchThePropertyInitializers()
    {
        var settings = new TaomSettings();

        Assert.AreEqual(SmithsMeasureSettingsProvider.DefaultCapSmelting, settings.SmithsMeasureCapSmelting);
        Assert.AreEqual(SmithsMeasureSettingsProvider.DefaultYieldCap, settings.SmithsMeasureYieldCap);
        Assert.AreEqual(SmithsMeasureSettingsProvider.DefaultMakeTools, settings.SmithsMeasureMakeTools);
        Assert.AreEqual(1, SmithsMeasureSettingsProvider.MinYieldCap);
        Assert.AreEqual(10, SmithsMeasureSettingsProvider.MaxYieldCap);
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(10)]
    public void ACapInRange_IsUsed(int cap)
    {
        var sut = With(new TaomSettings { SmithsMeasureYieldCap = cap });

        Assert.AreEqual(cap, sut.YieldCap);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(11)]
    [DataRow(100)]
    [DataRow(int.MinValue)]
    [DataRow(int.MaxValue)]
    public void ACapOutOfRange_FallsBackToTheDefault_WithAWarning(int cap)
    {
        var sut = With(new TaomSettings { SmithsMeasureYieldCap = cap });

        Assert.AreEqual(SmithsMeasureSettingsProvider.DefaultYieldCap, sut.YieldCap);
        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void ABadCap_WarnsOnce_NotOnEveryRead()
    {
        var sut = With(new TaomSettings { SmithsMeasureYieldCap = 99 });

        _ = sut.YieldCap;
        _ = sut.YieldCap;
        _ = sut.YieldCap;

        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void ADifferentBadCap_WarnsAgain()
    {
        var settings = new TaomSettings { SmithsMeasureYieldCap = 99 };
        var sut = With(settings);
        _ = sut.YieldCap;

        settings.SmithsMeasureYieldCap = 0;
        _ = sut.YieldCap;

        _logger.ReceivedWithAnyArgs(2).LogWarning(default!);
    }

    [TestMethod]
    public void TheSwitches_ReadThrough()
    {
        var settings = new TaomSettings();
        var sut = With(settings);

        settings.SmithsMeasureCapSmelting = false;
        settings.SmithsMeasureMakeTools = false;

        Assert.IsFalse(sut.CapSmelting);
        Assert.IsFalse(sut.MakeToolsEnabled);
    }

    [TestMethod]
    public void WithNoSettingsObject_TheDefaultsAreRead_AndNothingIsLogged()
    {
        var sut = With(new TaomSettings());

        Assert.IsTrue(SmithsMeasureSettingsProvider.CapSmeltingFrom(null));
        Assert.AreEqual(1, sut.YieldCapFrom(null));
        Assert.IsTrue(SmithsMeasureSettingsProvider.MakeToolsFrom(null));
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }
}
