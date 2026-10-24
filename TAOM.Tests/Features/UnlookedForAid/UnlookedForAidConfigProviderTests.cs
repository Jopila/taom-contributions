using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.UnlookedForAid;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.UnlookedForAid;

/// <summary>One test per validation rule (csharp-architecture.md "Config Providers MUST Validate"), plus the shipped file.</summary>
[TestClass]
public class UnlookedForAidConfigProviderTests
{
    private const float Default = 0.5f;

    private string _tempDir = null!;
    private IModLogger _logger = null!;
    private UnlookedForAidConfigProvider _sut = null!;

    private string ConfigPath => Path.Combine(_tempDir, "unlooked_for_aid", "unlooked_for_aid.json");

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_UnlookedForAid_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "unlooked_for_aid"));
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new UnlookedForAidConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void Write(string json) => File.WriteAllText(ConfigPath, json);

    private void AssertRevertedWithWarnings()
    {
        Assert.AreEqual(Default, _sut.MinStrengthRatio, 0f);
        _logger.Received(2).LogWarning(Arg.Any<string>());
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("MinStrengthRatio")));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("held an invalid value")));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void ValidValue_IsUsed_WithNoWarning()
    {
        Write("{ \"MinStrengthRatio\": 0.25 }");

        Assert.AreEqual(0.25f, _sut.MinStrengthRatio, 0f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Zero_IsTheLowestAcceptedValue()
    {
        Write("{ \"MinStrengthRatio\": 0 }");

        Assert.AreEqual(0f, _sut.MinStrengthRatio, 0f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void MissingFile_Defaults_WithAWarning()
    {
        Assert.AreEqual(Default, _sut.MinStrengthRatio, 0f);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("not found")));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void MalformedJson_Defaults_WithAnError()
    {
        Write("{ \"MinStrengthRatio\": ");

        Assert.AreEqual(Default, _sut.MinStrengthRatio, 0f);
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void WrongType_Defaults_WithAnError()
    {
        Write("{ \"MinStrengthRatio\": \"half\" }");

        Assert.AreEqual(Default, _sut.MinStrengthRatio, 0f);
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void MissingKey_Defaults_WithWarnings()
    {
        Write("{ }");

        AssertRevertedWithWarnings();
    }

    [TestMethod]
    public void EmptyFile_Defaults_WithWarnings()
    {
        Write("");

        AssertRevertedWithWarnings();
    }

    [TestMethod]
    public void Nan_Defaults_WithWarnings()
    {
        Write("{ \"MinStrengthRatio\": NaN }");

        AssertRevertedWithWarnings();
    }

    [DataTestMethod]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    public void Infinity_Defaults_WithWarnings(string literal)
    {
        Write("{ \"MinStrengthRatio\": " + literal + " }");

        AssertRevertedWithWarnings();
    }

    [TestMethod]
    public void Negative_Defaults_WithWarnings()
    {
        Write("{ \"MinStrengthRatio\": -0.1 }");

        AssertRevertedWithWarnings();
    }

    [DataTestMethod]
    [DataRow("1")]
    [DataRow("1.0")]
    [DataRow("2.5")]
    public void OneOrMore_Defaults_WithWarnings(string value)
    {
        Write("{ \"MinStrengthRatio\": " + value + " }");

        AssertRevertedWithWarnings();
    }

    [TestMethod]
    public void JustBelowOne_IsAccepted()
    {
        Write("{ \"MinStrengthRatio\": 0.99 }");

        Assert.AreEqual(0.99f, _sut.MinStrengthRatio, 0.0001f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void TheShippedFile_LoadsAsTheDefault_WithNoWarning()
    {
        var shipped = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        Assert.IsTrue(File.Exists(Path.Combine(shipped, "unlooked_for_aid", "unlooked_for_aid.json")),
            "Main/_Module/ModuleData/unlooked_for_aid/unlooked_for_aid.json is missing, so every install falls back to the default with a warning");
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(shipped);
        var logger = Substitute.For<IModLogger>();

        var provider = new UnlookedForAidConfigProvider(paths, logger);

        Assert.AreEqual(Default, provider.MinStrengthRatio, 0f);
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogError(Arg.Any<string>());
    }
}
