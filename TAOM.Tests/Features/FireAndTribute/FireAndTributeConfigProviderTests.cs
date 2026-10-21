using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Execution;
using TAOM.Features.FireAndTribute;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FireAndTribute;

[TestClass]
public class FireAndTributeConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IModLogger _logger = null!;
    private FireAndTributeConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_FireAndTribute_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "fire_and_tribute");
        Directory.CreateDirectory(_featureDir);

        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new FireAndTributeConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private const string GoodHost = @"{ ""id"": ""gundabad"", ""side"": ""Evil"", ""clan"": ""gundabad_raiders"",
        ""name"": ""{=taom_fat_host_gundabad}The Host of Gundabad"", ""anchors"": [""town_G1""], ""targetSides"": [""Free""],
        ""preferredTargets"": [""erebor""], ""troops"": [ { ""id"": ""gundabad_snaga"", ""share"": 0.5 }, { ""id"": ""gundabad_grunt"", ""share"": 0.5 } ] }";

    private void Write(string json) => File.WriteAllText(Path.Combine(_featureDir, "fire_and_tribute.json"), json);

    private void WriteTuning(string tuningBody) => Write("{ \"tuning\": { " + tuningBody + " }, \"hosts\": [ " + GoodHost + " ] }");

    private void WriteHost(string hostJson) => Write("{ \"hosts\": [ " + hostJson + " ] }");

    private void AssertWarned(string text) =>
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains(text)));

    // ---- file handling ---------------------------------------------------------------------------------------------

    [TestMethod]
    public void GetConfig_MissingFile_ReturnsDefaultTuningAndNoHostsAndWarns()
    {
        var config = _sut.GetConfig();

        Assert.AreEqual(45, config.Tuning.FirstHostDelayDays);
        Assert.AreEqual(0, config.Hosts.Count);
        AssertWarned("not found");
    }

    [TestMethod]
    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
    {
        Write("{ \"tuning\": ");

        var config = _sut.GetConfig();

        Assert.AreEqual(120, config.Tuning.CooldownDays);
        Assert.AreEqual(0, config.Hosts.Count);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_WrongTypeForAnInt_ReturnsDefaultsAndLogsError()
    {
        Write("{ \"tuning\": { \"minTroops\": \"many\" } }");

        var config = _sut.GetConfig();

        Assert.AreEqual(80, config.Tuning.MinTroops);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetConfig_EmptyObject_KeepsTheTuningAndWarnsThatNoHostIsValid()
    {
        Write("{}");

        var config = _sut.GetConfig();

        Assert.AreEqual(0.05f, config.Tuning.SpawnChance);
        Assert.AreEqual(0, config.Hosts.Count);
        AssertWarned("no valid host");
    }

    [TestMethod]
    public void GetConfig_ValidTuningAndHost_ParsesEveryField()
    {
        Write(@"{ ""tuning"": { ""firstHostDelayDays"": 10, ""spawnChance"": 0.25, ""cooldownDays"": 30, ""warlessStartDay"": 20,
            ""minTroops"": 10, ""maxTroops"": 90, ""strengthShare"": 0.5, ""strengthPerRaid"": 100, ""quotaMin"": 2, ""quotaMax"": 9,
            ""quotaExtraMin"": 1, ""quotaExtraMax"": 4, ""minHearth"": 25, ""nearestVillages"": 5, ""engageRadius"": 12,
            ""engageStrengthRatio"": 1.5, ""maxHostDays"": 60, ""homeRadius"": 6, ""spawnRadiusDays"": 0.5 },
            ""hosts"": [ " + GoodHost + " ] }");

        var c = _sut.GetConfig();
        var t = c.Tuning;

        Assert.AreEqual(10, t.FirstHostDelayDays);
        Assert.AreEqual(0.25f, t.SpawnChance);
        Assert.AreEqual(30, t.CooldownDays);
        Assert.AreEqual(20, t.WarlessStartDay);
        Assert.AreEqual(10, t.MinTroops);
        Assert.AreEqual(90, t.MaxTroops);
        Assert.AreEqual(0.5f, t.StrengthShare);
        Assert.AreEqual(100f, t.StrengthPerRaid);
        Assert.AreEqual(2, t.QuotaMin);
        Assert.AreEqual(9, t.QuotaMax);
        Assert.AreEqual(1, t.QuotaExtraMin);
        Assert.AreEqual(4, t.QuotaExtraMax);
        Assert.AreEqual(25f, t.MinHearth);
        Assert.AreEqual(5, t.NearestVillages);
        Assert.AreEqual(12f, t.EngageRadius);
        Assert.AreEqual(1.5f, t.EngageStrengthRatio);
        Assert.AreEqual(60, t.MaxHostDays);
        Assert.AreEqual(6f, t.HomeRadius);
        Assert.AreEqual(0.5f, t.SpawnRadiusDays);
        var h = c.Hosts.Single();
        Assert.AreEqual("gundabad", h.Id);
        Assert.AreEqual(FactionSide.Evil, h.Side);
        Assert.AreEqual("gundabad_raiders", h.ClanId);
        Assert.AreEqual("{=taom_fat_host_gundabad}The Host of Gundabad", h.Name);
        CollectionAssert.AreEqual(new[] { "town_G1" }, h.Anchors.ToArray());
        CollectionAssert.AreEqual(new[] { FactionSide.Free }, h.TargetSides.ToArray());
        CollectionAssert.AreEqual(new[] { "erebor" }, h.PreferredTargets.ToArray());
        Assert.AreEqual(2, h.Troops.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // ---- tuning: one test per rule ---------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow("spawnChance", "NaN")]
    [DataRow("spawnChance", "Infinity")]
    [DataRow("spawnChance", "-0.1")]
    [DataRow("spawnChance", "1.5")]
    [DataRow("strengthShare", "NaN")]
    [DataRow("strengthShare", "0")]
    [DataRow("strengthShare", "-0.5")]
    [DataRow("strengthShare", "1.5")]
    [DataRow("strengthPerRaid", "NaN")]
    [DataRow("strengthPerRaid", "0")]
    [DataRow("strengthPerRaid", "-5")]
    [DataRow("strengthPerRaid", "1e30")]
    [DataRow("minHearth", "NaN")]
    [DataRow("minHearth", "-1")]
    [DataRow("minHearth", "100000")]
    [DataRow("engageRadius", "NaN")]
    [DataRow("engageRadius", "0")]
    [DataRow("engageRadius", "-3")]
    [DataRow("engageRadius", "500")]
    [DataRow("engageStrengthRatio", "NaN")]
    [DataRow("engageStrengthRatio", "0")]
    [DataRow("engageStrengthRatio", "-1")]
    [DataRow("engageStrengthRatio", "50")]
    [DataRow("homeRadius", "NaN")]
    [DataRow("homeRadius", "0")]
    [DataRow("homeRadius", "-2")]
    [DataRow("homeRadius", "999")]
    [DataRow("spawnRadiusDays", "NaN")]
    [DataRow("spawnRadiusDays", "0")]
    [DataRow("spawnRadiusDays", "-1")]
    [DataRow("spawnRadiusDays", "30")]
    [DataRow("firstHostDelayDays", "-1")]
    [DataRow("firstHostDelayDays", "100000")]
    [DataRow("cooldownDays", "-1")]
    [DataRow("cooldownDays", "100000")]
    [DataRow("warlessStartDay", "-5")]
    [DataRow("warlessStartDay", "100000")]
    [DataRow("minTroops", "0")]
    [DataRow("minTroops", "-4")]
    [DataRow("minTroops", "5000")]
    [DataRow("maxTroops", "0")]
    [DataRow("maxTroops", "5000")]
    [DataRow("quotaMin", "0")]
    [DataRow("quotaMin", "1000")]
    [DataRow("quotaMax", "0")]
    [DataRow("quotaMax", "1000")]
    [DataRow("quotaExtraMin", "-1")]
    [DataRow("quotaExtraMin", "1000")]
    [DataRow("quotaExtraMax", "-1")]
    [DataRow("quotaExtraMax", "1000")]
    [DataRow("nearestVillages", "0")]
    [DataRow("nearestVillages", "11")]
    [DataRow("maxHostDays", "0")]
    [DataRow("maxHostDays", "9999")]
    public void GetConfig_InvalidTuningField_RevertsThatFieldAndWarns(string field, string value)
    {
        WriteTuning($"\"{field}\": {value}");

        var t = _sut.GetConfig().Tuning;

        var defaults = new FireAndTributeTuning();
        var property = typeof(FireAndTributeTuning).GetProperties()
            .Single(p => string.Equals(p.Name, field, System.StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(property.GetValue(defaults), property.GetValue(t), field + " keeps its default");
        AssertWarned(field);
        AssertWarned("invalid values");
    }

    [DataTestMethod]
    [DataRow("\"minTroops\": 150, \"maxTroops\": 100", "minTroops/maxTroops")]
    [DataRow("\"quotaMin\": 9, \"quotaMax\": 3", "quotaMin/quotaMax")]
    [DataRow("\"quotaExtraMin\": 5, \"quotaExtraMax\": 1", "quotaExtraMin/quotaExtraMax")]
    public void GetConfig_UnorderedPair_RevertsBothAndWarns(string body, string pair)
    {
        WriteTuning(body);

        var t = _sut.GetConfig().Tuning;

        Assert.AreEqual(80, t.MinTroops);
        Assert.AreEqual(200, t.MaxTroops);
        Assert.AreEqual(6, t.QuotaMin);
        Assert.AreEqual(15, t.QuotaMax);
        Assert.AreEqual(0, t.QuotaExtraMin);
        Assert.AreEqual(3, t.QuotaExtraMax);
        AssertWarned(pair);
    }

    [TestMethod]
    public void GetConfig_EqualPairBounds_AreAccepted()
    {
        WriteTuning("\"minTroops\": 90, \"maxTroops\": 90, \"quotaMin\": 7, \"quotaMax\": 7, \"quotaExtraMin\": 2, \"quotaExtraMax\": 2");

        var t = _sut.GetConfig().Tuning;

        Assert.AreEqual(90, t.MinTroops);
        Assert.AreEqual(90, t.MaxTroops);
        Assert.AreEqual(7, t.QuotaMin);
        Assert.AreEqual(2, t.QuotaExtraMax);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_AnInvalidFieldDoesNotRevertItsNeighbours()
    {
        WriteTuning("\"spawnChance\": 9, \"cooldownDays\": 77");

        var t = _sut.GetConfig().Tuning;

        Assert.AreEqual(0.05f, t.SpawnChance);
        Assert.AreEqual(77, t.CooldownDays);
    }

    // ---- unknown keys ----------------------------------------------------------------------------------------------

    [TestMethod]
    public void GetConfig_UnknownRootKey_IsWarnedAndIgnored()
    {
        Write("{ \"hostz\": [], \"hosts\": [ " + GoodHost + " ] }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1, c.Hosts.Count);
        AssertWarned("unknown key 'hostz'");
    }

    [TestMethod]
    public void GetConfig_UnknownTuningKey_IsWarnedAndIgnored()
    {
        WriteTuning("\"spawnChanse\": 0.9");

        var c = _sut.GetConfig();

        Assert.AreEqual(0.05f, c.Tuning.SpawnChance);
        AssertWarned("unknown key 'spawnChanse'");
    }

    [TestMethod]
    public void GetConfig_UnknownHostKey_IsWarnedAndTheHostKept()
    {
        WriteHost(GoodHost.Replace("\"id\": \"gundabad\"", "\"id\": \"gundabad\", \"sdie\": \"Free\""));

        var c = _sut.GetConfig();

        Assert.AreEqual(1, c.Hosts.Count);
        AssertWarned("unknown key 'sdie'");
    }

    [TestMethod]
    public void GetConfig_UnknownTroopKey_IsWarnedAndTheTroopKept()
    {
        WriteHost(GoodHost.Replace("\"share\": 0.5 }, {", "\"share\": 0.5, \"weight\": 2 }, {"));

        var c = _sut.GetConfig();

        Assert.AreEqual(2, c.Hosts.Single().Troops.Count);
        AssertWarned("unknown key 'weight'");
    }

    // ---- hosts: one test per rule ----------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow("\"side\": \"Evil\"", "\"side\": \"Good\"")]
    [DataRow("\"side\": \"Evil\"", "\"side\": \"Evil, Free\"")]
    [DataRow("\"side\": \"Evil\"", "\"side\": \"+1\"")]
    [DataRow("\"side\": \"Evil\"", "\"side\": \"1\"")]
    [DataRow("\"side\": \"Evil\"", "\"side\": null")]
    [DataRow("\"clan\": \"gundabad_raiders\"", "\"clan\": \"mordor_raiders\"")]
    [DataRow("\"clan\": \"gundabad_raiders\"", "\"clan\": null")]
    [DataRow("\"id\": \"gundabad\"", "\"id\": \"Gundabad Host\"")]
    [DataRow("\"id\": \"gundabad\"", "\"id\": \"\"")]
    [DataRow("\"id\": \"gundabad\"", "\"id\": null")]
    [DataRow("\"anchors\": [\"town_G1\"]", "\"anchors\": []")]
    [DataRow("\"anchors\": [\"town_G1\"]", "\"anchors\": [\"bad id!\"]")]
    [DataRow("\"anchors\": [\"town_G1\"]", "\"anchors\": null")]
    [DataRow("\"anchors\": [\"town_G1\"]", "\"regions\": [\"town_G1\"]")]
    [DataRow("\"targetSides\": [\"Free\"]", "\"targetSides\": []")]
    [DataRow("\"targetSides\": [\"Free\"]", "\"targetSides\": [\"Neutral\"]")]
    [DataRow("\"targetSides\": [\"Free\"]", "\"targetSides\": [\"Evil\"]")]
    [DataRow("\"targetSides\": [\"Free\"]", "\"targetSides\": [\"Goodies\"]")]
    [DataRow("\"targetSides\": [\"Free\"]", "\"targetSides\": null")]
    [DataRow("{ \"id\": \"gundabad_snaga\", \"share\": 0.5 }, { \"id\": \"gundabad_grunt\", \"share\": 0.5 }", "")]
    [DataRow("{ \"id\": \"gundabad_snaga\", \"share\": 0.5 }, { \"id\": \"gundabad_grunt\", \"share\": 0.5 }", "{ \"id\": \"gundabad_snaga\", \"share\": 0.3 }, { \"id\": \"gundabad_grunt\", \"share\": 0.3 }")]
    [DataRow("{ \"id\": \"gundabad_snaga\", \"share\": 0.5 }, { \"id\": \"gundabad_grunt\", \"share\": 0.5 }", "{ \"id\": \"gundabad_snaga\", \"share\": 0.9 }, { \"id\": \"gundabad_grunt\", \"share\": 0.9 }")]
    [DataRow("{ \"id\": \"gundabad_snaga\", \"share\": 0.5 }, { \"id\": \"gundabad_grunt\", \"share\": 0.5 }", "{ \"id\": \"gundabad_snaga\", \"share\": 0 }")]
    [DataRow("{ \"id\": \"gundabad_snaga\", \"share\": 0.5 }, { \"id\": \"gundabad_grunt\", \"share\": 0.5 }", "{ \"id\": \"gundabad_snaga\", \"share\": NaN }")]
    [DataRow("{ \"id\": \"gundabad_snaga\", \"share\": 0.5 }, { \"id\": \"gundabad_grunt\", \"share\": 0.5 }", "{ \"id\": \"gundabad_snaga\", \"share\": -1 }")]
    [DataRow("{ \"id\": \"gundabad_snaga\", \"share\": 0.5 }, { \"id\": \"gundabad_grunt\", \"share\": 0.5 }", "{ \"id\": \"gundabad_snaga\", \"share\": 1.5 }")]
    [DataRow("{ \"id\": \"gundabad_snaga\", \"share\": 0.5 }, { \"id\": \"gundabad_grunt\", \"share\": 0.5 }", "{ \"id\": \"Not A Troop\", \"share\": 1 }")]
    public void GetConfig_InvalidHost_IsSkippedWithAWarning(string original, string replacement)
    {
        WriteHost(GoodHost.Replace(original, replacement));

        var c = _sut.GetConfig();

        Assert.AreEqual(0, c.Hosts.Count);
        AssertWarned("skipped");
        AssertWarned("invalid values");
    }

    [TestMethod]
    public void GetConfig_SideNames_AreCaseInsensitiveAndTrimmed()
    {
        WriteHost(GoodHost.Replace("\"side\": \"Evil\"", "\"side\": \" evil \"").Replace("[\"Free\"]", "[\"free\"]"));

        var h = _sut.GetConfig().Hosts.Single();

        Assert.AreEqual(FactionSide.Evil, h.Side);
        CollectionAssert.AreEqual(new[] { FactionSide.Free }, h.TargetSides.ToArray());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_NullHostRow_IsSkipped()
    {
        Write("{ \"hosts\": [ null, " + GoodHost + " ] }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1, c.Hosts.Count);
        AssertWarned("the row is null");
    }

    [TestMethod]
    public void GetConfig_DuplicateHostId_KeepsTheFirst()
    {
        Write("{ \"hosts\": [ " + GoodHost + ", " + GoodHost.Replace("\"Evil\"", "\"Neutral\"").Replace("\"Free\"", "\"Evil\"") + " ] }");

        var c = _sut.GetConfig();

        Assert.AreEqual(1, c.Hosts.Count);
        Assert.AreEqual(FactionSide.Evil, c.Hosts[0].Side);
        AssertWarned("already used");
    }

    [TestMethod]
    public void GetConfig_OneBadHostDoesNotSinkTheOthers()
    {
        Write("{ \"hosts\": [ " + GoodHost.Replace("\"Evil\"", "\"Nope\"") + ", " + GoodHost.Replace("gundabad\"", "second\"").Replace("taom_fat_host_gundabad", "taom_fat_host_second") + " ] }");

        var c = _sut.GetConfig();

        Assert.AreEqual("second", c.Hosts.Single().Id);
    }

    [DataTestMethod]
    [DataRow("\"name\": \"{=taom_fat_host_gundabad}The Host of Gundabad\"", "\"name\": \"The Host\"")]
    [DataRow("\"name\": \"{=taom_fat_host_gundabad}The Host of Gundabad\"", "\"name\": \"{=other_key}The Host\"")]
    [DataRow("\"name\": \"{=taom_fat_host_gundabad}The Host of Gundabad\"", "\"name\": \"{=taom_fat_host_gundabad}\"")]
    [DataRow("\"name\": \"{=taom_fat_host_gundabad}The Host of Gundabad\"", "\"name\": \"{=taom_fat_host_gundabad}Bad {BRACE}\"")]
    [DataRow("\"name\": \"{=taom_fat_host_gundabad}The Host of Gundabad\"", "\"name\": \"{=taom_fat_host_rhun}The Host of Gundabad\"")]
    [DataRow("\"name\": \"{=taom_fat_host_gundabad}The Host of Gundabad\"", "\"name\": null")]
    public void GetConfig_InvalidName_FallsBackToAKeyedGenericName(string original, string replacement)
    {
        WriteHost(GoodHost.Replace(original, replacement));

        var h = _sut.GetConfig().Hosts.Single();

        Assert.AreEqual(RaidHostDef.FallbackName, h.Name);
        Assert.AreEqual("{=taom_fat_host_unknown}A raiding host", h.Name, "its own key, never another host's key with other English");
        AssertWarned("name");
    }

    [TestMethod]
    public void GetConfig_OverlongName_FallsBack()
    {
        WriteHost(GoodHost.Replace("The Host of Gundabad", new string('x', 200)));

        Assert.AreEqual(RaidHostDef.FallbackName, _sut.GetConfig().Hosts.Single().Name);
    }

    [TestMethod]
    public void GetConfig_BadAnchorsAreDropped_GoodOnesKept()
    {
        WriteHost(GoodHost.Replace("[\"town_G1\"]", "[\"town_G1\", \"bad id\", \"town_G1\", \"castle_G4\"]"));

        var h = _sut.GetConfig().Hosts.Single();

        CollectionAssert.AreEqual(new[] { "town_G1", "castle_G4" }, h.Anchors.ToArray());
        AssertWarned("anchors entry dropped");
    }

    [TestMethod]
    public void GetConfig_AnchorsPastTheCap_AreDropped_TheFirstTwelveKept()
    {
        var many = string.Join(", ", Enumerable.Range(1, 14).Select(i => "\"town_X" + i + "\""));
        WriteHost(GoodHost.Replace("[\"town_G1\"]", "[" + many + "]"));

        var h = _sut.GetConfig().Hosts.Single();

        Assert.AreEqual(FireAndTributeConfigProvider.MaxAnchors, h.Anchors.Count);
        Assert.AreEqual("town_X12", h.Anchors[11]);
        AssertWarned("anchors entry dropped: 'town_X13' is past the 12 entries allowed");
    }

    [TestMethod]
    public void GetConfig_TheOldRegionsKey_IsAnUnknownKey_AndTheHostHasNoAnchor()
    {
        WriteHost(GoodHost.Replace("\"anchors\"", "\"regions\""));

        Assert.AreEqual(0, _sut.GetConfig().Hosts.Count);
        AssertWarned("unknown key 'regions'");
        AssertWarned("no usable anchor");
    }

    [TestMethod]
    public void GetConfig_ReadsTheFileAsUtf8_AccentedNamesSurvive()
    {
        File.WriteAllText(Path.Combine(_featureDir, "fire_and_tribute.json"),
            "{ \"hosts\": [ " + GoodHost.Replace("The Host of Gundabad", "The Host of Rh\u00fbn and An\u00f3rien") + " ] }",
            new System.Text.UTF8Encoding(false));

        Assert.AreEqual("{=taom_fat_host_gundabad}The Host of Rh\u00fbn and An\u00f3rien", _sut.GetConfig().Hosts.Single().Name);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_TargetSides_DropNeutralAndOwnSideButKeepFree()
    {
        WriteHost(GoodHost.Replace("[\"Free\"]", "[\"Free\", \"Neutral\", \"Evil\", \"Free\"]"));

        var h = _sut.GetConfig().Hosts.Single();

        CollectionAssert.AreEqual(new[] { FactionSide.Free }, h.TargetSides.ToArray());
        AssertWarned("never raids a Neutral realm");
        AssertWarned("never raids its own side");
    }

    [TestMethod]
    public void GetConfig_NeutralHost_MayTargetFreeAndEvil()
    {
        WriteHost(GoodHost.Replace("\"Evil\"", "\"Neutral\"").Replace("[\"Free\"]", "[\"Free\", \"Evil\"]"));

        var h = _sut.GetConfig().Hosts.Single();

        CollectionAssert.AreEqual(new[] { FactionSide.Free, FactionSide.Evil }, h.TargetSides.ToArray());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_BadPreferredTargets_AreDropped()
    {
        WriteHost(GoodHost.Replace("[\"erebor\"]", "[\"erebor\", \"Bad Id\", \"erebor\", \"mirkwood\"]"));

        var h = _sut.GetConfig().Hosts.Single();

        CollectionAssert.AreEqual(new[] { "erebor", "mirkwood" }, h.PreferredTargets.ToArray());
        AssertWarned("preferredTargets entry dropped");
    }

    [TestMethod]
    public void GetConfig_PreferredTargetsPastTheCap_AreDropped_TheFirstTwelveKept()
    {
        var many = string.Join(", ", Enumerable.Range(1, 14).Select(i => "\"realm" + i + "\""));
        WriteHost(GoodHost.Replace("[\"erebor\"]", "[" + many + "]"));

        var h = _sut.GetConfig().Hosts.Single();

        Assert.AreEqual(FireAndTributeConfigProvider.MaxPreferred, h.PreferredTargets.Count);
        Assert.AreEqual("realm12", h.PreferredTargets[11]);
        AssertWarned("preferredTargets entry dropped: 'realm13' is past the 12 entries allowed");
    }

    [TestMethod]
    public void GetConfig_TroopsPastTheCap_AreDropped_TheHostSurvivesOnTheFirstTwelve()
    {
        var twelve = string.Join(", ", Enumerable.Range(1, 12).Select(i => "{ \"id\": \"troop" + i + "\", \"share\": 0.083333 }"));
        var extra = ", { \"id\": \"troop13\", \"share\": 0.05 }, { \"id\": \"troop14\", \"share\": 0.05 }";
        WriteHost(GoodHost.Replace("{ \"id\": \"gundabad_snaga\", \"share\": 0.5 }, { \"id\": \"gundabad_grunt\", \"share\": 0.5 }", twelve + extra));

        var h = _sut.GetConfig().Hosts.Single();

        Assert.AreEqual(FireAndTributeConfigProvider.MaxTroops, h.Troops.Count);
        AssertWarned("troops entry dropped: 'troop13' is past the 12 troops allowed");
    }

    [TestMethod]
    public void GetConfig_ANullTroopRow_IsDropped_TheHostSurvives()
    {
        WriteHost(GoodHost.Replace("\"troops\": [ {", "\"troops\": [ null, {"));

        var h = _sut.GetConfig().Hosts.Single();

        Assert.AreEqual(2, h.Troops.Count);
        AssertWarned("troops entry dropped: a row is null");
    }

    [TestMethod]
    public void GetConfig_NoPreferredTargets_IsFine()
    {
        WriteHost(GoodHost.Replace("\"preferredTargets\": [\"erebor\"],", ""));

        Assert.AreEqual(0, _sut.GetConfig().Hosts.Single().PreferredTargets.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_BadTroopRowsAreDropped_TheHostSurvivesWhenTheRestSumsToOne()
    {
        WriteHost(GoodHost.Replace("{ \"id\": \"gundabad_grunt\", \"share\": 0.5 }",
            "{ \"id\": \"gundabad_grunt\", \"share\": 0.5 }, { \"id\": \"gundabad_snaga\", \"share\": 0.2 }, { \"id\": \"ghost\", \"share\": NaN }"));

        var h = _sut.GetConfig().Hosts.Single();

        Assert.AreEqual(2, h.Troops.Count);
        AssertWarned("listed twice");
        AssertWarned("share NaN");
    }

    [TestMethod]
    public void GetConfig_SharesWithinTolerance_AreAccepted()
    {
        WriteHost(GoodHost.Replace("\"share\": 0.5 }, {", "\"share\": 0.505 }, {"));

        Assert.AreEqual(1, _sut.GetConfig().Hosts.Count);
    }

    // ---- the shipped file ------------------------------------------------------------------------------------------

    [TestMethod]
    public void ShippedFile_LoadsWithNoWarningAndEveryTuningDefault()
    {
        var shipped = RepoPaths.RepoPath("Main", "_Module", "ModuleData", "fire_and_tribute", "fire_and_tribute.json");
        File.Copy(shipped, Path.Combine(_featureDir, "fire_and_tribute.json"));

        var c = _sut.GetConfig();

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        var d = new FireAndTributeTuning();
        foreach (var p in typeof(FireAndTributeTuning).GetProperties().Where(p => p.Name != nameof(FireAndTributeTuning.Unknown)))
            Assert.AreEqual(p.GetValue(d), p.GetValue(c.Tuning), p.Name + " in the shipped file matches the compiled default");
        Assert.AreEqual(8, c.Hosts.Count);
    }
}
