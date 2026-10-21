using System.Linq;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.NamedCompanions;

namespace TAOM.Tests.Features.NamedCompanions;

[TestClass]
public class NamedCompanionConfigProviderTests
{
    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;
    private NamedCompanionConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "named_companions"));

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new NamedCompanionConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void GetCompanions_ValidJson_ParsesAllEntries()
    {
        WriteConfig(@"[
  { ""character_id"": ""named_companion_aragorn"", ""spawn_settlement"": ""town_EN2"", ""race"": ""human"", ""enabled"": true },
  { ""character_id"": ""named_companion_legolas"", ""spawn_settlement"": ""town_M1"", ""race"": ""elf"", ""enabled"": true }
]");

        var companions = _sut.GetCompanions();

        Assert.AreEqual(2, companions.Count);
        Assert.AreEqual("named_companion_aragorn", companions[0].CharacterId);
        Assert.AreEqual("town_EN2", companions[0].SpawnSettlement);
        Assert.AreEqual("human", companions[0].Race);
        Assert.IsTrue(companions[0].Enabled);
        Assert.AreEqual("named_companion_legolas", companions[1].CharacterId);
        Assert.AreEqual("elf", companions[1].Race);
    }

    [TestMethod]
    public void GetCompanions_MissingFile_ReturnsEmptyListAndLogs()
    {
        var companions = _sut.GetCompanions();

        Assert.AreEqual(0, companions.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetCompanions_MalformedJson_ReturnsEmptyListAndLogs()
    {
        WriteConfig("not valid json {{{");

        var companions = _sut.GetCompanions();

        Assert.AreEqual(0, companions.Count);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetCompanions_CachesResult()
    {
        WriteConfig(@"[{ ""character_id"": ""test"", ""spawn_settlement"": ""town_1"", ""race"": ""human"" }]");

        var first = _sut.GetCompanions();
        var second = _sut.GetCompanions();

        Assert.AreSame(first, second);
    }

    [TestMethod]
    public void GetCompanions_DisabledEntry_ParsesCorrectly()
    {
        WriteConfig(@"[{ ""character_id"": ""test"", ""spawn_settlement"": ""town_1"", ""race"": ""human"", ""enabled"": false }]");

        var companions = _sut.GetCompanions();

        Assert.AreEqual(1, companions.Count);
        Assert.IsFalse(companions[0].Enabled);
    }

    [TestMethod]
    public void GetCompanions_DefaultEnabled_IsTrue()
    {
        WriteConfig(@"[{ ""character_id"": ""test"", ""spawn_settlement"": ""town_1"", ""race"": ""human"" }]");

        var companions = _sut.GetCompanions();

        Assert.IsTrue(companions[0].Enabled);
    }

    [TestMethod]
    public void GetCompanions_EmptyArray_ReturnsEmptyList()
    {
        WriteConfig("[]");

        var companions = _sut.GetCompanions();

        Assert.AreEqual(0, companions.Count);
    }

    [TestMethod]
    public void GetCompanions_NullFromANonEmptyFile_IsAParseFailure()
    {
        WriteConfig("null");

        var companions = _sut.GetCompanions();

        Assert.AreEqual(0, companions.Count);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetCompanions_SyntaxErrorInsideAnEntry_LogsAtError()
    {
        WriteConfig(@"[
  { ""character_id"": ""a"", ""spawn_settlement"": ""t1"" ""race"": ""human"" },
  { ""character_id"": ""b"", ""spawn_settlement"": ""t2"", ""race"": ""elf"" }
]");

        var companions = _sut.GetCompanions();

        Assert.AreEqual(0, companions.Count);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetCompanions_WrongTypedMembers_LogOneSummaryWarning()
    {
        WriteConfig(@"[
  { ""character_id"": ""a"", ""spawn_settlement"": ""t1"", ""race"": ""human"", ""stay_at_home"": ""sometimes"", ""hire_terms"": { ""gold"": ""lots"" } },
  { ""character_id"": ""b"", ""spawn_settlement"": ""t2"", ""race"": ""elf"" }
]");

        var companions = _sut.GetCompanions();

        Assert.AreEqual(2, companions.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("skipped 2 member(s)")));
    }

    [TestMethod]
    public void GetCompanions_WrongTypedEnabled_DisablesThatEntryOnly()
    {
        WriteConfig(@"[
  { ""character_id"": ""a"", ""spawn_settlement"": ""t1"", ""race"": ""human"", ""enabled"": ""no"" },
  { ""character_id"": ""b"", ""spawn_settlement"": ""t2"", ""race"": ""elf"" }
]");

        var companions = _sut.GetCompanions();

        Assert.AreEqual(2, companions.Count);
        Assert.IsFalse(companions[0].Enabled, "a companion whose enabled flag cannot be read fails closed");
        Assert.IsTrue(companions[1].Enabled);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("disabled")));
    }

    [TestMethod]
    public void GetCompanions_UnconvertibleEarlierEntry_DoesNotShiftTheDisabledIndex()
    {
        WriteConfig(@"[
  ""stray"",
  { ""character_id"": ""b"", ""spawn_settlement"": ""t1"", ""race"": ""human"", ""enabled"": ""no"" },
  { ""character_id"": ""c"", ""spawn_settlement"": ""t2"", ""race"": ""elf"" }
]");

        var companions = _sut.GetCompanions();

        Assert.IsFalse(companions.Single(c => c.CharacterId == "b").Enabled, "b has the unreadable flag");
        Assert.IsTrue(companions.Single(c => c.CharacterId == "c").Enabled, "c must not inherit b's disable");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("'b'") && s.Contains("disabled")));
    }

    [TestMethod]
    public void GetCompanions_WrongTypedNewField_KeepsEveryOtherCompanion()
    {
        WriteConfig(@"[
  { ""character_id"": ""a"", ""spawn_settlement"": ""t1"", ""race"": ""human"", ""hire_terms"": { ""gold"": ""lots"", ""requires_flags"": [""x""] } },
  { ""character_id"": ""b"", ""spawn_settlement"": ""t2"", ""race"": ""elf"" }
]");

        var companions = _sut.GetCompanions();

        Assert.AreEqual(2, companions.Count);
        Assert.AreEqual("a", companions[0].CharacterId);
        Assert.AreEqual("b", companions[1].CharacterId);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("ignored")));
    }

    [TestMethod]
    public void GetCompanions_LegacyEntry_ParsesWithOathDefaults()
    {
        WriteConfig(@"[{ ""character_id"": ""a"", ""spawn_settlement"": ""t1"", ""race"": ""human"" }]");

        var companion = _sut.GetCompanions()[0];

        Assert.IsFalse(companion.StayAtHome);
        Assert.IsNull(companion.HireTerms);
        Assert.AreEqual(0, companion.Topics?.Count ?? 0);
    }

    [TestMethod]
    public void GetCompanions_OathFieldsAreValidatedOnLoad()
    {
        WriteConfig(@"[{ ""character_id"": ""a"", ""spawn_settlement"": ""t1"", ""race"": ""human"", ""stay_at_home"": true,
  ""hire_terms"": { ""gold"": 5000000, ""items"": [{ ""item_id"": ""x"", ""count"": 2 }] } }]");

        var companion = _sut.GetCompanions()[0];

        Assert.IsTrue(companion.StayAtHome);
        Assert.IsNull(companion.HireTerms.Gold);
        Assert.AreEqual(1, companion.HireTerms.Items.Count);
    }

    private void WriteConfig(string content)
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "named_companions", "named_companion_config.json"),
            content);
    }
}
