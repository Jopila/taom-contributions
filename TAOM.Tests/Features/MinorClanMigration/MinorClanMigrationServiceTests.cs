using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MinorClanMigration;

namespace TAOM.Tests.Features.MinorClanMigration;

/// <summary>
/// The load migration (#957 part 2). A save keeps Clan.Culture and the clan's template list, and the Factions
/// file is not reloaded on load, so an old save still has the vanilla values. The service decides from raw
/// facts; the adapter applies. Fakes here are stateful, so a second run proves the migration is idempotent.
/// </summary>
[TestClass]
public class MinorClanMigrationServiceTests
{
    private IMinorClanMigrationAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private MinorClanMigrationService _sut = null!;
    private Dictionary<string, MinorClanFacts> _clans = null!;
    private HashSet<string> _characters = null!;

    [TestInitialize]
    public void Setup()
    {
        _clans = new Dictionary<string, MinorClanFacts>();
        _characters = new HashSet<string>();
        _adapter = Substitute.For<IMinorClanMigrationAdapter>();
        _adapter.Snapshot(Arg.Any<IReadOnlyCollection<string>>())
            .Returns(ci => _clans.Values.Where(c => ci.Arg<IReadOnlyCollection<string>>().Contains(c.ClanId)).ToList());
        _adapter.CharacterExists(Arg.Any<string>()).Returns(ci => _characters.Contains(ci.Arg<string>()));
        _adapter.SetCulture(Arg.Any<string>(), Arg.Any<string>()).Returns(ci =>
        {
            var f = _clans[ci.ArgAt<string>(0)];
            _clans[f.ClanId] = f with { CultureId = ci.ArgAt<string>(1) };
            return true;
        });
        _adapter.ReplaceTemplates(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns(ci =>
        {
            var f = _clans[ci.ArgAt<string>(0)];
            _clans[f.ClanId] = f with { TemplateIds = ci.ArgAt<IReadOnlyList<string>>(1) };
            return true;
        });
        _logger = Substitute.For<IModLogger>();
        _sut = new MinorClanMigrationService(_adapter, _logger);
    }

    private void Clan(string id, string culture, params string[] templates) =>
        _clans[id] = new MinorClanFacts(id, culture, templates);

    private void Clone(string original) => _characters.Add("taom_" + original);

    [TestMethod]
    public void Migrate_GreyCompanyOnTheVanillaCulture_MovesToArthedain()
    {
        Clan("legion_of_the_betrayed", "empire");

        var changed = _sut.Migrate();

        Assert.AreEqual(1, changed);
        Assert.AreEqual("arthedain", _clans["legion_of_the_betrayed"].CultureId);
    }

    [TestMethod]
    public void Migrate_CorsairBladesOnTheVanillaCulture_MovesToUmbar()
    {
        Clan("company_of_the_boar", "vlandia");

        var changed = _sut.Migrate();

        Assert.AreEqual(1, changed);
        Assert.AreEqual("umbar", _clans["company_of_the_boar"].CultureId);
    }

    [TestMethod]
    public void Migrate_CultureAlreadyRight_WritesNothing()
    {
        Clan("legion_of_the_betrayed", "arthedain");
        Clan("company_of_the_boar", "umbar");

        var changed = _sut.Migrate();

        Assert.AreEqual(0, changed);
        _adapter.DidNotReceive().SetCulture(Arg.Any<string>(), Arg.Any<string>());
        _adapter.DidNotReceive().ReplaceTemplates(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>());
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void Migrate_BeorningsAndDunlendingReavers_KeepTheirClanCulture()
    {
        Clan("eleftheroi", "empire");
        Clan("brotherhood_of_woods", "empire");

        _sut.Migrate();

        _adapter.DidNotReceive().SetCulture(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    [DataRow("legion_of_the_betrayed")]
    [DataRow("company_of_the_boar")]
    [DataRow("eleftheroi")]
    [DataRow("brotherhood_of_woods")]
    public void Migrate_OriginalLeaderTemplates_AreReplacedByTheirClonesInOrder(string clan)
    {
        var a = $"spc_{clan}_leader_0";
        var b = $"spc_{clan}_leader_1";
        Clan(clan, "x", a, b);
        Clone(a);
        Clone(b);

        var changed = _sut.Migrate();

        Assert.AreEqual(1, changed);
        CollectionAssert.AreEqual(new[] { "taom_" + a, "taom_" + b }, _clans[clan].TemplateIds.ToArray());
    }

    [TestMethod]
    public void Migrate_AMissingClone_KeepsThatOriginalAndReplacesTheRest()
    {
        Clan("eleftheroi", "empire", "spc_eleftheroi_leader_0", "spc_eleftheroi_leader_1");
        Clone("spc_eleftheroi_leader_1");

        _sut.Migrate();

        CollectionAssert.AreEqual(new[] { "spc_eleftheroi_leader_0", "taom_spc_eleftheroi_leader_1" },
            _clans["eleftheroi"].TemplateIds.ToArray());
    }

    [TestMethod]
    public void Migrate_NoCloneExistsAtAll_ChangesNothing()
    {
        Clan("eleftheroi", "empire", "spc_eleftheroi_leader_0");

        var changed = _sut.Migrate();

        Assert.AreEqual(0, changed);
        _adapter.DidNotReceive().ReplaceTemplates(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>());
    }

    [TestMethod]
    public void Migrate_TemplatesAlreadyClones_WritesNothing()
    {
        Clan("eleftheroi", "empire", "taom_spc_eleftheroi_leader_0");
        Clone("spc_eleftheroi_leader_0");

        var changed = _sut.Migrate();

        Assert.AreEqual(0, changed);
        _adapter.DidNotReceive().ReplaceTemplates(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>());
    }

    [TestMethod]
    public void Migrate_ATemplateThatIsNotAnOriginalLeader_IsKept()
    {
        Clan("eleftheroi", "empire", "some_other_template", "spc_eleftheroi_leader_0");
        Clone("some_other_template");
        Clone("spc_eleftheroi_leader_0");

        _sut.Migrate();

        CollectionAssert.AreEqual(new[] { "some_other_template", "taom_spc_eleftheroi_leader_0" },
            _clans["eleftheroi"].TemplateIds.ToArray());
    }

    [TestMethod]
    public void Migrate_AClanOutsideTheFour_IsNeverTouched()
    {
        Clan("ghilman", "darshi", "spc_ghilman_leader_0");
        Clone("spc_ghilman_leader_0");

        var changed = _sut.Migrate();

        Assert.AreEqual(0, changed);
        _adapter.DidNotReceive().SetCulture(Arg.Any<string>(), Arg.Any<string>());
        _adapter.DidNotReceive().ReplaceTemplates(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>());
    }

    [TestMethod]
    public void Migrate_AdapterReturnsAFactForAnUnaskedClan_IsIgnored()
    {
        _adapter.Snapshot(Arg.Any<IReadOnlyCollection<string>>())
            .Returns(new List<MinorClanFacts> { new("legion_of_the_betrayed_x", "empire", new List<string>()) });

        var changed = _sut.Migrate();

        Assert.AreEqual(0, changed);
        _adapter.DidNotReceive().SetCulture(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Migrate_AsksOnlyForTheFourClans()
    {
        _sut.Migrate();

        _adapter.Received(1).Snapshot(Arg.Is<IReadOnlyCollection<string>>(ids =>
            ids.OrderBy(i => i).SequenceEqual(new[]
            {
                "brotherhood_of_woods", "company_of_the_boar", "eleftheroi", "legion_of_the_betrayed",
            })));
    }

    [TestMethod]
    public void Migrate_NoMatchingClansInTheCampaign_ReturnsZero()
    {
        Assert.AreEqual(0, _sut.Migrate());
    }

    [TestMethod]
    public void Migrate_CultureAndTemplatesChangeTogether_CountsOneClanAndLogsOneInfoLine()
    {
        Clan("legion_of_the_betrayed", "empire", "spc_legion_of_the_betrayed_leader_0");
        Clone("spc_legion_of_the_betrayed_leader_0");

        var changed = _sut.Migrate();

        Assert.AreEqual(1, changed);
        _logger.Received(1).LogInfo(Arg.Is<string>(m =>
            m.Contains("legion_of_the_betrayed") && m.Contains("empire") && m.Contains("arthedain")));
    }

    [TestMethod]
    public void Migrate_TwoClansChange_LogsOneInfoLineEach()
    {
        Clan("legion_of_the_betrayed", "empire");
        Clan("company_of_the_boar", "vlandia");

        var changed = _sut.Migrate();

        Assert.AreEqual(2, changed);
        _logger.Received(2).LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void Migrate_RunTwice_SecondRunChangesNothing()
    {
        Clan("legion_of_the_betrayed", "empire", "spc_legion_of_the_betrayed_leader_0");
        Clone("spc_legion_of_the_betrayed_leader_0");
        Clan("company_of_the_boar", "vlandia", "spc_company_of_the_boar_leader_0");
        Clone("spc_company_of_the_boar_leader_0");

        Assert.AreEqual(2, _sut.Migrate());
        Assert.AreEqual(0, _sut.Migrate());
    }

    [TestMethod]
    public void Migrate_AdapterRefusesTheCulture_WarnsAndDoesNotCountTheClan()
    {
        Clan("legion_of_the_betrayed", "empire");
        _adapter.SetCulture(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var changed = _sut.Migrate();

        Assert.AreEqual(0, changed);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("legion_of_the_betrayed") && m.Contains("arthedain")));
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void Migrate_AdapterRefusesTheTemplates_WarnsAndDoesNotCountTheClan()
    {
        Clan("eleftheroi", "empire", "spc_eleftheroi_leader_0");
        Clone("spc_eleftheroi_leader_0");
        _adapter.ReplaceTemplates(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns(false);

        var changed = _sut.Migrate();

        Assert.AreEqual(0, changed);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("eleftheroi")));
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void Migrate_AdapterRefusesTheCulture_StillMovesTheTemplates()
    {
        Clan("legion_of_the_betrayed", "empire", "spc_legion_of_the_betrayed_leader_0");
        Clone("spc_legion_of_the_betrayed_leader_0");
        _adapter.SetCulture(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var changed = _sut.Migrate();

        Assert.AreEqual(1, changed);
        Assert.AreEqual("taom_spc_legion_of_the_betrayed_leader_0", _clans["legion_of_the_betrayed"].TemplateIds[0]);
    }
}
