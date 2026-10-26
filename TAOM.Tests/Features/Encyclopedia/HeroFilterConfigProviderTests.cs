using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Encyclopedia;

namespace TAOM.Tests.Features.Encyclopedia;

/// <summary>Every validation rule of hero_filters.json (#907): a bad group is dropped, never repaired.</summary>
[TestClass]
public class HeroFilterConfigProviderTests
{
    private string _tempDir = null!;
    private IModLogger _logger = null!;
    private HeroFilterConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_test_herofilters_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "encyclopedia"));
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new HeroFilterConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    private HeroFilterConfig Load(string json)
    {
        File.WriteAllText(Path.Combine(_tempDir, "encyclopedia", "hero_filters.json"), json);
        return _sut.LoadConfig();
    }

    private static string Group(string id, string races, string bounds) =>
        "{ \"id\": \"" + id + "\", \"races\": [" + races + "], \"ageUpperBounds\": [" + bounds + "] }";

    private static string Doc(string? defaultGroup, params string[] groups) =>
        "{ " + (defaultGroup == null ? "" : "\"defaultGroup\": \"" + defaultGroup + "\", ") + "\"groups\": [" + string.Join(",", groups) + "] }";

    [TestMethod]
    public void LoadConfig_ValidJson_ParsesGroupsInOrder_WithNoWarning()
    {
        var config = Load(Doc("men", Group("men", "\"human\"", "25,35,45,55"), Group("elves", "\"elf\"", "24,28,32,40")));

        CollectionAssert.AreEqual(new[] { "men", "elves" }, config.Groups.Select(g => g.Id).ToList());
        CollectionAssert.AreEqual(new[] { 25f, 35f, 45f, 55f }, config.Groups[0].AgeUpperBounds);
        CollectionAssert.AreEqual(new[] { "human" }, config.Groups[0].Races);
        Assert.AreEqual("men", config.DefaultGroup);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("25,35,45")]
    [DataRow("25,35,45,55,65")]
    [DataRow("")]
    public void LoadConfig_WrongBoundCount_DropsTheGroup(string bounds)
    {
        var config = Load(Doc("men", Group("men", "\"human\"", "25,35,45,55"), Group("elves", "\"elf\"", bounds)));

        CollectionAssert.AreEqual(new[] { "men" }, config.Groups.Select(g => g.Id).ToList());
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("elves")));
    }

    [DataTestMethod]
    [DataRow("18,35,45,55")]
    [DataRow("10,35,45,55")]
    [DataRow("25,35,45,10001")]
    [DataRow("25,35,35,55")]
    [DataRow("25,45,35,55")]
    [DataRow("-5,35,45,55")]
    public void LoadConfig_BoundAtOrBelow18_AboveTenThousand_OrNotStrictlyIncreasing_DropsTheGroup(string bounds)
    {
        var config = Load(Doc("men", Group("men", "\"human\"", "25,35,45,55"), Group("elves", "\"elf\"", bounds)));

        CollectionAssert.AreEqual(new[] { "men" }, config.Groups.Select(g => g.Id).ToList());
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("elves")));
    }

    [TestMethod]
    public void LoadConfig_BoundAtTenThousand_IsKept()
    {
        var config = Load(Doc("men", Group("men", "\"human\"", "25,35,45,10000")));

        Assert.AreEqual(1, config.Groups.Count);
    }

    [DataTestMethod]
    [DataRow("Infinity")]
    [DataRow("\"NaN\"")]
    public void LoadConfig_NonFiniteBound_DropsTheGroup(string bound)
    {
        var config = Load(Doc("men", Group("men", "\"human\"", "25,35,45,55"), Group("elves", "\"elf\"", "24,28,32," + bound)));

        CollectionAssert.AreEqual(new[] { "men" }, config.Groups.Select(g => g.Id).ToList());
    }

    [TestMethod]
    public void LoadConfig_DuplicateGroupId_KeepsTheFirst()
    {
        var config = Load(Doc("men", Group("men", "\"human\"", "25,35,45,55"), Group("men", "\"elf\"", "24,28,32,40")));

        Assert.AreEqual(1, config.Groups.Count);
        CollectionAssert.AreEqual(new[] { "human" }, config.Groups[0].Races);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("men")));
    }

    [TestMethod]
    public void LoadConfig_RaceInTwoGroups_KeepsTheFirstAndWarns()
    {
        var config = Load(Doc("men", Group("men", "\"human\",\"elf\"", "25,35,45,55"), Group("elves", "\"elf\"", "24,28,32,40")));

        CollectionAssert.AreEqual(new[] { "human", "elf" }, config.Groups[0].Races);
        Assert.AreEqual(0, config.Groups[1].Races.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("elf")));
    }

    [TestMethod]
    public void LoadConfig_EmptyOrWhitespaceRaceId_IsDropped()
    {
        var config = Load(Doc("men", Group("men", "\"human\",\"\",\"  \"", "25,35,45,55")));

        CollectionAssert.AreEqual(new[] { "human" }, config.Groups[0].Races);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void LoadConfig_UnknownGroupId_IsDropped()
    {
        var config = Load(Doc("men", Group("men", "\"human\"", "25,35,45,55"), Group("hobbits", "\"hobbit\"", "24,28,32,40")));

        CollectionAssert.AreEqual(new[] { "men" }, config.Groups.Select(g => g.Id).ToList());
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("hobbits")));
    }

    [TestMethod]
    public void LoadConfig_MissingDefaultGroup_FallsBackToTheFirstValidGroup()
    {
        var config = Load(Doc(null, Group("elves", "\"elf\"", "24,28,32,40"), Group("men", "\"human\"", "25,35,45,55")));

        Assert.AreEqual("elves", config.DefaultGroup);
    }

    [TestMethod]
    public void LoadConfig_DefaultGroupNamingADroppedGroup_FallsBackToTheFirstValidGroup()
    {
        var config = Load(Doc("elves", Group("elves", "\"elf\"", "24,28"), Group("men", "\"human\"", "25,35,45,55")));

        Assert.AreEqual("men", config.DefaultGroup);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void LoadConfig_AnyFailure_EndsWithOneSummaryWarning()
    {
        Load(Doc("men", Group("men", "\"human\"", "25,35,45,55"), Group("elves", "\"elf\"", "24,28"), Group("dwarves", "\"dwarf\"", "20,25")));

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("dropped 2")));
        _logger.Received(3).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void LoadConfig_MissingFile_ReturnsNoGroups_AndLogsAnError()
    {
        var config = _sut.LoadConfig();

        Assert.AreEqual(0, config.Groups.Count);
        _logger.Received().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void LoadConfig_ParseError_ReturnsNoGroups_AndLogsAnError()
    {
        var config = Load("{ not json");

        Assert.AreEqual(0, config.Groups.Count);
        _logger.Received().LogError(Arg.Any<string>());
    }
}
