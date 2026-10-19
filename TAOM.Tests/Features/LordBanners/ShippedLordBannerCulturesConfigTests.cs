using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LordBanners;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.LordBanners;

/// <summary>
/// Pins the banner map shipped to players. The provider is fail-soft, so a bad shipped row would be dropped and
/// the culture would lose its banners without a visible error; these tests make that loud.
/// </summary>
[TestClass]
public class ShippedLordBannerCulturesConfigTests
{
    /// <summary>Culture ids from the SandBox v1.5.5 spcultures.xml that taom_spcultures.xml does not define.</summary>
    private static readonly string[] ExtraCultureIds =
    {
        "nord", "vakken", "darshi", "neutral_culture", "empire", "vlandia", "sturgia", "khuzait", "aserai", "battania",
        "forest_bandits", "sea_raiders", "mountain_bandits", "desert_bandits", "steppe_bandits", "looters",
    };

    private IModLogger _logger = null!;
    private LordBannerMap _map = null!;

    private static string ModuleDataPath => RepoPath("Main", "_Module", "ModuleData");

    private static string GameDir()
    {
        var env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env!;
    }

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _map = new LordBannerCulturesConfigProvider(pathService, _logger).GetMap();
    }

    private static string[] DefinedCultureIds() =>
        Regex.Matches(File.ReadAllText(Path.Combine(ModuleDataPath, "taom_spcultures.xml")), @"<Culture\s+id=""([^""]+)""")
            .Cast<Match>().Select(m => m.Groups[1].Value).ToArray();

    [TestMethod]
    public void ShippedConfig_ParsesWithoutWarningOrError()
    {
        Assert.IsTrue(File.Exists(Path.Combine(ModuleDataPath, "lord_banners", "lord_banner_cultures.json")));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedConfig_EqualsTheBuiltInDefault()
    {
        var builtIn = LordBannerMap.Default();

        Assert.AreEqual(builtIn.Cultures.Count, _map.Cultures.Count);
        foreach (var pair in builtIn.Cultures)
            CollectionAssert.AreEqual(pair.Value.ToList(), _map.Cultures[pair.Key].ToList(), pair.Key);
    }

    [TestMethod]
    public void ShippedConfig_EveryKeyIsAKnownCultureId()
    {
        var known = DefinedCultureIds().Concat(ExtraCultureIds).ToHashSet();

        var unknown = _map.Cultures.Keys.Where(k => !known.Contains(k)).ToList();

        Assert.AreEqual(0, unknown.Count, "Keys that name no culture: " + string.Join(", ", unknown));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void ShippedConfig_EveryLordCultureHasABannerPoolAtLevelsOneToThree()
    {
        var banners = Path.Combine(GameDir(), "Modules", "SandBoxCore", "ModuleData", "items", "banners.xml");
        if (!File.Exists(banners))
            Assert.Inconclusive("Game install not found at " + banners);

        var xml = File.ReadAllText(banners);
        var items = Regex.Matches(xml, @"<Item\b[^>]*?culture=""Culture\.([^""]+)""[\s\S]*?banner_level=""(\d)""")
            .Cast<Match>().Select(m => (Culture: (string?)m.Groups[1].Value, Level: int.Parse(m.Groups[2].Value))).ToArray();
        Assert.AreEqual(45, items.Length, "banners.xml scan found a different item count");

        var lordCultures = Directory.GetFiles(Path.Combine(ModuleDataPath, "characters"), "*.xml")
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"<NPCCharacter\b[^>]*>").Cast<Match>())
            .Select(m => m.Value)
            .Where(tag => tag.Contains("occupation=\"Lord\""))
            .Select(tag => Regex.Match(tag, @"culture=""Culture\.([^""]+)""")).Where(m => m.Success)
            .Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.IsTrue(lordCultures.Count > 5, "lord culture scan found too few cultures");

        var service = new LordBannerService(new FixedProvider(_map));
        var empty = (from culture in lordCultures from level in new[] { 1, 2, 3 }
                     where service.Allowed(culture, level, items, i => (i.Culture, i.Level)).Count == 0
                     select culture + " L" + level).ToList();

        Assert.AreEqual(0, empty.Count, "Lord cultures with no banner at a level: " + string.Join(", ", empty));
    }

    private sealed class FixedProvider : ILordBannerCulturesConfigProvider
    {
        private readonly LordBannerMap _map;

        public FixedProvider(LordBannerMap map) => _map = map;

        public LordBannerMap GetMap() => _map;
    }
}
