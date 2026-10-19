using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FromTheAshes;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FromTheAshes;

/// <summary>
/// One test per validation rule (csharp-architecture.md "Config Providers MUST Validate"). Each field reverts to its
/// default on its own; one summary warning names every field that reverted.
/// </summary>
[TestClass]
public class FromTheAshesConfigProviderTests
{
    private string _tempDir = null!;
    private IModLogger _logger = null!;
    private IPathService _paths = null!;

    private string ConfigPath => Path.Combine(_tempDir, "settlement_buildings", "from_the_ashes.json");

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Ashes_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "settlement_buildings"));
        _paths = Substitute.For<IPathService>();
        _paths.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private FromTheAshesConfigProvider Sut() => new(_paths, _logger);

    /// <summary>The shipped file as JSON, with <paramref name="edit"/> applied before it is written.</summary>
    private FromTheAshesConfigProvider LoadEdited(System.Action<JObject> edit)
    {
        var json = JObject.Parse(File.ReadAllText(Path.Combine(
            RepoPaths.RepoPath("Main", "_Module", "ModuleData"), "settlement_buildings", "from_the_ashes.json")));
        edit(json);
        File.WriteAllText(ConfigPath, json.ToString());
        return Sut();
    }

    private static void AssertDefaults(FromTheAshesConfigProvider sut)
    {
        Assert.AreEqual(24f, sut.Rebuild.Hours);
        Assert.AreEqual(3, sut.Rebuild.NotableRelation);
        Assert.AreEqual(-1, sut.Rebuild.StopPenalty);
        Assert.AreEqual(0, sut.Thralls.NotableRelation);
        Assert.AreEqual(20f, sut.Project.Hours);
        Assert.AreEqual(40, sut.Project.Labour);
        Assert.AreEqual(15f, sut.Project.ConstructionPerHour);
    }

    [TestMethod]
    public void MissingFile_Defaults_WarnsOnce()
    {
        AssertDefaults(Sut());
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void ValidFile_IsUsed_WithNoWarning()
    {
        var sut = LoadEdited(j =>
        {
            j["rebuild"]!["hours"] = 48;
            j["project"]!["clay"] = 7;
        });

        Assert.AreEqual(48f, sut.Rebuild.Hours);
        Assert.AreEqual(7, sut.Project.Clay);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("-4")]
    [DataRow("0.5")]
    [DataRow("169")]
    public void Hours_BelowOneOrAbove168_Revert(string hours)
    {
        var sut = LoadEdited(j => j["rebuild"]!["hours"] = JToken.Parse(hours));

        Assert.AreEqual(24f, sut.Rebuild.Hours);
        Assert.AreEqual(3, sut.Rebuild.NotableRelation, "a valid field of the same object is kept");
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    public void Hours_NaN_Or_Infinity_Revert(string hours)
    {
        File.WriteAllText(ConfigPath, File.ReadAllText(Path.Combine(
            RepoPaths.RepoPath("Main", "_Module", "ModuleData"), "settlement_buildings", "from_the_ashes.json"))
            .Replace("\"hours\": 24,", "\"hours\": " + hours + ","));
        var sut = Sut();

        Assert.AreEqual(24f, sut.Rebuild.Hours);
        Assert.AreEqual(24f, sut.Thralls.Hours);
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ConstructionPerHour_NaN_Reverts()
    {
        File.WriteAllText(ConfigPath, File.ReadAllText(Path.Combine(
            RepoPaths.RepoPath("Main", "_Module", "ModuleData"), "settlement_buildings", "from_the_ashes.json"))
            .Replace("\"constructionPerHour\": 15", "\"constructionPerHour\": NaN"));

        Assert.AreEqual(15f, Sut().Project.ConstructionPerHour);
    }

    [DataTestMethod]
    [DataRow("labour", 501)]
    [DataRow("labour", -1)]
    [DataRow("hardwood", 1001)]
    [DataRow("clay", -1)]
    [DataRow("tools", 1001)]
    [DataRow("iron", -3)]
    [DataRow("notableRelation", 21)]
    [DataRow("ownerRelation", -1)]
    [DataRow("athleticsXp", 5001)]
    public void WholeField_OutsideItsRange_RevertsOnlyThatField(string field, int value)
    {
        var sut = LoadEdited(j =>
        {
            j["rebuild"]![field] = value;
            j["rebuild"]!["hours"] = 30;
        });

        Assert.AreEqual(30f, sut.Rebuild.Hours, "the valid field is kept");
        Assert.AreEqual(Default(field), Read(sut.Rebuild, field));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("rebuild." + field)));
    }

    [TestMethod]
    public void ConstructionPerHour_Negative_Or_Above1000_Reverts()
    {
        Assert.AreEqual(15f, LoadEdited(j => j["project"]!["constructionPerHour"] = -1).Project.ConstructionPerHour);
        Assert.AreEqual(15f, LoadEdited(j => j["project"]!["constructionPerHour"] = 1001).Project.ConstructionPerHour);
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(-21)]
    [DataRow(-1000)]
    public void StopPenalty_PositiveOrBelowMinus20_Reverts(int penalty)
    {
        var sut = LoadEdited(j => j["rebuild"]!["stopPenalty"] = penalty);

        Assert.AreEqual(-1, sut.Rebuild.StopPenalty);
    }

    [TestMethod]
    public void WholeField_Fractional_Reverts()
    {
        var sut = LoadEdited(j => j["rebuild"]!["labour"] = 20.5);

        Assert.AreEqual(20, sut.Rebuild.Labour);
    }

    [TestMethod]
    public void WholeField_BeyondLongRange_RevertsOnlyItself_NeverThrows()
    {
        File.WriteAllText(ConfigPath, File.ReadAllText(Path.Combine(
            RepoPaths.RepoPath("Main", "_Module", "ModuleData"), "settlement_buildings", "from_the_ashes.json"))
            .Replace("\"hardwood\": 40,", "\"hardwood\": 99999999999999999999,"));
        var sut = Sut();

        Assert.AreEqual(40, sut.Rebuild.Hardwood);
        Assert.AreEqual(24f, sut.Rebuild.Hours, "the valid fields are kept");
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("out of range")));
    }

    [TestMethod]
    public void FloatField_BeyondLongRange_RevertsOnlyItself_NeverThrows()
    {
        File.WriteAllText(ConfigPath, File.ReadAllText(Path.Combine(
            RepoPaths.RepoPath("Main", "_Module", "ModuleData"), "settlement_buildings", "from_the_ashes.json"))
            .Replace("\"constructionPerHour\": 15", "\"constructionPerHour\": 99999999999999999999"));
        var sut = Sut();

        Assert.AreEqual(15f, sut.Project.ConstructionPerHour);
        Assert.AreEqual(20f, sut.Project.Hours, "the valid fields are kept");
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("out of range")));
    }

    [TestMethod]
    public void WrongType_Reverts_OnlyThatField()
    {
        var sut = LoadEdited(j => j["rebuild"]!["labour"] = "twenty");

        Assert.AreEqual(20, sut.Rebuild.Labour);
        Assert.AreEqual(40, sut.Rebuild.Hardwood);
    }

    [TestMethod]
    public void MissingField_Reverts_OnlyThatField()
    {
        var sut = LoadEdited(j =>
        {
            ((JObject)j["project"]!).Remove("iron");
            j["project"]!["tools"] = 9;
        });

        Assert.AreEqual(5, sut.Project.Iron);
        Assert.AreEqual(9, sut.Project.Tools);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("project.iron") && s.Contains("missing")));
    }

    [TestMethod]
    public void MissingVariant_RevertsThatVariantOnly()
    {
        var sut = LoadEdited(j =>
        {
            j.Remove("thralls");
            j["rebuild"]!["hours"] = 30;
        });

        Assert.AreEqual(0, sut.Thralls.NotableRelation);
        Assert.AreEqual(24f, sut.Thralls.Hours);
        Assert.AreEqual(30f, sut.Rebuild.Hours);
    }

    [DataTestMethod]
    [DataRow("[]")]
    [DataRow("not json")]
    [DataRow("{}")]
    [DataRow("{ \"rebuild\": 4, \"thralls\": [], \"project\": null }")]
    public void WrongJsonShape_Reverts_NeverThrows(string text)
    {
        File.WriteAllText(ConfigPath, text);

        AssertDefaults(Sut());
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void SummaryWarning_NamesEveryRevertedField_Once()
    {
        _ = LoadEdited(j =>
        {
            j["rebuild"]!["labour"] = 9999;
            j["project"]!["hours"] = -5;
        }).Rebuild;

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("rebuild.labour") && s.Contains("project.hours")));
    }

    [DataTestMethod]
    [DataRow("hours", 1)]
    [DataRow("hours", 168)]
    [DataRow("labour", 0)]
    [DataRow("labour", 500)]
    [DataRow("hardwood", 1000)]
    [DataRow("notableRelation", 20)]
    [DataRow("stopPenalty", -20)]
    [DataRow("athleticsXp", 5000)]
    public void AcceptedEdges_AreKept_WithNoWarning(string field, int value)
    {
        var sut = LoadEdited(j => j["rebuild"]![field] = value);

        Assert.AreEqual((double)value, System.Convert.ToDouble(Read(sut.Rebuild, field)));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ThePathServiceThrows_Defaults_NeverThrows_WarnsOnce()
    {
        _paths.ModuleDataPath.Returns(_ => throw new System.InvalidOperationException("no module root"));

        AssertDefaults(Sut());
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void TheSchemaIsPerVariant_AProjectNeedsNoOwnerRelationOrStopPenalty_ARebuildNeedsNoConstruction()
    {
        var sut = LoadEdited(j =>
        {
            Assert.IsNull(j["project"]!["ownerRelation"], "the shipped project object omits the rebuild-only fields");
            Assert.IsNull(j["project"]!["stopPenalty"]);
            Assert.IsNull(j["rebuild"]!["constructionPerHour"], "the shipped rebuild object omits the project-only field");
            Assert.IsNull(j["thralls"]!["constructionPerHour"]);
        });

        Assert.AreEqual(15f, sut.Project.ConstructionPerHour);
        Assert.AreEqual(5, sut.Rebuild.OwnerRelation);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void AFieldOutsideItsVariant_IsIgnored_NotRead_NotWarnedAbout()
    {
        var sut = LoadEdited(j =>
        {
            j["project"]!["ownerRelation"] = 999;
            j["project"]!["stopPenalty"] = 99;
            j["rebuild"]!["constructionPerHour"] = "not a number";
            j["thralls"]!["constructionPerHour"] = -5;
        });

        Assert.AreEqual(0, sut.Project.OwnerRelation, "a rebuild-only field never reaches the project");
        Assert.AreEqual(0, sut.Project.StopPenalty);
        Assert.AreEqual(0f, sut.Rebuild.ConstructionPerHour);
        Assert.AreEqual(0f, sut.Thralls.ConstructionPerHour);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void AShortProjectObject_NamesItsOwnMissingField_NotTheRebuildOnes()
    {
        _ = LoadEdited(j => ((JObject)j["project"]!).Remove("constructionPerHour")).Project;

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("project.constructionPerHour") && !m.Contains("ownerRelation")));
    }

    [TestMethod]
    public void ReturnedConfigs_AreDistinctObjects()
    {
        var sut = Sut();

        Assert.AreNotSame(sut.Rebuild, sut.Thralls);
    }

    private static object Read(AshesVariantConfig config, string field) =>
        typeof(AshesVariantConfig).GetProperty(char.ToUpperInvariant(field[0]) + field.Substring(1))!.GetValue(config)!;

    private static object Default(string field) => Read(FromTheAshesConfigProvider.DefaultRebuild(), field);
}
