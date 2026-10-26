using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Encyclopedia;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Encyclopedia;

/// <summary>Pins the SHIPPED hero_filters.json against the provider and against RaceAge's race list (#907).</summary>
[TestClass]
public class ShippedHeroFiltersConfigTests
{
    private static (HeroFilterConfig Config, IModLogger Logger) LoadShipped()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(RepoPaths.RepoPath("Main", "_Module", "ModuleData"));
        var logger = Substitute.For<IModLogger>();
        return (new HeroFilterConfigProvider(pathService, logger).LoadConfig(), logger);
    }

    private static JObject RaceAge() =>
        JObject.Parse(RepoPaths.ReadSource("Main/_Module/ModuleData/raceage/race_age_config.json"));

    [TestMethod]
    public void ShippedFile_LoadsWithNoWarningAndNoError()
    {
        var (config, logger) = LoadShipped();

        Assert.AreEqual(8, config.Groups.Count);
        CollectionAssert.AreEqual(HeroFilterRules.KnownGroupIds.ToList(), config.Groups.Select(g => g.Id).ToList());
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void EveryRaceInRaceAgesConfig_IsInExactlyOneGroup()
    {
        var (config, _) = LoadShipped();

        foreach (var race in ((JObject)RaceAge()["races"]!).Properties().Select(p => p.Name))
            Assert.AreEqual(1, config.Groups.Count(g => g.Races.Contains(race)), race);
    }

    [TestMethod]
    public void EveryGroupedRace_IsOneRaceAgeKnows()
    {
        var (config, _) = LoadShipped();
        var known = ((JObject)RaceAge()["races"]!).Properties().Select(p => p.Name).ToList();

        foreach (var race in config.Groups.SelectMany(g => g.Races))
            CollectionAssert.Contains(known, race);
    }

    [TestMethod]
    public void TheDefaultGroup_ContainsRaceAgesDefaultRace()
    {
        var (config, _) = LoadShipped();
        var defaultRace = (string)RaceAge()["defaultRace"]!;

        var group = config.Groups.Single(g => g.Id == config.DefaultGroup);
        CollectionAssert.Contains(group.Races, defaultRace);
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void EveryRaceTheInstalledGameRegisters_IsInExactlyOneGroup()
    {
        const string modules = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules";
        var skins = new[] { Path.Combine(modules, @"LOTRLOME_Armory\ModuleData\skins.xml"), Path.Combine(modules, @"Native\ModuleData\skins.xml") }
            .Where(File.Exists).ToArray();
        if (skins.Length == 0)
            Assert.Inconclusive("Bannerlord install not found; race registration cannot be checked here.");
        var registered = skins
            .SelectMany(file => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), @"<race\b[^>]*?\bid\s*=\s*""([A-Za-z_0-9]+)""",
                System.Text.RegularExpressions.RegexOptions.Singleline).Cast<System.Text.RegularExpressions.Match>())
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
        var (config, _) = LoadShipped();

        Assert.IsTrue(registered.Contains("human"), "the install's skins.xml must register the engine's human race");
        foreach (var race in registered)
            Assert.AreEqual(1, config.Groups.Count(g => g.Races.Contains(race)), $"race '{race}' must be in exactly one hero_filters.json group");
    }
}
