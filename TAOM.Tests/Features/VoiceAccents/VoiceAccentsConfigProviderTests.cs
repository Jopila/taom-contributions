using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.VoiceAccents;

namespace TAOM.Tests.Features.VoiceAccents;

/// <summary>
/// One test per validation rule (csharp-architecture.md "Config Providers MUST Validate"). The
/// provider is fail-soft: a bad row is dropped with a warning and the game still loads.
/// </summary>
[TestClass]
public class VoiceAccentsConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IModLogger _logger = null!;
    private VoiceAccentsConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_VoiceAccents_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "voice_accents");
        Directory.CreateDirectory(_featureDir);

        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new VoiceAccentsConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "voice_accents.json"), json);

    [TestMethod]
    public void GetMap_FileMissing_ReturnsBuiltInMapAndWarns()
    {
        var map = _sut.GetMap();

        Assert.AreEqual("imperial", map.Cultures["gondor"]);
        Assert.IsTrue(map.Races.ContainsKey("nazghul"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void GetMap_InvalidJson_ReturnsBuiltInMapAndLogsError()
    {
        WriteConfig("{ this is not json");

        var map = _sut.GetMap();

        Assert.AreEqual("imperial", map.Cultures["gondor"]);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("parse")));
    }

    [TestMethod]
    public void GetMap_NullCulturesSection_ReturnsBuiltInMapAndWarns()
    {
        WriteConfig("{ \"cultures\": null, \"races\": {} }");

        var map = _sut.GetMap();

        Assert.AreEqual("imperial", map.Cultures["gondor"]);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("null")));
    }

    [TestMethod]
    public void GetMap_NullRacesSection_ReturnsBuiltInMapAndWarns()
    {
        WriteConfig("{ \"cultures\": {}, \"races\": null }");

        var map = _sut.GetMap();

        Assert.IsTrue(map.Races.ContainsKey("sauron"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("null")));
    }

    [TestMethod]
    public void GetMap_ValidFile_NormalizesCaseAndWhitespaceAndLogsInfo()
    {
        WriteConfig("{ \"cultures\": { \" Gondor \": \" IMPERIAL \" }, \"races\": { \" Nazghul\": \"Silent\" } }");

        var map = _sut.GetMap();

        Assert.AreEqual("imperial", map.Cultures["gondor"]);
        Assert.IsTrue(map.Races.ContainsKey("nazghul"));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("Loaded")));
    }

    [TestMethod]
    public void GetMap_EmptyCultureKey_DropsRowAndWarns()
    {
        WriteConfig("{ \"cultures\": { \" \": \"imperial\", \"gondor\": \"imperial\" }, \"races\": {} }");

        var map = _sut.GetMap();

        Assert.AreEqual(1, map.Cultures.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("empty key")));
    }

    [TestMethod]
    public void GetMap_UnknownAccentValue_DropsRowAndWarns()
    {
        WriteConfig("{ \"cultures\": { \"gondor\": \"gondorian\", \"mordor\": \"mountain_bandits\" }, \"races\": {} }");

        var map = _sut.GetMap();

        Assert.IsFalse(map.Cultures.ContainsKey("gondor"));
        Assert.IsTrue(map.Cultures.ContainsKey("mordor"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("gondorian")));
    }

    [TestMethod]
    public void GetMap_NullAccentValue_DropsRowAndWarns()
    {
        WriteConfig("{ \"cultures\": { \"gondor\": null }, \"races\": {} }");

        var map = _sut.GetMap();

        Assert.AreEqual(0, map.Cultures.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("gondor")));
    }

    [TestMethod]
    public void GetMap_EmptyRaceKey_DropsRowAndWarns()
    {
        WriteConfig("{ \"cultures\": {}, \"races\": { \"\": \"silent\" } }");

        var map = _sut.GetMap();

        Assert.AreEqual(0, map.Races.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("race row has an empty key")));
    }

    [TestMethod]
    public void GetMap_RaceAccentValue_IsAccepted()
    {
        WriteConfig("{ \"cultures\": {}, \"races\": { \" Saruman\": \"IMPERIAL\" } }");

        var map = _sut.GetMap();

        Assert.AreEqual("imperial", map.Races["saruman"]);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetMap_UnknownRaceValue_DropsRowAndWarns()
    {
        WriteConfig("{ \"cultures\": {}, \"races\": { \"sauron\": \"gondorian\", \"nazghul\": \"silent\" } }");

        var map = _sut.GetMap();

        Assert.IsFalse(map.Races.ContainsKey("sauron"));
        Assert.AreEqual("silent", map.Races["nazghul"]);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("races['sauron']") && m.Contains("gondorian")));
    }

    [TestMethod]
    public void GetMap_DuplicateKeysAfterNormalizing_WarnsAndKeepsTheFirst()
    {
        WriteConfig("{ \"cultures\": { \"Gondor\": \"imperial\", \" gondor \": \"looters\" }, \"races\": { \"Orc\": \"silent\", \"ORC\": \"imperial\" } }");

        var map = _sut.GetMap();

        Assert.AreEqual("imperial", map.Cultures["gondor"]);
        Assert.AreEqual("silent", map.Races["orc"]);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("cultures['gondor']") && m.Contains("duplicate")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("races['orc']") && m.Contains("duplicate")));
    }

    [TestMethod]
    public void GetMap_RejectionsInBothSections_LogExactlyOneSummaryWarning()
    {
        WriteConfig("{ \"cultures\": { \"gondor\": \"nope\" }, \"races\": { \"orc\": \"nope\" } }");

        _sut.GetMap();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("null")]
    public void GetMap_EmptyFile_ReturnsBuiltInMapAndWarnsAboutTheNullSection(string content)
    {
        WriteConfig(content);

        var map = _sut.GetMap();

        Assert.AreEqual("imperial", map.Cultures["gondor"]);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("null")));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }
}
