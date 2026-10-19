using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.WatchfulHolds;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WatchfulHolds;

[TestClass]
public class WatchfulHoldsConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;
    private WatchfulHoldsConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_WatchfulHolds_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "watchful_holds");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new WatchfulHoldsConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) => File.WriteAllText(Path.Combine(_featureDir, "watchful_holds.json"), json);

    private void AssertWarned(string field) =>
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(field)));

    // ---- file handling ------------------------------------------------------------------------

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultsAndWarns()
    {
        var config = _sut.GetConfig();

        Assert.AreEqual(5f, config.Radius);
        Assert.AreEqual(1.25f, config.RowFor("mordor").ChanceMultiplier);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("{ \"radius\": ");

        var config = _sut.GetConfig();

        Assert.AreEqual(5f, config.Radius);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_WrongTypeForAnInt_ReturnsDefaultsAndLogsError()
    {
        WriteConfig("{ \"minGarrison\": \"many\" }");

        var config = _sut.GetConfig();

        Assert.AreEqual(30, config.MinGarrison);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_EmptyObject_KeepsEveryDefaultWithoutWarning()
    {
        WriteConfig("{}");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.30f, config.PlayerChancePerHour);
        Assert.AreEqual(24f, config.HornCooldownHours);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_ValidFile_ParsesEveryField()
    {
        WriteConfig(@"{
            ""radius"": 7.5, ""minGarrison"": 10, ""minPartySize"": 12,
            ""playerChancePerHour"": 0.5, ""aiChancePerHour"": 0.25,
            ""woundFraction"": 0.04, ""woundExtraMax"": 3, ""killFraction"": 0.02, ""killExtraMax"": 2,
            ""foodLossFraction"": 0.1, ""moraleLoss"": 20, ""cohesionLoss"": 15, ""cohesionFloor"": 25, ""hornCooldownHours"": 6,
            ""cultures"": { ""default"": { ""chanceMultiplier"": 2, ""lossMultiplier"": 3 }, ""gondor"": { ""chanceMultiplier"": 0.5, ""lossMultiplier"": 0.25 } } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(7.5f, c.Radius);
        Assert.AreEqual(10, c.MinGarrison);
        Assert.AreEqual(12, c.MinPartySize);
        Assert.AreEqual(0.5f, c.PlayerChancePerHour);
        Assert.AreEqual(0.25f, c.AiChancePerHour);
        Assert.AreEqual(0.04f, c.WoundFraction);
        Assert.AreEqual(3, c.WoundExtraMax);
        Assert.AreEqual(0.02f, c.KillFraction);
        Assert.AreEqual(2, c.KillExtraMax);
        Assert.AreEqual(0.1f, c.FoodLossFraction);
        Assert.AreEqual(20f, c.MoraleLoss);
        Assert.AreEqual(15f, c.CohesionLoss);
        Assert.AreEqual(25f, c.CohesionFloor);
        Assert.AreEqual(6f, c.HornCooldownHours);
        Assert.AreEqual(0.5f, c.RowFor("gondor").ChanceMultiplier);
        Assert.AreEqual(0.25f, c.RowFor("gondor").LossMultiplier);
        Assert.AreEqual(2f, c.RowFor("rohan").ChanceMultiplier, "a culture with no row takes the default row");
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_IsCachedForTheProcess()
    {
        WriteConfig("{ \"radius\": 6 }");
        var first = _sut.GetConfig();
        WriteConfig("{ \"radius\": 9 }");

        Assert.AreSame(first, _sut.GetConfig());
        Assert.AreEqual(6f, _sut.GetConfig().Radius);
    }

    // ---- one test per validation rule, floats: NaN, Infinity, sign, range ---------------------

    [DataTestMethod]
    [DataRow("radius", "\"NaN\"")]
    [DataRow("radius", "\"Infinity\"")]
    [DataRow("radius", "0")]
    [DataRow("radius", "-1")]
    [DataRow("radius", "50.5")]
    [DataRow("playerChancePerHour", "\"NaN\"")]
    [DataRow("playerChancePerHour", "-0.1")]
    [DataRow("playerChancePerHour", "1.5")]
    [DataRow("aiChancePerHour", "\"NaN\"")]
    [DataRow("aiChancePerHour", "\"-Infinity\"")]
    [DataRow("aiChancePerHour", "-0.1")]
    [DataRow("aiChancePerHour", "1.1")]
    [DataRow("woundFraction", "\"NaN\"")]
    [DataRow("woundFraction", "-0.01")]
    [DataRow("woundFraction", "2")]
    [DataRow("killFraction", "\"Infinity\"")]
    [DataRow("killFraction", "-0.01")]
    [DataRow("killFraction", "1.01")]
    [DataRow("foodLossFraction", "\"NaN\"")]
    [DataRow("foodLossFraction", "-1")]
    [DataRow("foodLossFraction", "1.5")]
    [DataRow("moraleLoss", "\"NaN\"")]
    [DataRow("moraleLoss", "\"Infinity\"")]
    [DataRow("moraleLoss", "-5")]
    [DataRow("moraleLoss", "100.5")]
    [DataRow("cohesionLoss", "\"NaN\"")]
    [DataRow("cohesionLoss", "-5")]
    [DataRow("cohesionLoss", "101")]
    [DataRow("cohesionFloor", "\"NaN\"")]
    [DataRow("cohesionFloor", "-1")]
    [DataRow("cohesionFloor", "100.5")]
    [DataRow("hornCooldownHours", "\"NaN\"")]
    [DataRow("hornCooldownHours", "\"Infinity\"")]
    [DataRow("hornCooldownHours", "-1")]
    public void GetConfig_BadFloat_RevertsThatFieldToItsDefaultAndWarns(string field, string value)
    {
        WriteConfig("{ \"" + field + "\": " + value + ", \"minGarrison\": 11 }");

        var config = _sut.GetConfig();
        var defaults = new WatchfulHoldsConfig();

        Assert.AreEqual(Read(defaults, field), Read(config, field), field + " reverts to its compiled default");
        Assert.AreEqual(11, config.MinGarrison, "a good field beside a bad one is kept");
        AssertWarned(field);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("invalid values")));
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    private static float Read(WatchfulHoldsConfig c, string field) => field switch
    {
        "radius" => c.Radius,
        "playerChancePerHour" => c.PlayerChancePerHour,
        "aiChancePerHour" => c.AiChancePerHour,
        "woundFraction" => c.WoundFraction,
        "killFraction" => c.KillFraction,
        "foodLossFraction" => c.FoodLossFraction,
        "moraleLoss" => c.MoraleLoss,
        "cohesionLoss" => c.CohesionLoss,
        "cohesionFloor" => c.CohesionFloor,
        "hornCooldownHours" => c.HornCooldownHours,
        _ => throw new System.ArgumentException(field),
    };

    [DataTestMethod]
    [DataRow("radius", "0.01")]
    [DataRow("radius", "50")]
    [DataRow("playerChancePerHour", "0")]
    [DataRow("playerChancePerHour", "1")]
    [DataRow("aiChancePerHour", "0")]
    [DataRow("woundFraction", "1")]
    [DataRow("killFraction", "0")]
    [DataRow("foodLossFraction", "1")]
    [DataRow("moraleLoss", "0")]
    [DataRow("moraleLoss", "100")]
    [DataRow("cohesionLoss", "100")]
    [DataRow("cohesionFloor", "0")]
    [DataRow("cohesionFloor", "100")]
    [DataRow("hornCooldownHours", "0")]
    public void GetConfig_FloatOnItsBoundary_IsKept(string field, string value)
    {
        WriteConfig("{ \"" + field + "\": " + value + " }");

        var config = _sut.GetConfig();

        Assert.AreEqual(float.Parse(value, System.Globalization.CultureInfo.InvariantCulture), Read(config, field));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // ---- ints: sign and the overflow cap ------------------------------------------------------

    [DataTestMethod]
    [DataRow("minGarrison", "-1", 30)]
    [DataRow("minGarrison", "100001", 30)]
    [DataRow("minPartySize", "-5", 30)]
    [DataRow("minPartySize", "2000000", 30)]
    [DataRow("woundExtraMax", "-1", 5)]
    [DataRow("woundExtraMax", "10001", 5)]
    [DataRow("killExtraMax", "-1", 1)]
    [DataRow("killExtraMax", "2147483647", 1)]
    public void GetConfig_BadInt_RevertsThatFieldToItsDefaultAndWarns(string field, string value, int expected)
    {
        WriteConfig("{ \"" + field + "\": " + value + ", \"radius\": 6 }");

        var config = _sut.GetConfig();

        Assert.AreEqual(expected, field switch
        {
            "minGarrison" => config.MinGarrison,
            "minPartySize" => config.MinPartySize,
            "woundExtraMax" => config.WoundExtraMax,
            _ => config.KillExtraMax,
        });
        Assert.AreEqual(6f, config.Radius);
        AssertWarned(field);
    }

    [TestMethod]
    public void GetConfig_ZeroInts_AreKept()
    {
        WriteConfig("{ \"minGarrison\": 0, \"minPartySize\": 0, \"woundExtraMax\": 0, \"killExtraMax\": 0 }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0, c.MinGarrison);
        Assert.AreEqual(0, c.MinPartySize);
        Assert.AreEqual(0, c.WoundExtraMax);
        Assert.AreEqual(0, c.KillExtraMax);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // ---- cultures -----------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow("\"NaN\"")]
    [DataRow("\"Infinity\"")]
    [DataRow("-0.5")]
    [DataRow("5.1")]
    public void GetConfig_BadChanceMultiplier_RevertsToTheCompiledRowAndWarns(string value)
    {
        WriteConfig("{ \"cultures\": { \"default\": { }, \"mordor\": { \"chanceMultiplier\": " + value + ", \"lossMultiplier\": 2 } } }");

        var row = _sut.GetConfig().RowFor("mordor");

        Assert.AreEqual(1.25f, row.ChanceMultiplier, "falls back to the compiled value of that culture's row");
        Assert.AreEqual(2f, row.LossMultiplier, "the good field beside it is kept");
        AssertWarned("mordor");
        AssertWarned("chanceMultiplier");
    }

    [DataTestMethod]
    [DataRow("\"NaN\"")]
    [DataRow("\"Infinity\"")]
    [DataRow("-1")]
    [DataRow("6")]
    public void GetConfig_BadLossMultiplier_RevertsToOneAndWarns(string value)
    {
        WriteConfig("{ \"cultures\": { \"default\": { }, \"mordor\": { \"chanceMultiplier\": 1.5, \"lossMultiplier\": " + value + " } } }");

        var row = _sut.GetConfig().RowFor("mordor");

        Assert.AreEqual(1f, row.LossMultiplier);
        Assert.AreEqual(1.5f, row.ChanceMultiplier, "the good field beside it is kept");
        AssertWarned("lossMultiplier");
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("5")]
    public void GetConfig_MultiplierOnItsBoundary_IsKept(string value)
    {
        WriteConfig("{ \"cultures\": { \"default\": { \"chanceMultiplier\": " + value + ", \"lossMultiplier\": " + value + " } } }");

        var row = _sut.GetConfig().RowFor("anything");

        Assert.AreEqual(float.Parse(value), row.ChanceMultiplier);
        Assert.AreEqual(float.Parse(value), row.LossMultiplier);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_BadMultiplierOnACultureWithoutACompiledRow_FallsBackToOne()
    {
        WriteConfig("{ \"cultures\": { \"default\": { }, \"gondor\": { \"chanceMultiplier\": \"NaN\" } } }");

        Assert.AreEqual(1f, _sut.GetConfig().RowFor("gondor").ChanceMultiplier);
        AssertWarned("gondor");
    }

    [TestMethod]
    public void GetConfig_NullCultureRow_IsDroppedAndWarns()
    {
        WriteConfig("{ \"cultures\": { \"default\": { }, \"gondor\": null } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1f, c.RowFor("gondor").ChanceMultiplier, "the dropped row means the default row applies");
        AssertWarned("gondor");
    }

    [TestMethod]
    public void GetConfig_NullCulturesObject_UsesTheCompiledTableAndWarns()
    {
        WriteConfig("{ \"cultures\": null }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1.25f, c.RowFor("isengard").ChanceMultiplier);
        AssertWarned("cultures");
    }

    [TestMethod]
    public void GetConfig_NoDefaultRow_AddsOneAndWarns()
    {
        WriteConfig("{ \"cultures\": { \"gondor\": { \"chanceMultiplier\": 0.5 } } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(0.5f, c.RowFor("gondor").ChanceMultiplier);
        Assert.AreEqual(1f, c.RowFor("rohan").ChanceMultiplier);
        AssertWarned("default");
    }

    [TestMethod]
    public void GetConfig_BlankCultureKey_IsDroppedAndWarns()
    {
        WriteConfig("{ \"cultures\": { \"default\": { }, \"  \": { \"chanceMultiplier\": 3 } } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1f, c.RowFor("  ").ChanceMultiplier);
        AssertWarned("empty culture key");
    }

    [TestMethod]
    public void RowFor_IsCaseAndWhitespaceInsensitive_AndBlankOrNullTakeTheDefault()
    {
        WriteConfig("{ \"cultures\": { \"default\": { \"chanceMultiplier\": 2 }, \" Mordor \": { \"chanceMultiplier\": 3 } } }");

        var c = _sut.GetConfig();

        Assert.AreEqual(3f, c.RowFor("MORDOR").ChanceMultiplier);
        Assert.AreEqual(3f, c.RowFor(" mordor").ChanceMultiplier);
        Assert.AreEqual(2f, c.RowFor(null).ChanceMultiplier);
        Assert.AreEqual(2f, c.RowFor("").ChanceMultiplier);
        Assert.AreEqual(2f, c.RowFor("nowhere").ChanceMultiplier);
    }

    // ---- the file that ships ------------------------------------------------------------------

    [TestMethod]
    public void ShippedJson_LoadsWithoutAWarningAndMatchesTheCompiledDefaults()
    {
        var shipped = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        Assert.IsTrue(File.Exists(Path.Combine(shipped, "watchful_holds", "watchful_holds.json")), "the shipped file exists");
        _pathService.ModuleDataPath.Returns(shipped);

        var config = new WatchfulHoldsConfigProvider(_pathService, _logger).GetConfig();
        var defaults = new WatchfulHoldsConfig();

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        Assert.AreEqual(5f, config.Radius);
        Assert.AreEqual(30, config.MinGarrison);
        Assert.AreEqual(30, config.MinPartySize);
        Assert.AreEqual(0.30f, config.PlayerChancePerHour);
        Assert.AreEqual(0.15f, config.AiChancePerHour);
        Assert.AreEqual(0.02f, config.WoundFraction);
        Assert.AreEqual(5, config.WoundExtraMax);
        Assert.AreEqual(0.01f, config.KillFraction);
        Assert.AreEqual(1, config.KillExtraMax);
        Assert.AreEqual(0.05f, config.FoodLossFraction);
        Assert.AreEqual(10f, config.MoraleLoss);
        Assert.AreEqual(5f, config.CohesionLoss);
        Assert.AreEqual(10f, config.CohesionFloor);
        Assert.AreEqual(24f, config.HornCooldownHours);
        foreach (var culture in new[] { "mordor", "dolguldur", "isengard", "lothlorien", "mirkwood" })
        {
            Assert.AreEqual(1.25f, config.RowFor(culture).ChanceMultiplier, culture);
            Assert.AreEqual(1f, config.RowFor(culture).LossMultiplier, culture);
        }
        Assert.AreEqual(1f, config.RowFor("gondor").ChanceMultiplier);
        Assert.AreEqual(1f, config.RowFor("default").LossMultiplier);
        Assert.AreEqual(defaults.Cultures.Count, config.Cultures.Count, "the shipped table and the compiled table list the same cultures");
    }

    [TestMethod]
    public void ShippedJson_EveryCultureKeyButDefaultIsAMainCultureId()
    {
        var shipped = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        _pathService.ModuleDataPath.Returns(shipped);
        var valid = TAOM.Tests.Core.CultureDataFixture.MainCultureIds();

        var config = new WatchfulHoldsConfigProvider(_pathService, _logger).GetConfig();

        var invalid = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(
            config.Cultures.Keys, key => key != WatchfulHoldsConfig.DefaultCultureKey && !valid.Contains(key)));
        Assert.AreEqual(0, invalid.Count, "not main culture ids: " + string.Join(", ", invalid));
    }
}
