using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LordBanners;

namespace TAOM.Tests.Features.LordBanners;

/// <summary>One test per validation rule (csharp-architecture.md "Config Providers MUST Validate").</summary>
[TestClass]
public class LordBannerCulturesConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IModLogger _logger = null!;
    private LordBannerCulturesConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_LordBanners_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "lord_banners");
        Directory.CreateDirectory(_featureDir);

        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new LordBannerCulturesConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "lord_banner_cultures.json"), json);

    [TestMethod]
    public void GetMap_FileMissing_ReturnsBuiltInMapAndWarns()
    {
        var map = _sut.GetMap();

        Assert.AreEqual("empire", map.Cultures["gondor"][0]);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void GetMap_InvalidJson_ReturnsBuiltInMapAndLogsError()
    {
        WriteConfig("{ this is not json");

        var map = _sut.GetMap();

        Assert.IsTrue(map.Cultures.ContainsKey("gondor"));
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("parse")));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("null")]
    [DataRow("{ \"cultures\": null }")]
    public void GetMap_EmptyOrNullContent_ReturnsBuiltInMapAndWarnsAboutTheNullSection(string content)
    {
        WriteConfig(content);

        var map = _sut.GetMap();

        Assert.IsTrue(map.Cultures.ContainsKey("gondor"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("null")));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetMap_ValidFile_NormalizesAndLogsInfo()
    {
        WriteConfig("{ \"cultures\": { \" Gondor \": [\" EMPIRE \"] } }");

        var map = _sut.GetMap();

        Assert.AreEqual("empire", map.Cultures["gondor"][0]);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("Loaded")));
    }

    [TestMethod]
    public void GetMap_EmptyKey_DropsRowAndWarnsOnceInSummary()
    {
        WriteConfig("{ \"cultures\": { \" \": [\"empire\"], \"gondor\": [\"empire\"] } }");

        var map = _sut.GetMap();

        Assert.AreEqual(1, map.Cultures.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("empty key")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    [TestMethod]
    public void GetMap_UnknownValue_DropsRowAndWarns()
    {
        WriteConfig("{ \"cultures\": { \"gondor\": [\"empire\", \"neutral_culture\"], \"erebor\": [\"sturgia\"] } }");

        var map = _sut.GetMap();

        Assert.IsFalse(map.Cultures.ContainsKey("gondor"));
        Assert.IsTrue(map.Cultures.ContainsKey("erebor"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("neutral_culture") && m.Contains("gondor")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    [TestMethod]
    public void GetMap_NullRow_DropsRowAndWarns()
    {
        WriteConfig("{ \"cultures\": { \"gondor\": null } }");

        var map = _sut.GetMap();

        Assert.AreEqual(0, map.Cultures.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("gondor") && m.Contains("null")));
    }

    [TestMethod]
    public void GetMap_DuplicateKeyAfterNormalizing_KeepsTheFirstAndWarns()
    {
        WriteConfig("{ \"cultures\": { \"Gondor\": [\"empire\"], \" gondor \": [\"sturgia\"] } }");

        var map = _sut.GetMap();

        Assert.AreEqual("empire", map.Cultures["gondor"][0]);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("duplicate")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    [TestMethod]
    public void GetMap_RepeatedValuesInOneRow_CollapseSilently()
    {
        WriteConfig("{ \"cultures\": { \"gondor\": [\"empire\", \"Empire\"] } }");

        var map = _sut.GetMap();

        Assert.AreEqual(1, map.Cultures["gondor"].Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetMap_EmptyList_IsKeptAsARowWithNoBannerCultures()
    {
        WriteConfig("{ \"cultures\": { \"elves\": [] } }");

        var map = _sut.GetMap();

        Assert.AreEqual(0, map.Cultures["elves"].Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }
}
