using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.WarChronicle.Events;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>
/// <see cref="ChronicleConfigProvider"/>: the shipped path, a missing file, a file with problems (one warning
/// each plus a summary) and an unreadable file. The validator's rules are ChronicleConfigValidatorTests'.
/// </summary>
[TestClass]
public class ChronicleConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IPathService _pathService = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Chronicle_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "war_chronicle");
        Directory.CreateDirectory(_featureDir);

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string FilePath => Path.Combine(_featureDir, "war_chronicle.json");

    [TestMethod]
    public void GetEvents_TheShippedFile_LoadsThirteenEventsWithNoWarning()
    {
        _pathService.ModuleDataPath.Returns(RepoPaths.RepoPath("Main", "_Module", "ModuleData"));

        var events = new ChronicleConfigProvider(_pathService, _logger).GetEvents();

        Assert.AreEqual(13, events.Count);
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("Loaded war_chronicle.json (13 events)")));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetEvents_MissingFile_LoadsNothingAndWarnsOnce()
    {
        var events = new ChronicleConfigProvider(_pathService, _logger).GetEvents();

        Assert.AreEqual(0, events.Count);
        _logger.Received(1).LogWarning(Arg.Any<string>());
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("not found")));
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetEvents_AFileWithTwoProblems_WarnsForEachAndOnceInSummary()
    {
        File.WriteAllText(FilePath, "{ \"schemaVersion\": 1, \"events\": [ null, 5 ] }");

        var events = new ChronicleConfigProvider(_pathService, _logger).GetEvents();

        Assert.AreEqual(0, events.Count);
        _logger.Received(3).LogWarning(Arg.Any<string>());
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("contained invalid values")));
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetEvents_AnUnreadableFile_LoadsNothingAndLogsAnError()
    {
        File.WriteAllText(FilePath, "{ \"schemaVersion\": 1, \"events\": [] }");
        var sut = new ChronicleConfigProvider(_pathService, _logger);

        int count;
        using (new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            count = sut.GetEvents().Count;

        Assert.AreEqual(0, count);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("could not read")));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetEvents_CalledTwice_ReadsTheFileOnce()
    {
        _pathService.ModuleDataPath.Returns(RepoPaths.RepoPath("Main", "_Module", "ModuleData"));
        var sut = new ChronicleConfigProvider(_pathService, _logger);

        Assert.AreSame(sut.GetEvents(), sut.GetEvents());
        _logger.Received(1).LogInfo(Arg.Any<string>());
    }
}
