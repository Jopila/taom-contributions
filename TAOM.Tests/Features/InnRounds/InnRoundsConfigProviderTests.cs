using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.InnRounds;

namespace TAOM.Tests.Features.InnRounds;

[TestClass]
public class InnRoundsConfigProviderTests
{
    private string _tempDir = null!;
    private IModLogger _logger = null!;

    // JSON key, property reader, default, lowest allowed, highest allowed.
    private static readonly (string Key, Func<InnRoundsConfig, float> Get, float Default, double Min, double Max)[] Fields =
    {
        ("maxRoundsPerDay", c => c.MaxRoundsPerDay, 3f, 1, 20),
        ("maxPricePerHead", c => c.MaxPricePerHead, 35f, 1, 1000),
        ("fallbackPricePerHead", c => c.FallbackPricePerHead, 27f, 1, 1000),
        ("moraleMin", c => c.MoraleMin, 20f, 0, 100),
        ("moraleMax", c => c.MoraleMax, 40f, 0, 100),
        ("renownMin", c => c.RenownMin, 1f, 0, 50),
        ("renownMax", c => c.RenownMax, 3f, 0, 50),
        ("charmXpMin", c => c.CharmXpMin, 20f, 0, 1000),
        ("charmXpMax", c => c.CharmXpMax, 60f, 0, 1000),
    };

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_InnRounds_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "inn_rounds"));
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private InnRoundsConfigProvider Provider()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(_tempDir);
        return new InnRoundsConfigProvider(paths, _logger);
    }

    private string JsonPath => Path.Combine(_tempDir, "inn_rounds", "inn_rounds.json");

    private InnRoundsConfig LoadJson(string body)
    {
        File.WriteAllText(JsonPath, body);
        return Provider().GetConfig();
    }

    private InnRoundsConfig LoadWith(string key, string jsonValue) => LoadJson("{ \"" + key + "\": " + jsonValue + " }");

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    [TestMethod]
    public void Defaults_AreTheApprovedValues()
    {
        var config = new InnRoundsConfig();

        foreach (var field in Fields)
            Assert.AreEqual(field.Default, field.Get(config), 0.00001f, field.Key);
        Assert.AreEqual(0, config.CultureDrinks.Count);
    }

    [TestMethod]
    public void EveryDeclaredProperty_HasARuleRow()
    {
        var declared = typeof(InnRoundsConfig).GetProperties().Select(p => p.Name).Where(n => n != nameof(InnRoundsConfig.CultureDrinks)).OrderBy(n => n).ToArray();
        var covered = Fields.Select(f => char.ToUpperInvariant(f.Key[0]) + f.Key.Substring(1)).OrderBy(n => n).ToArray();

        CollectionAssert.AreEqual(declared, covered);
    }

    [TestMethod]
    public void AMissingFile_GivesTheDefaults()
    {
        var config = Provider().GetConfig();

        Assert.AreEqual(3f, config.MaxRoundsPerDay);
    }

    [TestMethod]
    public void BrokenJson_GivesTheDefaults_AndLogsAnError()
    {
        var config = LoadJson("{ not json");

        Assert.AreEqual(35f, config.MaxPricePerHead);
        _logger.Received().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void NaNAndInfinity_RevertToTheDefaultAndWarn_ForEveryField()
    {
        foreach (var field in Fields)
            foreach (var bad in new[] { "NaN", "Infinity", "-Infinity" })
            {
                _logger.ClearReceivedCalls();
                var config = LoadWith(field.Key, bad);

                Assert.AreEqual(field.Default, field.Get(config), 0.00001f, field.Key + " = " + bad);
                _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(field.Key)));
                _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("invalid values")));
            }
    }

    [TestMethod]
    public void BelowAndAboveTheRange_RevertAndWarn_ForEveryField()
    {
        foreach (var field in Fields)
            foreach (var bad in new[] { field.Min - 1, field.Max + 1 })
            {
                _logger.ClearReceivedCalls();
                var config = LoadWith(field.Key, Num(bad));

                Assert.AreEqual(field.Default, field.Get(config), 0.00001f, field.Key + " = " + bad);
                _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(field.Key)));
            }
    }

    [TestMethod]
    public void TheBounds_AreKept_ForEveryField()
    {
        // A max-side field is tried at its top and a min-side field at its bottom, so each pair stays ordered.
        foreach (var field in Fields)
        {
            _logger.ClearReceivedCalls();
            var edges = field.Key.EndsWith("Min", StringComparison.Ordinal) ? new[] { field.Min }
                : field.Key.EndsWith("Max", StringComparison.Ordinal) ? new[] { field.Max }
                : new[] { field.Min, field.Max };
            foreach (var edge in edges)
                Assert.AreEqual((float)edge, field.Get(LoadWith(field.Key, Num(edge))), 0.00001f, field.Key + " = " + edge);
            _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        }
    }

    [TestMethod]
    public void OneBadValue_LeavesTheOthers_AndWarnsOnceAsSummary()
    {
        var config = LoadJson("{ \"maxRoundsPerDay\": 0, \"maxPricePerHead\": 80 }");

        Assert.AreEqual(3f, config.MaxRoundsPerDay);
        Assert.AreEqual(80f, config.MaxPricePerHead);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("invalid values")));
    }

    [TestMethod]
    public void AValidFile_LogsNoWarning()
    {
        LoadJson("{ \"maxRoundsPerDay\": 5, \"moraleMin\": 10, \"moraleMax\": 30 }");

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_IsCachedForTheProcess()
    {
        var provider = Provider();

        Assert.AreSame(provider.GetConfig(), provider.GetConfig());
    }

    [TestMethod]
    public void AMoraleMinAboveMax_RevertsBothToTheirDefaults()
    {
        var config = LoadJson("{ \"moraleMin\": 60, \"moraleMax\": 30 }");

        Assert.AreEqual(20f, config.MoraleMin);
        Assert.AreEqual(40f, config.MoraleMax);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("moraleMin")));
    }

    [TestMethod]
    public void ARenownMinAboveMax_AndACharmXpMinAboveMax_RevertToDefaults()
    {
        var config = LoadJson("{ \"renownMin\": 9, \"renownMax\": 2, \"charmXpMin\": 500, \"charmXpMax\": 100 }");

        Assert.AreEqual(1f, config.RenownMin);
        Assert.AreEqual(3f, config.RenownMax);
        Assert.AreEqual(20f, config.CharmXpMin);
        Assert.AreEqual(60f, config.CharmXpMax);
    }

    [TestMethod]
    public void EqualMinAndMax_AreAllowed()
    {
        var config = LoadJson("{ \"moraleMin\": 25, \"moraleMax\": 25 }");

        Assert.AreEqual(25f, config.MoraleMin);
        Assert.AreEqual(25f, config.MoraleMax);
    }

    [TestMethod]
    public void TheCultureMap_KeepsBeerAndWine_AndDropsAnythingElseWithAWarning()
    {
        var config = LoadJson("{ \"cultureDrinks\": { \"gondor\": \"wine\", \"erebor\": \"beer\", \"umbar\": \"whisky\", \"x\": \"\" } }");

        Assert.AreEqual("wine", config.CultureDrinks["gondor"]);
        Assert.AreEqual("beer", config.CultureDrinks["erebor"]);
        Assert.IsFalse(config.CultureDrinks.ContainsKey("umbar"));
        Assert.IsFalse(config.CultureDrinks.ContainsKey("x"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("umbar")));
    }

    [TestMethod]
    public void TheCultureMap_IsMatchedOnItsCultureIdIgnoringCase()
    {
        var config = LoadJson("{ \"cultureDrinks\": { \"Gondor\": \"Wine\" } }");

        Assert.AreEqual("wine", config.CultureDrinks["gondor"]);
    }

    [TestMethod]
    public void ANullCultureMap_GivesAnEmptyMap()
    {
        var config = LoadJson("{ \"cultureDrinks\": null }");

        Assert.AreEqual(0, config.CultureDrinks.Count);
    }

    [TestMethod]
    public void TheShippedFile_LoadsWithoutAWarning_AndKeepsTheDefaults()
    {
        var shipped = Path.Combine(TAOM.Tests.Infrastructure.RepoPaths.RepoPath("Main", "_Module", "ModuleData", "inn_rounds"), "inn_rounds.json");
        File.Copy(shipped, JsonPath);

        var config = Provider().GetConfig();

        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
        foreach (var field in Fields)
            Assert.AreEqual(field.Default, field.Get(config), 0.00001f, field.Key);
        Assert.IsTrue(config.CultureDrinks.Count >= 23);
    }
}
