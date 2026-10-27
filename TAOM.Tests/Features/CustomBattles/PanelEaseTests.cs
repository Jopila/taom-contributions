using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// #1027: the panel slide-ins ease like vanilla's. TAOM's custom battle screen copies SandBoxCore's, whose three
/// panel definitions use <c>EaseType="EaseOut" EaseFunction="Quint"</c>; TAOM's copy carried the dead
/// <c>EaseIn</c> (removed in #1024), so its panels slid at constant speed.
/// </summary>
[TestClass]
public class PanelEaseTests
{
    private static XElement[] Definitions(string path) =>
        XDocument.Load(path).Descendants("VisualDefinition").ToArray();

    private static string[] Attributes(XElement element) =>
        element.Attributes().Select(a => a.Name.LocalName + "=" + a.Value).OrderBy(s => s, StringComparer.Ordinal).ToArray();

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void CustomBattleScreen_PanelDefinitions_MatchVanillaSandBoxCore()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        var vanillaPath = Path.Combine(game, "Modules", "SandBoxCore", "GUI", "Prefabs", "CustomBattle", "CustomBattleScreen.xml");
        if (!File.Exists(vanillaPath))
            Assert.Inconclusive($"No vanilla custom battle screen at {vanillaPath}; the install is missing.");

        var vanilla = Definitions(vanillaPath).ToDictionary(d => (string)d.Attribute("Name")!);
        var taom = Definitions(RepoPath("Main", "_Module", "GUI", "PreFabs", "CustomBattle", "CustomBattleScreen.xml"));

        CollectionAssert.AreEquivalent(vanilla.Keys.ToArray(), taom.Select(d => (string)d.Attribute("Name")!).ToArray(),
            "TAOM's custom battle screen should define the same panels as vanilla's");
        foreach (var definition in taom)
        {
            var name = (string)definition.Attribute("Name")!;
            var reference = vanilla[name];
            CollectionAssert.AreEqual(Attributes(reference), Attributes(definition),
                $"{name}: attributes differ from vanilla SandBoxCore CustomBattleScreen.xml");

            var vanillaStates = reference.Elements("VisualState").ToDictionary(s => (string?)s.Attribute("State") ?? "");
            var taomStates = definition.Elements("VisualState").ToList();
            Assert.AreEqual(vanillaStates.Count, taomStates.Count, $"{name}: VisualState count differs from vanilla");
            foreach (var state in taomStates)
            {
                var stateName = (string?)state.Attribute("State") ?? "";
                Assert.IsTrue(vanillaStates.ContainsKey(stateName), $"{name}: vanilla has no VisualState '{stateName}'");
                CollectionAssert.AreEqual(Attributes(vanillaStates[stateName]), Attributes(state),
                    $"{name}/{stateName}: VisualState attributes differ from vanilla");
            }
        }
    }
}
