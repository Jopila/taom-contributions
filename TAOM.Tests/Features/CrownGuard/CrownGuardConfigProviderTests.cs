using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CrownGuard;

namespace TAOM.Tests.Features.CrownGuard;

/// <summary>One test per validation rule (csharp-architecture.md "Config Providers MUST Validate").</summary>
[TestClass]
public class CrownGuardConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IModLogger _logger = null!;
    private CrownGuardConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_CrownGuard_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "crown_guard");
        Directory.CreateDirectory(_featureDir);

        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new CrownGuardConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "crown_guard_cultures.json"), json);

    [TestMethod]
    public void GetMap_FileMissing_ReturnsBuiltInMapAndWarns()
    {
        var map = _sut.GetMap();

        Assert.AreEqual("gondor_mt_fountain_guard", map.Cultures["gondor"]);
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
    public void GetMap_ValidFile_TrimsAndLowerCasesKeysButKeepsTroopCase()
    {
        WriteConfig("{ \"cultures\": { \" Gondor \": \" Gondor_Guard \" } }");

        var map = _sut.GetMap();

        Assert.AreEqual(1, map.Cultures.Count);
        Assert.AreEqual("Gondor_Guard", map.Cultures["gondor"]);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("Loaded")));
    }

    [TestMethod]
    public void GetMap_EmptyKey_DropsRowAndWarnsOnceInSummary()
    {
        WriteConfig("{ \"cultures\": { \" \": \"a_troop\", \"gondor\": \"b_troop\" } }");

        var map = _sut.GetMap();

        Assert.AreEqual(1, map.Cultures.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("empty key")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    [DataTestMethod]
    [DataRow("\"\"")]
    [DataRow("\"   \"")]
    [DataRow("null")]
    public void GetMap_EmptyValue_DropsRowAndWarns(string value)
    {
        WriteConfig("{ \"cultures\": { \"gondor\": " + value + ", \"erebor\": \"b_troop\" } }");

        var map = _sut.GetMap();

        Assert.IsFalse(map.Cultures.ContainsKey("gondor"));
        Assert.IsTrue(map.Cultures.ContainsKey("erebor"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("gondor") && m.Contains("empty")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    [TestMethod]
    public void GetMap_DuplicateKeyAfterNormalizing_KeepsTheFirstAndWarns()
    {
        WriteConfig("{ \"cultures\": { \"Gondor\": \"first_troop\", \" gondor \": \"second_troop\" } }");

        var map = _sut.GetMap();

        Assert.AreEqual(1, map.Cultures.Count);
        Assert.AreEqual("first_troop", map.Cultures["gondor"]);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("duplicate")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }
}
