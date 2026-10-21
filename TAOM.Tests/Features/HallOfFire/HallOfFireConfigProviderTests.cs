using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.HallOfFire;

namespace TAOM.Tests.Features.HallOfFire;

/// <summary>
/// One test per validation rule (csharp-architecture.md "Config Providers MUST Validate"). The
/// provider is fail-soft: a bad row is dropped with a warning and the game still loads.
/// </summary>
[TestClass]
public class HallOfFireConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IModLogger _logger = null!;
    private HallOfFireConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_HallOfFire_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "hall_of_fire");
        Directory.CreateDirectory(_featureDir);

        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new HallOfFireConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "hall_of_fire.json"), json);

    [TestMethod]
    public void GetMap_FileMissing_ReturnsBuiltInMapAndWarns()
    {
        var map = _sut.GetMap();

        Assert.AreEqual("empire", map.Cultures["gondor"]);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void GetMap_InvalidJson_ReturnsBuiltInMapAndLogsError()
    {
        WriteConfig("{ this is not json");

        var map = _sut.GetMap();

        Assert.AreEqual("empire", map.Cultures["gondor"]);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("parse")));
    }

    [TestMethod]
    public void GetMap_NullCulturesSection_ReturnsBuiltInMapAndWarns()
    {
        WriteConfig("{ \"cultures\": null }");

        var map = _sut.GetMap();

        Assert.AreEqual("empire", map.Cultures["gondor"]);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("null")));
    }

    [TestMethod]
    public void GetMap_EmptyCulturesObject_IsTheOffSwitch_EmptyMapNoWarningInfoWithZeroRows()
    {
        WriteConfig("{ \"cultures\": {} }");

        var map = _sut.GetMap();

        Assert.AreEqual(0, map.Cultures.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("(0 culture row(s))")));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("null")]
    public void GetMap_EmptyFile_ReturnsBuiltInMapAndWarnsAboutTheNullSection(string content)
    {
        WriteConfig(content);

        var map = _sut.GetMap();

        Assert.AreEqual("empire", map.Cultures["gondor"]);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("null")));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetMap_ValidFile_NormalizesCaseAndWhitespaceAndLogsInfo()
    {
        WriteConfig("{ \"cultures\": { \" Gondor \": \" EMPIRE \" } }");

        var map = _sut.GetMap();

        Assert.AreEqual("empire", map.Cultures["gondor"]);
        Assert.AreEqual(1, map.Cultures.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("Loaded")));
    }

    [TestMethod]
    public void GetMap_EmptyCultureKey_DropsRowAndWarns()
    {
        WriteConfig("{ \"cultures\": { \" \": \"empire\", \"gondor\": \"empire\" } }");

        var map = _sut.GetMap();

        Assert.AreEqual(1, map.Cultures.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("empty key")));
    }

    [TestMethod]
    public void GetMap_ValueOutsideTheSixVanillaSets_DropsRowAndWarns()
    {
        WriteConfig("{ \"cultures\": { \"gondor\": \"gondorian\", \"erebor\": \"sturgia\" } }");

        var map = _sut.GetMap();

        Assert.IsFalse(map.Cultures.ContainsKey("gondor"));
        Assert.IsTrue(map.Cultures.ContainsKey("erebor"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("gondorian")));
    }

    [TestMethod]
    public void GetMap_WildernessSetAsValue_DropsRowAndWarns()
    {
        WriteConfig("{ \"cultures\": { \"gondor\": \"plains\" } }");

        var map = _sut.GetMap();

        Assert.AreEqual(0, map.Cultures.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("plains")));
    }

    [TestMethod]
    public void GetMap_NullValue_DropsRowAndWarns()
    {
        WriteConfig("{ \"cultures\": { \"gondor\": null } }");

        var map = _sut.GetMap();

        Assert.AreEqual(0, map.Cultures.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("gondor")));
    }

    [TestMethod]
    public void GetMap_KeyEqualToItsValue_DropsTheNoOpRowAndWarns()
    {
        WriteConfig("{ \"cultures\": { \"Sturgia\": \"sturgia\", \"erebor\": \"sturgia\" } }");

        var map = _sut.GetMap();

        Assert.IsFalse(map.Cultures.ContainsKey("sturgia"));
        Assert.IsTrue(map.Cultures.ContainsKey("erebor"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("sturgia") && m.Contains("no-op")));
    }

    [TestMethod]
    public void GetMap_DuplicateKeysAfterNormalizing_WarnsAndKeepsTheFirst()
    {
        WriteConfig("{ \"cultures\": { \"Gondor\": \"empire\", \" gondor \": \"vlandia\" } }");

        var map = _sut.GetMap();

        Assert.AreEqual("empire", map.Cultures["gondor"]);
        Assert.AreEqual(1, map.Cultures.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("cultures['gondor']") && m.Contains("duplicate")));
    }

    [TestMethod]
    public void GetMap_Rejections_LogExactlyOneSummaryWarning()
    {
        WriteConfig("{ \"cultures\": { \"gondor\": \"nope\", \"erebor\": \"nope\" } }");

        _sut.GetMap();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    [TestMethod]
    public void GetMap_CalledTwice_ReadsTheFileOnce()
    {
        WriteConfig("{ \"cultures\": { \"gondor\": \"empire\" } }");

        var first = _sut.GetMap();
        File.Delete(Path.Combine(_featureDir, "hall_of_fire.json"));
        var second = _sut.GetMap();

        Assert.AreSame(first, second);
    }
}
