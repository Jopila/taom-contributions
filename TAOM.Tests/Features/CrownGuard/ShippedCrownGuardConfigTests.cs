using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CrownGuard;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CrownGuard;

/// <summary>
/// Pins the guard map shipped to players. The provider is fail-soft, so a bad shipped row would be dropped and the
/// culture would fall back to vanilla's guard without a visible error; these tests make that loud.
/// </summary>
[TestClass]
public class ShippedCrownGuardConfigTests
{
    private const int MinimumKingdomCultures = 20;
    private const int MinimumTroops = 1000;

    private static Dictionary<string, string?> _troopRaces = null!;
    private static HashSet<string> _kingdomCultures = null!;

    private IModLogger _logger = null!;
    private CrownGuardMap _map = null!;

    private static string ModuleDataPath => RepoPath("Main", "_Module", "ModuleData");

    [ClassInitialize]
    public static void LoadShippedData(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _)
    {
        _troopRaces = new Dictionary<string, string?>();
        foreach (var file in Directory.EnumerateFiles(ModuleDataPath, "*.xml", SearchOption.AllDirectories))
        {
            if (file.Split(Path.DirectorySeparatorChar).Contains("Languages"))
                continue;
            foreach (var npc in XDocument.Load(file).Descendants("NPCCharacter"))
            {
                var id = (string?)npc.Attribute("id");
                if (!string.IsNullOrEmpty(id) && !_troopRaces.ContainsKey(id!))
                    _troopRaces[id!] = (string?)npc.Attribute("race");
            }
        }

        _kingdomCultures = new HashSet<string>();
        foreach (var file in new[] { "taom_spkingdoms.xml", "spkingdoms.xslt" })
        {
            foreach (var kingdom in XDocument.Load(Path.Combine(ModuleDataPath, file)).Descendants().Where(e => e.Name.LocalName == "Kingdom"))
            {
                var culture = (string?)kingdom.Attribute("culture");
                if (culture != null)
                    _kingdomCultures.Add(culture.Replace("Culture.", ""));
            }
        }
    }

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _map = new CrownGuardConfigProvider(pathService, _logger).GetMap();
    }

    [TestMethod]
    public void ShippedConfig_ParsesWithoutWarningOrError()
    {
        Assert.IsTrue(File.Exists(Path.Combine(ModuleDataPath, "crown_guard", "crown_guard_cultures.json")));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedConfig_EqualsTheBuiltInDefault()
    {
        var builtIn = CrownGuardMap.Default();

        Assert.AreEqual(builtIn.Cultures.Count, _map.Cultures.Count);
        foreach (var pair in builtIn.Cultures)
            Assert.AreEqual(pair.Value, _map.Cultures[pair.Key], pair.Key);
    }

    [TestMethod]
    public void ShippedConfig_KeysEqualTheKingdomCultures()
    {
        Assert.IsTrue(_kingdomCultures.Count >= MinimumKingdomCultures, "kingdom culture scan found too few: " + _kingdomCultures.Count);

        var missing = _kingdomCultures.Where(c => !_map.Cultures.ContainsKey(c)).OrderBy(c => c).ToList();
        var stale = _map.Cultures.Keys.Where(k => !_kingdomCultures.Contains(k)).OrderBy(k => k).ToList();

        Assert.AreEqual(0, missing.Count, "Kingdom cultures with no guard row: " + string.Join(", ", missing));
        Assert.AreEqual(0, stale.Count, "Guard rows for a culture no kingdom uses: " + string.Join(", ", stale));
    }

    [TestMethod]
    public void ShippedConfig_EveryTroopIdIsANpcCharacterInTheModuleData()
    {
        Assert.IsTrue(_troopRaces.Count >= MinimumTroops, "NPCCharacter scan found too few: " + _troopRaces.Count);

        var missing = _map.Cultures.Where(p => !_troopRaces.ContainsKey(p.Value)).Select(p => p.Key + " -> " + p.Value).ToList();

        Assert.AreEqual(0, missing.Count, "Rows naming a troop that does not exist: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void ShippedConfig_NoGuardIsATroll()
    {
        Assert.IsTrue(_map.Cultures.Count >= MinimumKingdomCultures, "the shipped map is too small to check");

        var trolls = _map.Cultures.Where(p => _troopRaces.TryGetValue(p.Value, out var race) && (race == "cave_troll" || race == "hill_troll"))
            .Select(p => p.Key + " -> " + p.Value).ToList();

        Assert.AreEqual(0, trolls.Count, "Guards that are trolls: " + string.Join(", ", trolls));
    }
}
