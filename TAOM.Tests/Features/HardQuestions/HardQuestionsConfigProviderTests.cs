using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.HardQuestions;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HardQuestions;

/// <summary>One test per validation rule, each run over every field that carries the rule.</summary>
[TestClass]
public class HardQuestionsConfigProviderTests
{
    private string _tempDir = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_HardQuestions_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "hard_questions"));
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private HardQuestionsConfigProvider Provider(string? moduleData = null)
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(moduleData ?? _tempDir);
        return new HardQuestionsConfigProvider(paths, _logger);
    }

    private HardQuestionsConfig LoadWith(HardQuestionsField field, string jsonValue)
    {
        File.WriteAllText(Path.Combine(_tempDir, "hard_questions", "hard_questions.json"),
            "{ \"" + field.Name + "\": " + jsonValue + " }");
        return Provider().GetConfig();
    }

    private static IOrderedEnumerable<HardQuestionsField> Of(HardQuestionsFieldKind kind) =>
        HardQuestionsConfigProvider.Fields.Where(f => f.Kind == kind).OrderBy(f => f.Name, StringComparer.Ordinal);

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private void AssertReverted(HardQuestionsField field, string jsonValue)
    {
        var config = LoadWith(field, jsonValue);

        Assert.AreEqual(field.Get(new HardQuestionsConfig()), field.Get(config), $"{field.Name} = {jsonValue} was not reverted");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(field.Name)));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("invalid values")));
        _logger.ClearReceivedCalls();
    }

    private void AssertKept(HardQuestionsField field, double value)
    {
        var config = LoadWith(field, Num(value));

        Assert.AreEqual((float)value, field.Get(config), 0f, $"{field.Name} = {value} was refused");
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.ClearReceivedCalls();
    }

    [TestMethod]
    public void Fields_CoverEveryNumericConfigPropertyOnce()
    {
        var properties = typeof(HardQuestionsConfig).GetProperties()
            .Where(p => p.PropertyType == typeof(float)).Select(p => p.Name.ToLowerInvariant()).OrderBy(n => n).ToList();
        var fields = HardQuestionsConfigProvider.Fields.Select(f => f.Name.ToLowerInvariant()).OrderBy(n => n).ToList();

        CollectionAssert.AreEqual(properties, fields, "a config value has no validation rule, or a rule names no value");
    }

    [TestMethod]
    public void GetConfig_TheShippedFile_LoadsCleanAndMatchesTheDefaults()
    {
        var moduleData = RepoPaths.RepoPath("Main", "_Module", "ModuleData");

        var config = Provider(moduleData).GetConfig();

        foreach (var field in HardQuestionsConfigProvider.Fields)
            Assert.AreEqual(field.Get(new HardQuestionsConfig()), field.Get(config), field.Name);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_MissingFile_WarnsAndUsesDefaults()
    {
        var config = Provider().GetConfig();

        Assert.AreEqual(1.5f, config.BribeMultiplierClan);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_LogsErrorAndUsesDefaults()
    {
        File.WriteAllText(Path.Combine(_tempDir, "hard_questions", "hard_questions.json"), "{ not json ");

        var config = Provider().GetConfig();

        Assert.AreEqual(3.5f, config.BribeMultiplierKingdom);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_JsonNull_UsesDefaults()
    {
        File.WriteAllText(Path.Combine(_tempDir, "hard_questions", "hard_questions.json"), "null");

        Assert.AreEqual(0.15f, Provider().GetConfig().WoundChance);
    }

    [TestMethod]
    public void GetConfig_ValidEdit_IsAppliedWithoutWarnings()
    {
        File.WriteAllText(Path.Combine(_tempDir, "hard_questions", "hard_questions.json"),
            "{ \"woundChance\": 0.5, \"stripRenownLoss\": -50, \"charmXpClan\": 10 }");

        var config = Provider().GetConfig();

        Assert.AreEqual(0.5f, config.WoundChance);
        Assert.AreEqual(-50f, config.StripRenownLoss);
        Assert.AreEqual(10f, config.CharmXpClan);
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
        File.WriteAllText(Path.Combine(_tempDir, "hard_questions", "hard_questions.json"),
            "{ \"woundChance\": 4, \"charmXpClan\": 10 }");

        var config = Provider().GetConfig();

        Assert.AreEqual(0.15f, config.WoundChance);
        Assert.AreEqual(10f, config.CharmXpClan);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("invalid values")));
    }

    [TestMethod]
    public void Chance_OutsideZeroToOne_Reverts()
    {
        foreach (var field in Of(HardQuestionsFieldKind.Chance))
        {
            AssertReverted(field, "1.01");
            AssertReverted(field, "-0.01");
        }
    }

    [TestMethod]
    public void Chance_AtTheBounds_IsKept()
    {
        foreach (var field in Of(HardQuestionsFieldKind.Chance))
        {
            AssertKept(field, 0);
            AssertKept(field, 1);
        }
    }

    [TestMethod]
    public void Penalty_ThatIsPositive_Reverts()
    {
        foreach (var field in Of(HardQuestionsFieldKind.Penalty))
            AssertReverted(field, "5");
    }

    [TestMethod]
    public void Penalty_BeyondTheMagnitudeCap_Reverts()
    {
        foreach (var field in Of(HardQuestionsFieldKind.Penalty))
            AssertReverted(field, Num(-1_000_001d));
    }

    [TestMethod]
    public void Penalty_AtZeroOrTheCap_IsKept()
    {
        foreach (var field in Of(HardQuestionsFieldKind.Penalty))
        {
            AssertKept(field, 0);
            AssertKept(field, -1_000_000d);
        }
    }

    [TestMethod]
    public void Reward_ThatIsNegative_Reverts()
    {
        foreach (var field in Of(HardQuestionsFieldKind.Reward))
            AssertReverted(field, "-1");
    }

    [TestMethod]
    public void Reward_BeyondTheMagnitudeCap_Reverts()
    {
        foreach (var field in Of(HardQuestionsFieldKind.Reward))
            AssertReverted(field, Num(1_000_001d));
    }

    [TestMethod]
    public void Reward_AtZeroOrTheCap_IsKept()
    {
        foreach (var field in Of(HardQuestionsFieldKind.Reward))
        {
            AssertKept(field, 0);
            AssertKept(field, 1_000_000d);
        }
    }

    [TestMethod]
    public void Multiplier_ThatIsNegative_Reverts()
    {
        foreach (var field in Of(HardQuestionsFieldKind.Multiplier))
            AssertReverted(field, "-0.5");
    }

    [TestMethod]
    public void Multiplier_BeyondTheCap_Reverts()
    {
        foreach (var field in Of(HardQuestionsFieldKind.Multiplier))
            AssertReverted(field, "1000.5");
    }

    [TestMethod]
    public void Multiplier_AtZeroOrTheCap_IsKept()
    {
        foreach (var field in Of(HardQuestionsFieldKind.Multiplier))
        {
            AssertKept(field, 0);
            AssertKept(field, 1000);
        }
    }

    [TestMethod]
    public void EveryKind_RejectsNaNAndInfinityBeforeTheRangeCheck()
    {
        foreach (var field in HardQuestionsConfigProvider.Fields)
        {
            AssertReverted(field, "NaN");
            AssertReverted(field, "Infinity");
            AssertReverted(field, "-Infinity");
        }
    }
}
