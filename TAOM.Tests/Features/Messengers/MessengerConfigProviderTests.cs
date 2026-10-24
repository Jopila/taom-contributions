using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Messengers;

namespace TAOM.Tests.Features.Messengers;

[TestClass]
public class MessengerConfigProviderTests
{
    private string _tempDir = null!;
    private string _messengersDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private MessengerConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Messengers_" + Path.GetRandomFileName());
        _messengersDir = Path.Combine(_tempDir, "messengers");
        Directory.CreateDirectory(_messengersDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new MessengerConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_messengersDir, "messenger_config.json"), json);

    [TestMethod]
    public void GetConfig_ValidJson_ParsesAllFields()
    {
        WriteConfig(@"{
  ""accidentChancePerHour"": 0.005,
  ""travelSpeedMultiplier"": 1.5
}");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.005f, config.AccidentChancePerHour, 0.00001f);
        Assert.AreEqual(1.5f, config.TravelSpeedMultiplier, 0.0001f);
    }

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndLogsInfo()
    {
        var config = _sut.GetConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f);
        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("not valid {{{");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f);
        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_NegativeAccidentChance_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": -0.1 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("accidentChancePerHour=-0.1")));
    }

    [TestMethod]
    public void GetConfig_AccidentChanceAboveOne_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 1.5 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("accidentChancePerHour=1.5")));
    }

    [TestMethod]
    public void GetConfig_ZeroSpeedMultiplier_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""travelSpeedMultiplier"": 0.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("travelSpeedMultiplier=0")));
    }

    [TestMethod]
    public void GetConfig_NegativeSpeedMultiplier_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""travelSpeedMultiplier"": -1.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("travelSpeedMultiplier=-1")));
    }

    [TestMethod]
    public void GetConfig_AbsurdSpeedMultiplier_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""travelSpeedMultiplier"": 100.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("travelSpeedMultiplier=100")));
    }

    [TestMethod]
    public void GetConfig_BoundaryAccidentChanceZero_Accepted()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 0.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.0f, config.AccidentChancePerHour, 0.00001f);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("accidentChancePerHour")));
    }

    [TestMethod]
    public void GetConfig_BoundaryAccidentChanceOne_Accepted()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 1.0 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.AccidentChancePerHour, 0.00001f);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("accidentChancePerHour")));
    }

    [TestMethod]
    public void GetConfig_PartialJson_MergesWithDefaults()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 0.01 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.01f, config.AccidentChancePerHour, 0.00001f);
        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
    }

    [TestMethod]
    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 0.005 }");

        var first = _sut.GetConfig();
        var second = _sut.GetConfig();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void GetConfig_ValidValues_LogsInfoNotWarning()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": 0.003, ""travelSpeedMultiplier"": 1.2 }");

        _sut.GetConfig();

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("outside")));
    }

    [TestMethod]
    public void GetConfig_NaNAccidentChance_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""accidentChancePerHour"": ""NaN"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("accidentChancePerHour")));
    }

    [TestMethod]
    public void GetConfig_InfinitySpeedMultiplier_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""travelSpeedMultiplier"": ""Infinity"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("travelSpeedMultiplier")));
    }

    [TestMethod]
    public void GetConfig_NegativeInfinitySpeedMultiplier_RevertsToDefaultAndWarns()
    {
        WriteConfig(@"{ ""travelSpeedMultiplier"": ""-Infinity"" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("travelSpeedMultiplier")));
    }

    [TestMethod]
    public void DefaultConfig_MatchesLOTRAOMSpec()
    {
        var config = new MessengerConfig();

        Assert.AreEqual(0.002f, config.AccidentChancePerHour, 0.00001f,
            "Default accident chance should be 0.2%/hr to match LOTRAOM");
        Assert.AreEqual(1.0f, config.TravelSpeedMultiplier, 0.0001f,
            "Default speed multiplier should be 1.0 (no scaling)");
    }

    [TestMethod]
    public void GetConfig_PricingKeysMissing_UsesTheDefaults()
    {
        WriteConfig("{}");

        var cfg = _sut.GetConfig();

        Assert.AreEqual(25f, cfg.GoldPerTravelDay);
        Assert.AreEqual(2f, cfg.AtWarMultiplier);
        Assert.AreEqual(2f, cfg.NeverMetMultiplier);
        Assert.AreEqual(5f, cfg.BesiegedMultiplier);
    }

    private static float PricingValue(MessengerConfig cfg, string key) => key switch
    {
        "goldPerTravelDay" => cfg.GoldPerTravelDay,
        "atWarMultiplier" => cfg.AtWarMultiplier,
        "neverMetMultiplier" => cfg.NeverMetMultiplier,
        _ => cfg.BesiegedMultiplier,
    };

    private static float DefaultFor(string key) => PricingValue(new MessengerConfig(), key);

    [DataTestMethod]
    [DataRow("goldPerTravelDay", "-1")]
    [DataRow("goldPerTravelDay", "1001")]
    [DataRow("atWarMultiplier", "0.5")]
    [DataRow("atWarMultiplier", "11")]
    [DataRow("neverMetMultiplier", "0")]
    [DataRow("neverMetMultiplier", "10.5")]
    [DataRow("besiegedMultiplier", "0.9")]
    [DataRow("besiegedMultiplier", "99")]
    public void GetConfig_PricingFieldOutOfRange_RevertsToTheDefaultAndWarns(string key, string value)
    {
        WriteConfig("{ \"" + key + "\": " + value + " }");

        Assert.AreEqual(DefaultFor(key), PricingValue(_sut.GetConfig(), key));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(key)));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("invalid values")));
    }

    [DataTestMethod]
    [DataRow("0.1", true)]
    [DataRow("10", true)]
    [DataRow("0.09", false)]
    [DataRow("10.01", false)]
    public void GetConfig_TravelSpeedMultiplierAtItsBounds_IsKeptInsideAndRevertedOutside(string value, bool kept)
    {
        WriteConfig("{ \"travelSpeedMultiplier\": " + value + " }");

        var cfg = _sut.GetConfig();

        if (kept)
        {
            Assert.AreEqual(float.Parse(value, System.Globalization.CultureInfo.InvariantCulture), cfg.TravelSpeedMultiplier);
            _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("travelSpeedMultiplier")));
            return;
        }
        Assert.AreEqual(new MessengerConfig().TravelSpeedMultiplier, cfg.TravelSpeedMultiplier);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("travelSpeedMultiplier")));
    }

    [TestMethod]
    public void GetConfig_PricingFieldsInRange_AreKept()
    {
        WriteConfig("{ \"goldPerTravelDay\": 1000, \"atWarMultiplier\": 10, \"neverMetMultiplier\": 1, \"besiegedMultiplier\": 3.5 }");

        var cfg = _sut.GetConfig();

        Assert.AreEqual(1000f, cfg.GoldPerTravelDay);
        Assert.AreEqual(10f, cfg.AtWarMultiplier);
        Assert.AreEqual(1f, cfg.NeverMetMultiplier);
        Assert.AreEqual(3.5f, cfg.BesiegedMultiplier);
    }

    [TestMethod]
    public void GetConfig_PricingFieldsAtTheirLowerBounds_AreKept()
    {
        WriteConfig("{ \"goldPerTravelDay\": 0, \"atWarMultiplier\": 1, \"neverMetMultiplier\": 1, \"besiegedMultiplier\": 1 }");

        var cfg = _sut.GetConfig();

        Assert.AreEqual(0f, cfg.GoldPerTravelDay);
        Assert.AreEqual(1f, cfg.AtWarMultiplier);
        Assert.AreEqual(1f, cfg.NeverMetMultiplier);
        Assert.AreEqual(1f, cfg.BesiegedMultiplier);
    }

    [DataTestMethod]
    [DataRow("goldPerTravelDay", "NaN")]
    [DataRow("goldPerTravelDay", "Infinity")]
    [DataRow("goldPerTravelDay", "-Infinity")]
    [DataRow("atWarMultiplier", "NaN")]
    [DataRow("atWarMultiplier", "Infinity")]
    [DataRow("atWarMultiplier", "-Infinity")]
    [DataRow("neverMetMultiplier", "NaN")]
    [DataRow("neverMetMultiplier", "Infinity")]
    [DataRow("neverMetMultiplier", "-Infinity")]
    [DataRow("besiegedMultiplier", "NaN")]
    [DataRow("besiegedMultiplier", "Infinity")]
    [DataRow("besiegedMultiplier", "-Infinity")]
    public void GetConfig_NonFinitePricingField_RevertsToTheDefaultAndWarns(string key, string value)
    {
        WriteConfig("{ \"" + key + "\": \"" + value + "\" }");

        Assert.AreEqual(DefaultFor(key), PricingValue(_sut.GetConfig(), key));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(key)));
    }
}
