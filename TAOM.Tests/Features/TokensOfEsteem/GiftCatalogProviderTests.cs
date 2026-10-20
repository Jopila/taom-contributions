using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.TokensOfEsteem;
using TAOM.Features.TokensOfEsteem.Models;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.TokensOfEsteem;

/// <summary>
/// The catalog is user-editable data keyed by ids the game owns, so every id is checked against the live registries at load
/// and an unknown one is dropped with one log line. A bad number drops its entry or taste group, never applies as a default
/// (csharp-architecture.md, "Config Providers MUST Validate").
/// </summary>
[TestClass]
public class GiftCatalogProviderTests
{
    private const string Good = @"{ ""entries"": [
        { ""category"": ""jewelry"", ""lordBase"": 8, ""tastes"": [ { ""races"": [""dwarf""], ""cultures"": [""erebor""], ""bonus"": 4 } ] },
        { ""category"": ""salt"", ""lordBase"": 1, ""tastes"": [] } ] }";

    private IGiftItemAdapter _registry = null!;
    private IRaceManager _races = null!;
    private IModLogger _logger = null!;
    private IPathService _paths = null!;
    private string _dir = null!;
    private GiftCatalogProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _registry = Substitute.For<IGiftItemAdapter>();
        _registry.IsRegistryReady.Returns(true);
        foreach (var id in new[] { "jewelry", "salt", "wine", "horse", "melee_weapons_4" })
            _registry.IsKnownCategory(id).Returns(true);
        foreach (var id in new[] { "erebor", "vlandia", "gondor" })
            _registry.IsKnownCulture(id).Returns(true);
        _races = Substitute.For<IRaceManager>();
        foreach (var name in new[] { "human", "dwarf", "elf", "orc" })
            _races.IsValidRaceName(name).Returns(true);
        _logger = Substitute.For<IModLogger>();
        _dir = Path.Combine(Path.GetTempPath(), "taom-gifts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_dir, "gifts"));
        _paths = Substitute.For<IPathService>();
        _paths.ModuleDataPath.Returns(_dir);
        _sut = new GiftCatalogProvider(_paths, _registry, _races, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private void WriteFile(string json) => File.WriteAllText(Path.Combine(_dir, "gifts", "gift_catalog.json"), json);

    private static string Entry(string category, string lordBase, string tastes = "[]") =>
        $@"{{ ""entries"": [ {{ ""category"": ""{category}"", ""lordBase"": {lordBase}, ""tastes"": {tastes} }} ] }}";

    // Every drop logs its own line naming the id, and any drop adds one summary warning with the count.
    private int Warnings => _logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IModLogger.LogWarning));

    [TestMethod]
    public void Load_ValidJson_BuildsTheEntriesAndTheirTastes()
    {
        var catalog = _sut.Load(Good);

        Assert.AreEqual(2, catalog.Count);
        Assert.IsTrue(catalog.TryGet("jewelry", out var jewelry));
        Assert.AreEqual(8, jewelry.LordBase);
        Assert.AreEqual(1, jewelry.Tastes.Count);
        CollectionAssert.AreEqual(new[] { "dwarf" }, jewelry.Tastes[0].Races.ToArray());
        CollectionAssert.AreEqual(new[] { "erebor" }, jewelry.Tastes[0].Cultures.ToArray());
        Assert.AreEqual(4, jewelry.Tastes[0].Bonus);
        Assert.AreEqual(0, Warnings);
        _logger.DidNotReceiveWithAnyArgs().LogError(default!);
    }

    [TestMethod]
    public void Load_UnknownCategory_IsDroppedWithOneLogLine()
    {
        var catalog = _sut.Load(Entry("mithril", "9"));

        Assert.AreEqual(0, catalog.Count);
        Assert.AreEqual(2, Warnings, "the drop's own line and the summary");
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("mithril")));
    }

    [TestMethod]
    public void Load_UnknownRace_IsDroppedFromItsGroup_WhichKeepsTheRest()
    {
        var catalog = _sut.Load(Entry("jewelry", "8", @"[ { ""races"": [""dwarf"", ""hobbit""], ""cultures"": [], ""bonus"": 3 } ]"));

        Assert.IsTrue(catalog.TryGet("jewelry", out var jewelry));
        CollectionAssert.AreEqual(new[] { "dwarf" }, jewelry.Tastes.Single().Races.ToArray());
        Assert.AreEqual(2, Warnings, "the drop's own line and the summary");
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("hobbit")));
    }

    [TestMethod]
    public void Load_UnknownCulture_IsDroppedFromItsGroup()
    {
        var catalog = _sut.Load(Entry("jewelry", "8", @"[ { ""races"": [""dwarf""], ""cultures"": [""rohan""], ""bonus"": 3 } ]"));

        Assert.IsTrue(catalog.TryGet("jewelry", out var jewelry));
        Assert.AreEqual(0, jewelry.Tastes.Single().Cultures.Count);
        CollectionAssert.AreEqual(new[] { "dwarf" }, jewelry.Tastes.Single().Races.ToArray());
        Assert.AreEqual(2, Warnings, "the drop's own line and the summary");
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("rohan")));
    }

    [TestMethod]
    public void Load_GroupWithNoKnownRaceOrCulture_IsDropped_AndTheEntryStays()
    {
        var catalog = _sut.Load(Entry("jewelry", "8", @"[ { ""races"": [""hobbit""], ""cultures"": [""rohan""], ""bonus"": 3 } ]"));

        Assert.IsTrue(catalog.TryGet("jewelry", out var jewelry));
        Assert.AreEqual(0, jewelry.Tastes.Count);
        Assert.AreEqual(4, Warnings, "a line each for the race, the culture and the group left empty, and the summary");
    }

    [TestMethod]
    public void Load_GroupNamingNothing_IsDropped()
    {
        var catalog = _sut.Load(Entry("jewelry", "8", @"[ { ""races"": [], ""cultures"": [], ""bonus"": 3 } ]"));

        Assert.AreEqual(0, catalog.TryGet("jewelry", out var e) ? e.Tastes.Count : -1);
        Assert.AreEqual(2, Warnings, "the drop's own line and the summary");
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("11")]
    [DataRow("-4")]
    [DataRow("1000000")]
    [DataRow("2147483647")]
    public void Load_LordBaseOutsideOneToTen_DropsTheEntryWithOneLogLine(string lordBase)
    {
        var catalog = _sut.Load(Entry("jewelry", lordBase));

        Assert.AreEqual(0, catalog.Count);
        Assert.AreEqual(2, Warnings, "the drop's own line and the summary");
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("jewelry")));
    }

    [DataTestMethod]
    [DataRow("1")]
    [DataRow("10")]
    public void Load_LordBaseAtTheEdges_IsKept(string lordBase)
    {
        Assert.AreEqual(1, _sut.Load(Entry("jewelry", lordBase)).Count);
        Assert.AreEqual(0, Warnings);
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("11")]
    [DataRow("-2")]
    [DataRow("2147483647")]
    public void Load_BonusOutsideOneToTen_DropsTheGroupWithOneLogLine(string bonus)
    {
        var catalog = _sut.Load(Entry("jewelry", "8", $@"[ {{ ""races"": [""dwarf""], ""cultures"": [], ""bonus"": {bonus} }} ]"));

        Assert.IsTrue(catalog.TryGet("jewelry", out var jewelry));
        Assert.AreEqual(0, jewelry.Tastes.Count);
        Assert.AreEqual(2, Warnings, "the drop's own line and the summary");
    }

    [TestMethod]
    public void Load_LordBaseThatIsAString_DropsTheEntryRatherThanApplyingADefault()
    {
        var catalog = _sut.Load(Entry("jewelry", "\"eight\""));

        Assert.AreEqual(0, catalog.Count);
        Assert.AreEqual(2, Warnings, "the drop's own line and the summary");
    }

    [TestMethod]
    public void Load_FractionalLordBase_IsNotAcceptedAsAnInteger()
    {
        var catalog = _sut.Load(Entry("jewelry", "8.5"));

        Assert.AreEqual(0, catalog.Count);
        Assert.AreEqual(2, Warnings, "the drop's own line and the summary");
    }

    [TestMethod]
    public void Load_DuplicateCategory_KeepsTheFirstAndLogsTheSecond()
    {
        var json = @"{ ""entries"": [
            { ""category"": ""jewelry"", ""lordBase"": 8, ""tastes"": [] },
            { ""category"": ""jewelry"", ""lordBase"": 2, ""tastes"": [] } ] }";

        var catalog = _sut.Load(json);

        Assert.AreEqual(1, catalog.Count);
        Assert.IsTrue(catalog.TryGet("jewelry", out var entry));
        Assert.AreEqual(8, entry.LordBase);
        Assert.AreEqual(2, Warnings, "the drop's own line and the summary");
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Load_BlankCategory_IsDroppedWithOneLogLine(string category)
    {
        Assert.AreEqual(0, _sut.Load(Entry(category, "5")).Count);
        Assert.AreEqual(2, Warnings, "the drop's own line and the summary");
    }

    [TestMethod]
    public void Load_AnyDrop_EndsWithOneSummaryNamingTheCount()
    {
        var json = @"{ ""entries"": [
            { ""category"": ""mithril"", ""lordBase"": 8, ""tastes"": [] },
            { ""category"": ""jewelry"", ""lordBase"": 99, ""tastes"": [] },
            { ""category"": ""salt"", ""lordBase"": 2, ""tastes"": [ { ""races"": [""dwarf"", ""hobbit""], ""cultures"": [], ""bonus"": 3 } ] } ] }";

        var catalog = _sut.Load(json);

        Assert.AreEqual(1, catalog.Count);
        Assert.AreEqual(4, Warnings, "three drops, each with its own line, and one summary");
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("3 dropped")));
    }

    [TestMethod]
    public void Load_NothingDropped_LogsNoWarningAtAll()
    {
        _sut.Load(Good);

        Assert.AreEqual(0, Warnings);
    }

    [TestMethod]
    public void Load_MissingTastesAndLists_AreTreatedAsEmpty()
    {
        var json = @"{ ""entries"": [ { ""category"": ""salt"", ""lordBase"": 2 },
            { ""category"": ""jewelry"", ""lordBase"": 3, ""tastes"": [ { ""bonus"": 2 } ] } ] }";

        var catalog = _sut.Load(json);

        Assert.AreEqual(2, catalog.Count);
        Assert.IsTrue(catalog.TryGet("salt", out var salt));
        Assert.AreEqual(0, salt.Tastes.Count);
        Assert.IsTrue(catalog.TryGet("jewelry", out var jewelry));
        Assert.AreEqual(0, jewelry.Tastes.Count);
    }

    [TestMethod]
    public void Load_NullEntriesInTheLists_AreTolerated()
    {
        var json = @"{ ""entries"": [ null, { ""category"": ""salt"", ""lordBase"": 2, ""tastes"": [ null, { ""races"": [null, ""dwarf""], ""cultures"": null, ""bonus"": 2 } ] } ] }";

        var catalog = _sut.Load(json);

        Assert.AreEqual(1, catalog.Count);
        Assert.IsTrue(catalog.TryGet("salt", out var salt));
        Assert.AreEqual(1, salt.Tastes.Count);
        CollectionAssert.AreEqual(new[] { "dwarf" }, salt.Tastes[0].Races.ToArray());
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("not json at all")]
    [DataRow("{ \"entries\": ")]
    [DataRow("[]")]
    [DataRow("null")]
    [DataRow("{}")]
    public void Load_UnusableFile_IsAnEmptyCatalog(string json)
    {
        var catalog = _sut.Load(json);

        Assert.AreEqual(0, catalog.Count);
    }

    [TestMethod]
    public void Load_MalformedJson_LogsAnError()
    {
        _sut.Load("not json at all");

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
    }

    [TestMethod]
    public void Get_FileMissing_IsEmptyAndLogsOnce()
    {
        var first = _sut.Get();
        var second = _sut.Get();

        Assert.AreEqual(0, first.Count);
        Assert.AreSame(first, second);
        Assert.AreEqual(1, Warnings);
    }

    [TestMethod]
    public void Get_RegistryNotReady_IsEmptyAndTriesAgainLater()
    {
        WriteFile(Good);
        _registry.IsRegistryReady.Returns(false);

        Assert.AreEqual(0, _sut.Get().Count);

        _registry.IsRegistryReady.Returns(true);

        Assert.AreEqual(2, _sut.Get().Count);
    }

    [TestMethod]
    public void Get_LoadedOnce_IsKeptForTheProcess()
    {
        WriteFile(Good);

        var first = _sut.Get();
        File.Delete(Path.Combine(_dir, "gifts", "gift_catalog.json"));
        var second = _sut.Get();

        Assert.AreSame(first, second);
        Assert.AreEqual(2, second.Count);
    }

    [TestMethod]
    public void Get_RegistryThrows_IsEmptyAndLogsAnError()
    {
        WriteFile(Good);
        _registry.IsKnownCategory(Arg.Any<string>()).Returns(_ => throw new InvalidOperationException("registry fault"));

        var catalog = _sut.Get();

        Assert.AreEqual(0, catalog.Count);
        _logger.ReceivedWithAnyArgs().LogError(default!);
    }

    // The shipped file, against the ids the game really has. The category ids are the 65 Create(...) ids of v1.5.5's
    // DefaultItemCategories; the culture ids are the playable cultures read from the data (CultureDataFixture); the race names
    // come from the installed skins.xml files when the game is installed (LiveInstall test) and from the frozen list below
    // otherwise. A renamed or mistyped id in the shipped catalog fails here instead of being dropped silently in game.
    [TestMethod]
    public void ShippedCatalog_AgainstTheFrozenRaceList_LoadsWithNothingDropped_AndSeedsTheDocumentedTastes()
    {
        AssertShippedCatalog(EngineRaces);
    }

    [TestCategory("LiveInstall")]
    [TestMethod]
    public void ShippedCatalog_AgainstTheInstalledRaces_LoadsWithNothingDropped_AndSeedsTheDocumentedTastes()
    {
        const string modules = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules";
        var skins = new[] { Path.Combine(modules, @"LOTRLOME_Armory\ModuleData\skins.xml"), Path.Combine(modules, @"Native\ModuleData\skins.xml") }
            .Where(File.Exists).ToArray();
        if (skins.Length == 0)
            Assert.Inconclusive("Bannerlord install not found; race registration cannot be checked here.");
        var registered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in skins)
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"<race\b[^>]*?\bid\s*=\s*""([A-Za-z_0-9]+)""", RegexOptions.Singleline))
                registered.Add(m.Groups[1].Value);

        AssertShippedCatalog(registered);
    }

    private void AssertShippedCatalog(IEnumerable<string> raceNames)
    {
        var shipped = File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "gifts", "gift_catalog.json"));
        var categories = new HashSet<string>(EngineCategories);
        var cultures = TAOM.Tests.Core.CultureDataFixture.MainCultureIds();
        var races = new HashSet<string>(raceNames);
        var registry = Substitute.For<IGiftItemAdapter>();
        registry.IsRegistryReady.Returns(true);
        registry.IsKnownCategory(Arg.Any<string>()).Returns(call => categories.Contains(call.ArgAt<string>(0)));
        registry.IsKnownCulture(Arg.Any<string>()).Returns(call => cultures.Contains(call.ArgAt<string>(0)));
        var races2 = Substitute.For<IRaceManager>();
        races2.IsValidRaceName(Arg.Any<string>()).Returns(call => races.Contains(call.ArgAt<string>(0)));
        var logger = Substitute.For<IModLogger>();

        var catalog = new GiftCatalogProvider(_paths, registry, races2, logger).Load(shipped);

        logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
        logger.DidNotReceiveWithAnyArgs().LogError(default!);
        Assert.IsTrue(catalog.Count >= 10, "the seeded catalog lists at least ten categories");
        foreach (var id in catalog.CategoryIds)
        {
            Assert.IsTrue(catalog.TryGet(id, out var entry));
            Assert.IsTrue(entry.LordBase >= 1 && entry.LordBase <= 10, id);
            Assert.IsTrue(entry.Tastes.All(t => t.Bonus >= 1 && t.Bonus <= 10), id);
        }

        Assert.IsTrue(Prized(catalog, "jewelry", "dwarf"), "dwarves prize jewelry");
        Assert.IsTrue(Prized(catalog, "silver", "dwarf"), "dwarves prize silver");
        Assert.IsTrue(Prized(catalog, "wine", "elf"), "elves prize wine");
        Assert.IsTrue(Prized(catalog, "velvet", "elf"), "elves prize velvet");
        Assert.IsTrue(Prized(catalog, "linen", "elf"), "elves prize linen");
        Assert.IsTrue(Prized(catalog, "sumpter_horse", culture: "vlandia"), "the Rohirrim (culture vlandia) prize horses");
        Assert.IsTrue(Prized(catalog, "horse", culture: "vlandia"));
        Assert.IsTrue(Prized(catalog, "noble_horse", culture: "vlandia"));
        // All 22 Armory creature mounts (wargs, spiders, elk and the rest) are item category war_horse, so a Rohirric taste for
        // war_horse would make a warg a horse-lord's treasure.
        Assert.IsFalse(Prized(catalog, "war_horse", culture: "vlandia"), "a creature mount is not a Rohirric prize");
        Assert.IsFalse(Prized(catalog, "war_horse", "human", "vlandia"));
        foreach (var orc in new[] { "orc", "uruk", "uruk_hai", "pale_uruk", "dg_uruk", "goblin", "berserker" })
        {
            Assert.IsTrue(Prized(catalog, "meat", orc), orc + " chiefs prize meat");
            foreach (var weapons in new[] { "melee_weapons_3", "melee_weapons_4", "melee_weapons_5" })
                Assert.IsTrue(Prized(catalog, weapons, orc), orc + " chiefs prize " + weapons);
        }
        Assert.IsFalse(Prized(catalog, "meat", "human", "gondor"), "meat is no taste of men");
    }

    private static bool Prized(GiftCatalog catalog, string category, string race = "", string culture = "") =>
        catalog.TryGet(category, out var entry) && entry.TasteBonus(race, culture) > 0;

    private static readonly string[] EngineRaces =
    {
        "human", "dwarf", "uruk", "nazghul", "orc", "uruk_hai", "berserker", "cave_troll", "hill_troll", "pale_uruk", "dg_uruk",
        "goblin", "elf", "saruman", "sauron",
    };

    private static readonly string[] EngineCategories =
    {
        "grain", "fish", "meat", "cheese", "grape", "date_fruit", "olives", "butter", "oil", "flax", "linen", "wool", "cloth",
        "cotton", "velvet", "hardwood", "iron", "salt", "silver", "hides", "clay", "beer", "wine", "tools", "pottery", "leather",
        "fur", "jewelry", "arrows", "sheep", "cow", "hog", "sumpter_horse", "horse", "war_horse", "noble_horse", "horse_equipment",
        "horse_equipment_2", "horse_equipment_3", "horse_equipment_4", "horse_equipment_5", "melee_weapons", "melee_weapons_2",
        "melee_weapons_3", "melee_weapons_4", "melee_weapons_5", "ranged_weapons", "ranged_weapons_2", "ranged_weapons_3",
        "ranged_weapons_4", "ranged_weapons_5", "shield", "shield_2", "shield_3", "shield_4", "shield_5", "garment", "light_armor",
        "medium_armor", "heavy_armor", "ultra_armor", "banner", "unassigned", "felt", "planks",
    };
}
