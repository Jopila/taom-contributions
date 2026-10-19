using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.VoiceAccents;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.VoiceAccents;

/// <summary>
/// Pins the voice map shipped to players. The provider is fail-soft, so a bad shipped row would be
/// dropped and the culture would go silent without a visible error; these tests make that loud.
/// </summary>
[TestClass]
public class ShippedVoiceAccentsConfigTests
{
    /// <summary>Culture ids from the SandBox v1.5.5 spcultures.xml that taom_spcultures.xml does not define.</summary>
    private static readonly string[] ExtraCultureIds =
    {
        "nord", "vakken", "darshi", "neutral_culture", "empire", "vlandia", "sturgia", "khuzait", "aserai", "battania",
        "forest_bandits", "sea_raiders", "mountain_bandits", "desert_bandits", "steppe_bandits", "looters",
    };

    private static readonly string[] VanillaCulturesTheBaseModelDoesNotKnow = { "nord", "vakken", "darshi", "neutral_culture" };

    private static readonly string[] EngineTribalCultureIds =
        { "sturgia", "aserai", "khuzait", "battania", "vlandia", "nord", "vakken" };

    private static readonly string[] TribalAccents = { "vlandian", "sturgian", "khuzait", "aserai", "battanian" };

    private static string ModuleDataPath => RepoPath("Main", "_Module", "ModuleData");

    private IModLogger _logger = null!;
    private VoiceAccentMap _map = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _map = new VoiceAccentsConfigProvider(pathService, _logger).GetMap();
    }

    private static string[] DefinedCultureIds() =>
        Regex.Matches(File.ReadAllText(Path.Combine(ModuleDataPath, "taom_spcultures.xml")),
                @"<Culture\s+id=""([^""]+)""")
            .Cast<Match>().Select(m => m.Groups[1].Value).ToArray();

    [TestMethod]
    public void ShippedConfig_ParsesWithoutWarningOrError()
    {
        Assert.IsTrue(File.Exists(Path.Combine(ModuleDataPath, "voice_accents", "voice_accents.json")));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedConfig_EqualsTheBuiltInDefault()
    {
        var builtIn = VoiceAccentMap.Default();

        CollectionAssert.AreEquivalent(builtIn.Cultures.ToList(), _map.Cultures.ToList());
        CollectionAssert.AreEquivalent(builtIn.Races.ToList(), _map.Races.ToList());
    }

    [TestMethod]
    public void ShippedConfig_EveryCultureKeyIsAKnownCultureId()
    {
        var known = DefinedCultureIds().Concat(ExtraCultureIds).ToHashSet();

        var unknown = _map.Cultures.Keys.Where(k => !known.Contains(k)).ToList();

        Assert.AreEqual(0, unknown.Count, "Culture keys that name no culture: " + string.Join(", ", unknown));
    }

    [TestMethod]
    public void ShippedConfig_EveryDefinedCultureHasARow()
    {
        var defined = DefinedCultureIds();
        Assert.IsTrue(defined.Length > 20, "taom_spcultures.xml culture scan found too few ids");

        var missing = defined.Where(id => !_map.Cultures.ContainsKey(id)).ToList();

        Assert.AreEqual(0, missing.Count, "Cultures without a voice row (they would be silent): " + string.Join(", ", missing));
    }

    [TestMethod]
    public void ShippedConfig_EveryRaceKeyIsARaceInTheArmorySkins()
    {
        var skins = File.ReadAllText(RepoPath("docs", "reference", "lotrlome-armory-snapshot", "skins.xml"));
        var races = Regex.Matches(skins, @"<race\s+id\s*=\s*""([^""]+)""")
            .Cast<Match>().Select(m => m.Groups[1].Value).ToHashSet();
        Assert.IsTrue(races.Count > 5, "skins.xml race scan found too few races");

        var unknown = _map.Races.Keys.Where(k => !races.Contains(k)).ToList();

        Assert.AreEqual(0, unknown.Count, "Race keys that name no skins.xml race: " + string.Join(", ", unknown));
    }

    [TestMethod]
    public void ShippedConfig_VanillaCulturesTheBaseModelDoesNotKnowHaveARow()
    {
        var missing = VanillaCulturesTheBaseModelDoesNotKnow.Where(id => !_map.Cultures.ContainsKey(id)).ToList();

        Assert.AreEqual(0, missing.Count, "Rows missing for: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void ShippedConfig_NoNonTribalCultureOrRaceUsesATribalAccent()
    {
        var offenders = _map.Cultures
            .Where(p => !EngineTribalCultureIds.Contains(p.Key) && TribalAccents.Contains(p.Value))
            .Concat(_map.Races.Where(p => TribalAccents.Contains(p.Value)))
            .Select(p => p.Key + "=" + p.Value).ToList();

        Assert.AreEqual(0, offenders.Count,
            "A culture outside the engine's tribal list (ConversationTagHelper.TribalVoiceGroup) selects the high/low "
            + "register text variants, which vanilla recorded almost only in imperial_high and imperial_low, so a tribal "
            + "accent on it is nearly silent. A race row applies in any culture, so it counts too: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void ShippedConfig_NoTribalCultureUsesImperial()
    {
        var offenders = _map.Cultures
            .Where(p => EngineTribalCultureIds.Contains(p.Key) && p.Value == VoiceAccentMap.Imperial)
            .Select(p => p.Key).ToList();

        Assert.AreEqual(0, offenders.Count,
            "A tribal culture (ConversationTagHelper.TribalVoiceGroup) selects the tribal text variants, which vanilla "
            + "did not record in imperial: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void ShippedConfig_NoRowUsesAnAccentVanillaBarelyRecorded()
    {
        var offenders = _map.Cultures.Concat(_map.Races)
            .Where(p => p.Value == "mountain_bandits" || p.Value == "forest_bandits")
            .Select(p => p.Key + "=" + p.Value).ToList();

        Assert.AreEqual(0, offenders.Count,
            "SandBox records 5 voiced lines in mountain_bandits and none in forest_bandits: " + string.Join(", ", offenders));
    }
}
