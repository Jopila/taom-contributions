using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Provender;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Provender;

/// <summary>
/// The rations are user-editable data keyed by ids the game owns, so every id is checked against the live registries at load
/// and an unknown one is dropped with one log line. A bad number drops its ration, never applies as a default
/// (csharp-architecture.md, "Config Providers MUST Validate").
/// </summary>
[TestClass]
public class RationCatalogProviderTests
{
    private const string Good = @"{ ""rations"": [
        { ""id"": ""beer"", ""item"": ""beer"", ""troopsPerItem"": 7, ""morale"": 15, ""default"": true, ""races"": [""dwarf""], ""cultures"": [""vlandia""] },
        { ""id"": ""wine"", ""item"": ""wine"", ""troopsPerItem"": 20, ""morale"": 20, ""races"": [""elf""] } ] }";

    private IProvenderWorldAdapter _world = null!;
    private IRaceManager _races = null!;
    private IModLogger _logger = null!;
    private IPathService _paths = null!;
    private string _dir = null!;
    private RationCatalogProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _world = Substitute.For<IProvenderWorldAdapter>();
        _world.IsRegistryReady.Returns(true);
        foreach (var id in new[] { "beer", "wine", "meat" })
            _world.IsKnownItem(id).Returns(true);
        foreach (var id in new[] { "vlandia", "gondor" })
            _world.IsKnownCulture(id).Returns(true);
        _races = Substitute.For<IRaceManager>();
        foreach (var name in new[] { "human", "dwarf", "elf", "orc", "uruk" })
            _races.IsValidRaceName(name).Returns(true);
        _logger = Substitute.For<IModLogger>();
        _dir = Path.Combine(Path.GetTempPath(), "taom-rations-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_dir, "provender"));
        _paths = Substitute.For<IPathService>();
        _paths.ModuleDataPath.Returns(_dir);
        _sut = new RationCatalogProvider(_paths, _world, _races, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private void WriteFile(string json) => File.WriteAllText(Path.Combine(_dir, "provender", "rations.json"), json);

    private static string Ration(string fields) => "{ \"rations\": [ { " + fields + " } ] }";

    private const string Valid = "\"id\": \"beer\", \"item\": \"beer\", \"troopsPerItem\": 7, \"morale\": 15, \"default\": true";

    private int Warnings => _logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IModLogger.LogWarning));

    [TestMethod]
    public void Load_ValidJson_BuildsTheRationsInFileOrder()
    {
        var catalog = _sut.Load(Good);

        Assert.AreEqual(2, catalog.Count);
        var beer = catalog.Entries[0];
        Assert.AreEqual("beer", beer.Id);
        Assert.AreEqual("beer", beer.ItemId);
        Assert.AreEqual(7, beer.TroopsPerItem);
        Assert.AreEqual(15, beer.Morale);
        Assert.IsTrue(beer.IsDefault);
        CollectionAssert.AreEqual(new[] { "dwarf" }, beer.Races.ToArray());
        CollectionAssert.AreEqual(new[] { "vlandia" }, beer.Cultures.ToArray());
        Assert.IsFalse(catalog.Entries[1].IsDefault);
        Assert.AreEqual(0, Warnings);
        _logger.DidNotReceiveWithAnyArgs().LogError(default!);
    }

    [TestMethod]
    public void Load_UnknownItem_DropsTheRation_WithOneLogLineAndASummary()
    {
        _world.IsKnownItem("mead").Returns(false);

        var catalog = _sut.Load(Ration("\"id\": \"mead\", \"item\": \"mead\", \"troopsPerItem\": 7, \"morale\": 15, \"default\": true"));

        Assert.AreEqual(0, catalog.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("unknown item 'mead'")));
        Assert.AreEqual(2, Warnings, "the drop and the summary");
    }

    [TestMethod]
    public void Load_UnknownRace_DropsThatRace_AndKeepsTheRation()
    {
        var catalog = _sut.Load(Ration(Valid + ", \"races\": [\"dwarf\", \"hobbit\"]"));

        Assert.AreEqual(1, catalog.Count);
        CollectionAssert.AreEqual(new[] { "dwarf" }, catalog.Entries[0].Races.ToArray());
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("unknown race 'hobbit'")));
    }

    [TestMethod]
    public void Load_UnknownCulture_DropsThatCulture_AndKeepsTheRation()
    {
        var catalog = _sut.Load(Ration(Valid + ", \"cultures\": [\"vlandia\", \"atlantis\"]"));

        CollectionAssert.AreEqual(new[] { "vlandia" }, catalog.Entries[0].Cultures.ToArray());
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("unknown culture 'atlantis'")));
    }

    [TestMethod]
    public void Load_ANonDefaultRationWhoseOnlyRaceIsUnknown_IsDropped()
    {
        var catalog = _sut.Load(Ration("\"id\": \"meat\", \"item\": \"meat\", \"troopsPerItem\": 5, \"morale\": 15, \"races\": [\"hobbit\"]"));

        Assert.AreEqual(0, catalog.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("names no known race or culture")));
    }

    [TestMethod]
    public void Load_ADefaultRationNamingNoOne_IsKept()
    {
        var catalog = _sut.Load(Ration(Valid));

        Assert.AreEqual(1, catalog.Count);
        Assert.AreEqual(0, catalog.Entries[0].Races.Count);
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("-3")]
    [DataRow("1001")]
    [DataRow("\"7\"")]
    [DataRow("7.5")]
    [DataRow("null")]
    public void Load_TroopsPerItemOutOfRangeOrNotAnInteger_DropsTheRation(string value)
    {
        var catalog = _sut.Load(Ration($"\"id\": \"beer\", \"item\": \"beer\", \"troopsPerItem\": {value}, \"morale\": 15, \"default\": true"));

        Assert.AreEqual(0, catalog.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("troopsPerItem")));
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("31")]
    [DataRow("\"15\"")]
    [DataRow("15.5")]
    [DataRow("null")]
    public void Load_MoraleOutOfRangeOrNotAnInteger_DropsTheRation(string value)
    {
        var catalog = _sut.Load(Ration($"\"id\": \"beer\", \"item\": \"beer\", \"troopsPerItem\": 7, \"morale\": {value}, \"default\": true"));

        Assert.AreEqual(0, catalog.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("morale")));
    }

    [DataTestMethod]
    [DataRow("1", "1")]
    [DataRow("1000", "30")]
    public void Load_TheRangeEdges_AreAccepted(string troops, string morale)
    {
        var catalog = _sut.Load(Ration($"\"id\": \"beer\", \"item\": \"beer\", \"troopsPerItem\": {troops}, \"morale\": {morale}, \"default\": true"));

        Assert.AreEqual(1, catalog.Count);
    }

    [TestMethod]
    public void Load_ASecondDefault_IsDropped()
    {
        var catalog = _sut.Load(@"{ ""rations"": [
            { ""id"": ""beer"", ""item"": ""beer"", ""troopsPerItem"": 7, ""morale"": 15, ""default"": true },
            { ""id"": ""wine"", ""item"": ""wine"", ""troopsPerItem"": 7, ""morale"": 15, ""default"": true } ] }");

        Assert.AreEqual(1, catalog.Count);
        Assert.AreEqual("beer", catalog.Entries[0].Id);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("second default")));
    }

    [TestMethod]
    public void Load_ADefaultThatIsNotABoolean_DropsTheRation()
    {
        var catalog = _sut.Load(Ration("\"id\": \"beer\", \"item\": \"beer\", \"troopsPerItem\": 7, \"morale\": 15, \"default\": \"yes\""));

        Assert.AreEqual(0, catalog.Count);
    }

    [TestMethod]
    public void Load_ADuplicateId_DropsTheLaterRation()
    {
        var catalog = _sut.Load(@"{ ""rations"": [
            { ""id"": ""beer"", ""item"": ""beer"", ""troopsPerItem"": 7, ""morale"": 15, ""default"": true },
            { ""id"": ""beer"", ""item"": ""wine"", ""troopsPerItem"": 7, ""morale"": 15, ""races"": [""elf""] } ] }");

        Assert.AreEqual(1, catalog.Count);
        Assert.AreEqual("beer", catalog.Entries[0].ItemId);
    }

    [TestMethod]
    public void Load_ABlankIdOrItem_DropsTheRation()
    {
        Assert.AreEqual(0, _sut.Load(Ration("\"id\": \"\", \"item\": \"beer\", \"troopsPerItem\": 7, \"morale\": 15, \"default\": true")).Count);
        Assert.AreEqual(0, _sut.Load(Ration("\"id\": \"beer\", \"item\": \"  \", \"troopsPerItem\": 7, \"morale\": 15, \"default\": true")).Count);
        Assert.AreEqual(0, _sut.Load(Ration("\"item\": \"beer\", \"troopsPerItem\": 7, \"morale\": 15, \"default\": true")).Count);
    }

    [TestMethod]
    public void Load_ARationThatIsNotAnObject_IsDropped_AndTheRestLoad()
    {
        var catalog = _sut.Load(@"{ ""rations"": [ 42, { ""id"": ""beer"", ""item"": ""beer"", ""troopsPerItem"": 7, ""morale"": 15, ""default"": true } ] }");

        Assert.AreEqual(1, catalog.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("is not an object")));
    }

    [TestMethod]
    public void Load_ABlankOrNonTextRace_IsDropped()
    {
        var catalog = _sut.Load(Ration(Valid + ", \"races\": [\"\", 5, \"dwarf\"]"));

        CollectionAssert.AreEqual(new[] { "dwarf" }, catalog.Entries[0].Races.ToArray());
        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("blank or non-text race")));
    }

    [TestMethod]
    public void Load_MalformedJson_LogsAnErrorAndReturnsEmpty()
    {
        var catalog = _sut.Load("{ not json");

        Assert.AreEqual(0, catalog.Count);
        _logger.ReceivedWithAnyArgs().LogError(default!);
    }

    [TestMethod]
    public void Load_NoRationsArray_ReturnsEmptyWithAWarning()
    {
        Assert.AreEqual(0, _sut.Load("{ \"entries\": [] }").Count);
        Assert.AreEqual(0, _sut.Load("[ ]").Count);
        Assert.IsTrue(Warnings >= 2);
    }

    [TestMethod]
    public void Get_RegistriesNotReady_IsEmpty_AndIsNotPinned()
    {
        WriteFile(Good);
        _world.IsRegistryReady.Returns(false);

        Assert.AreEqual(0, _sut.Get().Count);

        _world.IsRegistryReady.Returns(true);
        Assert.AreEqual(2, _sut.Get().Count, "the first call pinned nothing");
    }

    [TestMethod]
    public void Get_MissingFile_IsEmptyWithAWarning()
    {
        Assert.AreEqual(0, _sut.Get().Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void Get_LoadsOnce_AndKeepsTheCatalogForTheProcess()
    {
        WriteFile(Good);
        var first = _sut.Get();
        File.Delete(Path.Combine(_dir, "provender", "rations.json"));

        Assert.AreSame(first, _sut.Get());
    }

    [TestMethod]
    public void Get_AnExceptionWhileLoading_IsLoggedOnce_AndCachedEmpty()
    {
        _world.IsRegistryReady.Returns(_ => throw new InvalidOperationException("boom"));

        Assert.AreEqual(0, _sut.Get().Count);
        Assert.AreEqual(0, _sut.Get().Count);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("boom")));
    }

    [TestMethod]
    public void ARaceListThatIsNotAnArray_IsIgnoredWithAWarning()
    {
        var catalog = _sut.Load(@"{ ""rations"": [ { ""id"": ""beer"", ""item"": ""beer"", ""troopsPerItem"": 7, ""morale"": 15, ""default"": true, ""races"": ""dwarf"" } ] }");

        Assert.AreEqual(1, catalog.Count, "the default ration still loads");
        Assert.AreEqual(0, catalog.Entries[0].Races.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("not an array")));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TheShippedFile_LoadsCleanly_AndNamesNoMead()
    {
        // The races come from the unversioned Armory, so a rename there must fail here, not drop a ration in game: the race
        // check reads the installed skins.xml files (Inconclusive without the install). The items stay faked: beer and wine
        // are vanilla SandBoxCore items and meat is registered in code.
        var json = File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "provender", "rations.json"));
        var live = TAOM.Tests.Core.CultureRaceConsistencyTests.RegisteredRaces();
        _races.IsValidRaceName(Arg.Any<string>()).Returns(call => live.Contains(call.ArgAt<string>(0)));

        var catalog = _sut.Load(json);

        Assert.AreEqual(3, catalog.Count);
        Assert.AreEqual(0, Warnings, "every race the shipped file names is registered by the installed skins.xml files");
        Assert.AreEqual(1, catalog.Entries.Count(e => e.IsDefault));
        Assert.IsFalse(catalog.Entries.Any(e => e.ItemId == "mead"), "there is no mead in the game");
        var meat = catalog.Entries.Single(e => e.Id == "meat");
        Assert.AreEqual(5, meat.TroopsPerItem);
        Assert.AreEqual(15, meat.Morale);
        CollectionAssert.Contains(meat.Races.ToList(), "cave_troll");
        Assert.AreEqual(20, catalog.Entries.Single(e => e.Id == "wine").Morale);
    }
}
