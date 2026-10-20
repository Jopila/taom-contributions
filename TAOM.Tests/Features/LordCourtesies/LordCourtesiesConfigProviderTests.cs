using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LordCourtesies;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LordCourtesies;

/// <summary>One test per validation rule, each run over every field that carries the rule.</summary>
[TestClass]
public class LordCourtesiesConfigProviderTests
{
    private string _tempDir = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_LordCourtesies_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "lord_courtesies"));
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private LordCourtesiesConfigProvider Provider(string? moduleData = null)
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(moduleData ?? _tempDir);
        return new LordCourtesiesConfigProvider(paths, _logger);
    }

    private void WriteJson(string json) =>
        File.WriteAllText(Path.Combine(_tempDir, "lord_courtesies", "lord_courtesies.json"), json);

    private LordCourtesiesConfig LoadWith(LordCourtesiesField field, string jsonValue)
    {
        WriteJson("{ \"" + field.Name + "\": " + jsonValue + " }");
        return Provider().GetConfig();
    }

    private static IOrderedEnumerable<LordCourtesiesField> Of(LordCourtesiesFieldKind kind) =>
        LordCourtesiesConfigProvider.Fields.Where(f => f.Kind == kind).OrderBy(f => f.Name, StringComparer.Ordinal);

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private void AssertReverted(LordCourtesiesField field, string jsonValue)
    {
        var config = LoadWith(field, jsonValue);

        Assert.AreEqual(field.Get(new LordCourtesiesConfig()), field.Get(config), $"{field.Name} = {jsonValue} was not reverted");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(field.Name)));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("invalid values")));
        _logger.ClearReceivedCalls();
    }

    private void AssertKept(LordCourtesiesField field, double value)
    {
        var config = LoadWith(field, Num(value));

        Assert.AreEqual((float)value, field.Get(config), 0f, $"{field.Name} = {value} was refused");
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.ClearReceivedCalls();
    }

    [TestMethod]
    public void Fields_CoverEveryNumericConfigPropertyOnce()
    {
        var properties = typeof(LordCourtesiesConfig).GetProperties()
            .Where(p => p.PropertyType == typeof(float)).Select(p => p.Name.ToLowerInvariant()).OrderBy(n => n).ToList();
        var fields = LordCourtesiesConfigProvider.Fields.Select(f => f.Name.ToLowerInvariant()).OrderBy(n => n).ToList();

        Assert.IsTrue(properties.Count > 0);
        CollectionAssert.AreEqual(properties, fields, "a config value has no validation rule, or a rule names no value");
    }

    [TestMethod]
    public void Config_HoldsOnlyTheRevealValues_BecauseTheGiftIsPaidByTheHandoverRule()
    {
        var names = typeof(LordCourtesiesConfig).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(new[] { "LandlessCost", "RefusalCost", "RevealMinRelation" }, names);
    }

    [TestMethod]
    public void GetConfig_TheShippedFile_LoadsCleanAndMatchesTheDefaults()
    {
        var moduleData = RepoPaths.RepoPath("Main", "_Module", "ModuleData");

        var config = Provider(moduleData).GetConfig();

        foreach (var field in LordCourtesiesConfigProvider.Fields)
            Assert.AreEqual(field.Get(new LordCourtesiesConfig()), field.Get(config), field.Name);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void TheShippedFile_NamesOnlyTheRevealValues()
    {
        var text = File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "lord_courtesies", "lord_courtesies.json"));

        Assert.IsFalse(text.Contains("giftTierDivisor") || text.Contains("giftCap") || text.Contains("highTierThreshold"));
    }

    [TestMethod]
    public void Defaults_AreTheApprovedValues()
    {
        var c = new LordCourtesiesConfig();

        Assert.AreEqual(5f, c.RevealMinRelation);
        Assert.AreEqual(1f, c.RefusalCost);
        Assert.AreEqual(5f, c.LandlessCost);
    }

    [TestMethod]
    public void GetConfig_MissingFile_WarnsAndUsesDefaults()
    {
        var config = Provider().GetConfig();

        Assert.AreEqual(5f, config.RevealMinRelation);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_LogsErrorAndUsesDefaults()
    {
        WriteJson("{ not json ");

        var config = Provider().GetConfig();

        Assert.AreEqual(5f, config.LandlessCost);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_JsonNull_UsesDefaults()
    {
        WriteJson("null");

        Assert.AreEqual(5f, Provider().GetConfig().RevealMinRelation);
    }

    [TestMethod]
    public void GetConfig_ValidEdit_IsAppliedWithoutWarnings()
    {
        WriteJson("{ \"landlessCost\": 4, \"revealMinRelation\": -10, \"refusalCost\": 2 }");

        var config = Provider().GetConfig();

        Assert.AreEqual(4f, config.LandlessCost);
        Assert.AreEqual(-10f, config.RevealMinRelation);
        Assert.AreEqual(2f, config.RefusalCost);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
    }

    [TestMethod]
    public void GetConfig_IsCachedForTheProcess()
    {
        var provider = Provider();

        Assert.AreSame(provider.GetConfig(), provider.GetConfig());
    }

    [TestMethod]
    public void GetConfig_OneBadValue_LeavesTheOthersAndWarnsOnceAsSummary()
    {
        WriteJson("{ \"landlessCost\": -3, \"refusalCost\": 2 }");

        var config = Provider().GetConfig();

        Assert.AreEqual(5f, config.LandlessCost);
        Assert.AreEqual(2f, config.RefusalCost);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("invalid values")));
    }

    [TestMethod]
    public void Amount_NegativeOrBeyondTheCap_Reverts()
    {
        Assert.IsTrue(Of(LordCourtesiesFieldKind.Amount).Any());
        foreach (var field in Of(LordCourtesiesFieldKind.Amount))
        {
            AssertReverted(field, "-1");
            AssertReverted(field, Num(1_000_001d));
        }
    }

    [TestMethod]
    public void Amount_AtZeroOrTheCap_IsKept()
    {
        foreach (var field in Of(LordCourtesiesFieldKind.Amount))
        {
            AssertKept(field, 0);
            AssertKept(field, 1_000_000d);
        }
    }

    [TestMethod]
    public void RelationLevel_BeyondPlusOrMinusHundred_Reverts()
    {
        Assert.IsTrue(Of(LordCourtesiesFieldKind.RelationLevel).Any());
        foreach (var field in Of(LordCourtesiesFieldKind.RelationLevel))
        {
            AssertReverted(field, "100.5");
            AssertReverted(field, "-100.5");
        }
    }

    [TestMethod]
    public void RelationLevel_AtTheBounds_IsKept()
    {
        foreach (var field in Of(LordCourtesiesFieldKind.RelationLevel))
        {
            AssertKept(field, -100);
            AssertKept(field, 100);
        }
    }

    [TestMethod]
    public void EveryKind_RejectsNaNAndInfinityBeforeTheRangeCheck()
    {
        foreach (var field in LordCourtesiesConfigProvider.Fields)
        {
            AssertReverted(field, "NaN");
            AssertReverted(field, "Infinity");
            AssertReverted(field, "-Infinity");
        }
    }
}
