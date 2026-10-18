using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.BattleScenes;

/// <summary>
/// The settlement scenes wired by hand in the live, unversioned TAOM_Map settlements.xml (Kysaro's drop, lotraom-assets
/// db387f23f, 2026-10-17), and every taom_* scene that file names. A TAOM_Map reinstall or an artist copy-port of the
/// file puts the old scenes back, and audit_scene_names.py marks the old scenes [OK] because they still exist, so only
/// this read catches the revert. Inconclusive on a machine without the module.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class SettlementSceneLiveDataTests
{
    // (settlement id, Location id, the scene every scene_name* attribute on that Location must name)
    private static readonly (string Settlement, string Location, string Scene)[] Wired =
    {
        ("town_EW4", "center", "taom_gondor_town_pelargir_forceatmo"),
        ("town_EW10", "center", "taom_gondor_town_serelond_forceatmo"),
        ("town_EW10", "arena", "taom_gondor_arena_serelond_forceatmo"),
        ("castle_village_EW7_4", "village_center", "taom_gondor_village_amongelin_forceatmo"),
        ("village_EW9_2", "village_center", "taom_gondor_village_lothgobel_forceatmo"),
        ("town_MM1", "center", "taom_dwarven_moria_e_forceatmo"),
        // The five Mordor towns other than Minas Morgul (town_ES2), which keeps its own scene (Mike, 2026-10-17).
        ("town_ES1", "center", "taom_mordor_town_01_forceatmo"),
        ("town_ES3", "center", "taom_mordor_town_01_forceatmo"),
        ("town_ES4", "center", "taom_mordor_town_01_forceatmo"),
        ("town_ES5", "center", "taom_mordor_town_01_forceatmo"),
        ("town_ES6", "center", "taom_mordor_town_01_forceatmo"),
    };

    private static string ModuleDir()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        string module = Path.Combine(game, "Modules", "TAOM_Map");
        if (!File.Exists(Path.Combine(module, "ModuleData", "settlements.xml")))
            Assert.Inconclusive("TAOM_Map not installed on this machine; the live wiring cannot be checked here.");
        return module;
    }

    private static XElement LoadSettlements(string module) =>
        XDocument.Load(Path.Combine(module, "ModuleData", "settlements.xml")).Root!;

    private static IEnumerable<XAttribute> SceneNames(XElement location) =>
        location.Attributes().Where(a => a.Name.LocalName.StartsWith("scene_name", StringComparison.Ordinal));

    [TestMethod]
    public void WiredSettlements_NameTheirSceneOnEveryLevel()
    {
        var settlements = LoadSettlements(ModuleDir()).Elements("Settlement").ToList();
        var failures = new List<string>();
        foreach (var (settlement, location, scene) in Wired)
        {
            var s = settlements.Where(e => (string?)e.Attribute("id") == settlement).ToList();
            if (s.Count != 1) { failures.Add($"{settlement}: {s.Count} settlements with that id"); continue; }
            var locs = s[0].Descendants("Location").Where(l => (string?)l.Attribute("id") == location).ToList();
            if (locs.Count != 1) { failures.Add($"{settlement}/{location}: {locs.Count} Locations"); continue; }
            var names = SceneNames(locs[0]).Select(a => a.Value).ToList();
            if (names.Count == 0 || names.Any(n => n != scene))
                failures.Add($"{settlement}/{location} names [{string.Join(", ", names)}], expected {scene}");
        }
        Assert.AreEqual(0, failures.Count, "the live wiring was reverted:\n" + string.Join("\n", failures));
    }

    [TestMethod]
    public void EveryTaomSceneTheMapNames_ExistsInTaomMap()
    {
        string module = ModuleDir();
        var missing = LoadSettlements(module).Descendants("Location").SelectMany(SceneNames)
            .Select(a => a.Value).Where(v => v.StartsWith("taom_", StringComparison.Ordinal)).Distinct()
            .Where(s => !File.Exists(Path.Combine(module, "SceneObj", s, "scene.xscene"))).ToList();
        Assert.AreEqual(0, missing.Count,
            $"TAOM_Map/SceneObj lacks {string.Join(", ", missing)}: those settlements would load no scene");
    }
}
