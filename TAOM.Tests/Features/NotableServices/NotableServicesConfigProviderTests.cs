using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.NotableServices;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.NotableServices;

[TestClass]
public class NotableServicesConfigProviderTests
{
    private string _tempDir = null!;
    private IModLogger _logger = null!;

    // JSON key, property reader, default, lowest allowed, highest allowed, whether the lowest is excluded.
    private static readonly (string Key, Func<NotableServicesConfig, float> Get, float Default, double Min, double Max, bool MinExclusive)[] Fields =
    {
        ("introducePrice", c => c.IntroducePrice, 50f, 0, 100000, false),
        ("revealPrice", c => c.RevealPrice, 100f, 0, 100000, false),
        ("revealCount", c => c.RevealCount, 3f, 1, 12, false),
        ("revealMaxDiagonalFraction", c => c.RevealMaxDiagonalFraction, 0.2f, 0, 1, true),
        ("revealMinRelation", c => c.RevealMinRelation, 0f, -100, 100, false),
        ("gainPerMan", c => c.GainPerMan, 0.5f, 0, 10, false),
        ("maxGainPerGift", c => c.MaxGainPerGift, 20f, 0, 100, false),
        ("militiaPerTroop", c => c.MilitiaPerTroop, 1f, 0, 10, false),
        ("hearthPerPrisoner", c => c.HearthPerPrisoner, 1f, 0, 10, false),
        ("prosperityPerPrisoner", c => c.ProsperityPerPrisoner, 1f, 0, 100, false),
        ("rogueryXpPerPrisoner", c => c.RogueryXpPerPrisoner, 10f, 0, 1000, false),
        ("maxMenPerGift", c => c.MaxMenPerGift, 100f, 1, 1000, false),
    };

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_NotableServices_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "notable_services"));
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private NotableServicesConfigProvider Provider(string? moduleData = null)
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(moduleData ?? _tempDir);
        return new NotableServicesConfigProvider(paths, _logger);
    }

    private string JsonPath => Path.Combine(_tempDir, "notable_services", "notable_services.json");

    private NotableServicesConfig LoadWith(string key, string jsonValue)
    {
        File.WriteAllText(JsonPath, "{ \"" + key + "\": " + jsonValue + " }");
        return Provider().GetConfig();
    }

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    [TestMethod]
    public void Defaults_AreTheApprovedValues()
    {
        var config = new NotableServicesConfig();

        foreach (var field in Fields)
            Assert.AreEqual(field.Default, field.Get(config), 0.00001f, field.Key);
    }

    [TestMethod]
    public void EveryDeclaredProperty_HasARuleRow()
    {
        Assert.AreEqual(typeof(NotableServicesConfig).GetProperties().Length, Fields.Length);
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
    public void BelowTheRange_RevertsAndWarns_ForEveryField()
    {
        foreach (var field in Fields)
        {
            _logger.ClearReceivedCalls();
            var below = field.MinExclusive ? field.Min : field.Min - 1;
            var config = LoadWith(field.Key, Num(below));

            Assert.AreEqual(field.Default, field.Get(config), 0.00001f, field.Key + " = " + below);
            _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(field.Key)));
        }
    }

    [TestMethod]
    public void AboveTheRange_RevertsAndWarns_ForEveryField()
    {
        foreach (var field in Fields)
        {
            _logger.ClearReceivedCalls();
            var config = LoadWith(field.Key, Num(field.Max + 1));

            Assert.AreEqual(field.Default, field.Get(config), 0.00001f, field.Key);
            _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(field.Key)));
        }
    }

    [TestMethod]
    public void TheBounds_AreKept_AndTheExcludedLowerBoundJustAboveIsKept()
    {
        foreach (var field in Fields)
        {
            _logger.ClearReceivedCalls();
            var low = field.MinExclusive ? 0.01 : field.Min;
            Assert.AreEqual((float)low, field.Get(LoadWith(field.Key, Num(low))), 0.00001f, field.Key + " low");
            Assert.AreEqual((float)field.Max, field.Get(LoadWith(field.Key, Num(field.Max))), 0.00001f, field.Key + " high");
            _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        }
    }

    [TestMethod]
    public void OneBadValue_LeavesTheOthers_AndWarnsOnceAsSummary()
    {
        File.WriteAllText(JsonPath, "{ \"introducePrice\": -5, \"revealPrice\": 250 }");

        var config = Provider().GetConfig();

        Assert.AreEqual(50f, config.IntroducePrice);
        Assert.AreEqual(250f, config.RevealPrice);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("invalid values")));
    }

    [TestMethod]
    public void MissingFile_UsesDefaults()
    {
        var config = Provider(Path.Combine(_tempDir, "nowhere")).GetConfig();

        Assert.AreEqual(50f, config.IntroducePrice);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void UnparseableFile_LogsErrorAndUsesDefaults()
    {
        File.WriteAllText(JsonPath, "{ not json ");

        var config = Provider().GetConfig();

        Assert.AreEqual(100f, config.MaxMenPerGift);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void JsonNull_UsesDefaults()
    {
        File.WriteAllText(JsonPath, "null");

        Assert.AreEqual(3f, Provider().GetConfig().RevealCount);
    }

    [TestMethod]
    public void GetConfig_IsCachedForTheProcess()
    {
        var provider = Provider();

        Assert.AreSame(provider.GetConfig(), provider.GetConfig());
    }

    [TestMethod]
    public void ShippedFile_LoadsCleanAndEqualsTheDefaults()
    {
        var moduleData = RepoPaths.RepoPath("Main", "_Module", "ModuleData");

        var config = Provider(moduleData).GetConfig();

        foreach (var field in Fields)
            Assert.AreEqual(field.Default, field.Get(config), 0.00001f, field.Key);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedFile_NamesEveryConfigProperty_AndNothingElse()
    {
        var parsed = JsonConvert.DeserializeObject<Dictionary<string, float>>(
            RepoPaths.ReadSource("Main/_Module/ModuleData/notable_services/notable_services.json"));

        Assert.IsNotNull(parsed);
        Assert.AreEqual(Fields.Length, parsed!.Count);
        foreach (var field in Fields)
        {
            Assert.IsTrue(parsed.ContainsKey(field.Key), "Shipped config has no key '" + field.Key + "'.");
            Assert.AreEqual(field.Default, parsed[field.Key], 0.00001f, field.Key);
        }
    }
}
