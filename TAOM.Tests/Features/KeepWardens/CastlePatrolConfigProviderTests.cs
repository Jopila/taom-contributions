using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.KeepWardens;

namespace TAOM.Tests.Features.KeepWardens;

/// <summary>
/// One test per validation rule (csharp-architecture.md "Config Providers MUST Validate"). Each array reverts to its default
/// on its own; one summary warning names every field that reverted.
/// </summary>
[TestClass]
public class CastlePatrolConfigProviderTests
{
    private static readonly float[] DefaultDays = { 10f, 8f, 6f };
    private static readonly int[] DefaultSizes = { 15, 20, 25 };

    private string _tempDir = null!;
    private IModLogger _logger = null!;
    private CastlePatrolConfigProvider _sut = null!;

    private string ConfigPath => Path.Combine(_tempDir, "configs", "castle_patrols.json");

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_CastlePatrol_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "configs"));
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new CastlePatrolConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void Write(string json) => File.WriteAllText(ConfigPath, json);

    private static string Json(string days, string sizes) => "{ \"spawnDays\": " + days + ", \"sizeLimits\": " + sizes + " }";

    private void AssertDefaults()
    {
        CollectionAssert.AreEqual(DefaultDays, _sut.SpawnDays.ToArray());
        CollectionAssert.AreEqual(DefaultSizes, _sut.SizeLimits.ToArray());
    }

    [TestMethod]
    public void MissingFile_Defaults()
    {
        AssertDefaults();
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void MalformedJson_Defaults()
    {
        Write("{ not json");

        AssertDefaults();
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("could not be read")));
    }

    [TestMethod]
    public void NotAnObject_Defaults()
    {
        Write("[1, 2, 3]");

        AssertDefaults();
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("not a JSON object")));
    }

    [TestMethod]
    public void ValidFile_IsUsed_WithNoWarning()
    {
        Write(Json("[12, 9.5, 3]", "[30, 40, 50]"));

        CollectionAssert.AreEqual(new[] { 12f, 9.5f, 3f }, _sut.SpawnDays.ToArray());
        CollectionAssert.AreEqual(new[] { 30, 40, 50 }, _sut.SizeLimits.ToArray());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("[NaN, 8, 6]")]
    [DataRow("[10, Infinity, 6]")]
    [DataRow("[10, 8, -Infinity]")]
    public void NaN_Or_Infinity_Days_Revert(string days)
    {
        Write(Json(days, "[15, 20, 25]"));

        AssertDefaults();
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("'spawnDays'")));
    }

    [DataTestMethod]
    [DataRow("[0, 8, 6]")]
    [DataRow("[10, -1, 6]")]
    [DataRow("[10, 8, 60.5]")]
    [DataRow("[10, 8, 1000000]")]
    [DataRow("[10, 8, 99999999999999999999]")]
    [DataRow("[10, 8, \"6\"]")]
    public void Days_OutOfRange_Or_NotANumber_Revert(string days)
    {
        Write(Json(days, "[15, 20, 25]"));

        AssertDefaults();
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("'spawnDays'")));
    }

    [TestMethod]
    public void Days_AtTheMaximum_AreKept()
    {
        Write(Json("[60, 8, 6]", "[15, 20, 25]"));

        Assert.AreEqual(60f, _sut.SpawnDays[0]);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("[10, 8]")]
    [DataRow("[10, 8, 6, 4]")]
    [DataRow("[]")]
    [DataRow("10")]
    public void Days_WrongLength_Or_NotAnArray_Revert(string days)
    {
        Write(Json(days, "[15, 20, 25]"));

        AssertDefaults();
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("'spawnDays'")));
    }

    [DataTestMethod]
    [DataRow("[0, 20, 25]")]
    [DataRow("[15, -5, 25]")]
    [DataRow("[15, 20, 501]")]
    [DataRow("[15, 20.5, 25]")]
    [DataRow("[15, 20, 9999999999]")]
    [DataRow("[15, 20, 99999999999999999999]")]
    [DataRow("[15, 20, null]")]
    public void SizeLimits_OutOfRange_Fractional_Or_NotANumber_Revert(string sizes)
    {
        Write(Json("[10, 8, 6]", sizes));

        AssertDefaults();
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("'sizeLimits'")));
    }

    [TestMethod]
    public void SizeLimits_AtTheMaximum_AreKept()
    {
        Write(Json("[10, 8, 6]", "[500, 20, 25]"));

        Assert.AreEqual(500, _sut.SizeLimits[0]);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("[15, 20]")]
    [DataRow("[15, 20, 25, 30]")]
    [DataRow("20")]
    public void SizeLimits_WrongLength_Or_NotAnArray_Revert(string sizes)
    {
        Write(Json("[10, 8, 6]", sizes));

        AssertDefaults();
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("'sizeLimits'")));
    }

    [TestMethod]
    public void OneBadField_DoesNotRevertTheOther()
    {
        Write(Json("[0, 8, 6]", "[30, 40, 50]"));

        CollectionAssert.AreEqual(DefaultDays, _sut.SpawnDays.ToArray());
        CollectionAssert.AreEqual(new[] { 30, 40, 50 }, _sut.SizeLimits.ToArray());
    }

    [TestMethod]
    public void MissingFields_Revert_AndOneSummaryWarningNamesBoth()
    {
        Write("{ }");

        AssertDefaults();
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("'spawnDays'") && s.Contains("'sizeLimits'")));
    }

    [TestMethod]
    public void TheLoad_HappensOnce()
    {
        Write(Json("[12, 9, 3]", "[30, 40, 50]"));
        _ = _sut.SpawnDays;
        Write("{ not json");

        Assert.AreEqual(12f, _sut.SpawnDays[0]);
        Assert.AreEqual(30, _sut.SizeLimits[0]);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void SizeLimits_BeyondLongRange_Revert_NeverThrow()
    {
        Write(Json("[10, 8, 6]", "[15, 20, 99999999999999999999]"));

        AssertDefaults();
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("'sizeLimits'")));
    }

    [TestMethod]
    public void ThePathServiceThrows_Defaults_NeverThrows_WarnsOnce()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_ => throw new System.InvalidOperationException("no module root"));
        var sut = new CastlePatrolConfigProvider(pathService, _logger);

        CollectionAssert.AreEqual(DefaultDays, sut.SpawnDays.ToArray());
        CollectionAssert.AreEqual(DefaultSizes, sut.SizeLimits.ToArray());
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }
}
