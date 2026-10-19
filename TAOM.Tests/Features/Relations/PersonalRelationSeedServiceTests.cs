using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.Relations;

[TestClass]
public class PersonalRelationSeedServiceTests
{
    private IPersonalRelationSeedAdapter _adapter = null!;
    private IPersonalRelationSettingsProvider _settings = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _server = null!;
    private IModLogger _logger = null!;
    private PersonalRelationSeedService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<IPersonalRelationSeedAdapter>();
        _adapter.Snapshot().Returns(FaramirWorld());
        _adapter.SetRelation(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        _settings = Substitute.For<IPersonalRelationSettingsProvider>();
        _settings.Enabled.Returns(true);
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.ShouldDeferToHost.Returns(false);
        _server = Substitute.For<IDedicatedServerProvider>();
        _server.IsDedicatedServer.Returns(false);
        _logger = Substitute.For<IModLogger>();
        _sut = new PersonalRelationSeedService(_adapter, _settings, _coop, _server, _logger);
    }

    private static SeedRow Row(string id, string? clan, bool leader = false, bool playerClan = false, int relation = 0) =>
        new(id, clan, leader, playerClan, relation);

    private static List<SeedRow> FaramirWorld() => new()
    {
        Row("denethor", "gondor_clan", leader: true, relation: 25),
        Row("faramir", "gondor_clan"),
        Row("boromir", "gondor_clan"),
    };

    private static Dictionary<string, int> Plan(params SeedRow[] rows) =>
        PersonalRelationSeedService.Plan(rows).ToDictionary(c => c.HeroId, c => c.Value);

    // --- the rule ---

    [TestMethod]
    public void Plan_MembersAtZero_TakeTheClanLeadersValue()
    {
        var plan = PersonalRelationSeedService.Plan(FaramirWorld()).ToDictionary(c => c.HeroId, c => c.Value);

        CollectionAssert.AreEquivalent(new[] { "faramir", "boromir" }, plan.Keys.ToList());
        Assert.AreEqual(25, plan["faramir"]);
        Assert.AreEqual(25, plan["boromir"]);
    }

    [TestMethod]
    public void Plan_TheClanLeader_IsNeverWritten()
    {
        Assert.IsFalse(Plan(FaramirWorld().ToArray()).ContainsKey("denethor"));
    }

    [TestMethod]
    public void Plan_AMemberWithAPersonalValue_KeepsIt()
    {
        var plan = Plan(Row("denethor", "g", leader: true, relation: 25), Row("faramir", "g", relation: -7), Row("boromir", "g", relation: 3));

        Assert.AreEqual(0, plan.Count);
    }

    [TestMethod]
    public void Plan_ANegativeLeaderValue_IsCopiedToo()
    {
        var plan = Plan(Row("l", "g", leader: true, relation: -40), Row("m", "g"));

        Assert.AreEqual(-40, plan["m"]);
    }

    [TestMethod]
    public void Plan_ALeaderAtZero_HasNothingToCopy()
    {
        Assert.AreEqual(0, Plan(Row("l", "g", leader: true), Row("m", "g")).Count);
    }

    [TestMethod]
    public void Plan_ThePlayersClan_IsSkipped()
    {
        var plan = Plan(Row("mine_l", "pc", leader: true, playerClan: true, relation: 50), Row("mine_m", "pc", playerClan: true));

        Assert.AreEqual(0, plan.Count);
    }

    [TestMethod]
    public void Plan_AClanlessHero_IsSkipped()
    {
        var plan = Plan(Row("wanderer", null), Row("hermit", ""), Row("denethor", "g", leader: true, relation: 25));

        Assert.AreEqual(0, plan.Count);
    }

    [TestMethod]
    public void Plan_AClanWhoseLeaderIsNotListed_IsSkipped()
    {
        Assert.AreEqual(0, Plan(Row("m1", "g"), Row("m2", "g")).Count);
    }

    [TestMethod]
    public void Plan_EachClanTakesItsOwnLeadersValue()
    {
        var plan = Plan(
            Row("a_l", "a", leader: true, relation: 10), Row("a_m", "a"),
            Row("b_l", "b", leader: true, relation: -20), Row("b_m", "b"));

        Assert.AreEqual(10, plan["a_m"]);
        Assert.AreEqual(-20, plan["b_m"]);
    }

    // --- running it ---

    [TestMethod]
    public void TrySeed_WritesThePlanThroughTheAdapter_AndReportsDone()
    {
        var done = _sut.TrySeed(false);

        Assert.IsTrue(done);
        _adapter.Received(1).SetRelation("faramir", 25);
        _adapter.Received(1).SetRelation("boromir", 25);
        _adapter.DidNotReceive().SetRelation("denethor", Arg.Any<int>());
    }

    [TestMethod]
    public void TrySeed_AlreadySeeded_DoesNothing_AndStaysDone()
    {
        var done = _sut.TrySeed(true);

        Assert.IsTrue(done);
        _adapter.DidNotReceiveWithAnyArgs().Snapshot();
        _adapter.DidNotReceiveWithAnyArgs().SetRelation(default!, default);
    }

    [TestMethod]
    public void TrySeed_FeatureOff_DoesNothing_AndStaysUnset()
    {
        _settings.Enabled.Returns(false);

        Assert.IsFalse(_sut.TrySeed(false));
        _adapter.DidNotReceiveWithAnyArgs().SetRelation(default!, default);
    }

    [TestMethod]
    public void TrySeed_CoopClientDefersToTheHost_DoesNothing_AndStaysUnset()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.IsFalse(_sut.TrySeed(false));
        _adapter.DidNotReceiveWithAnyArgs().Snapshot();
        _adapter.DidNotReceiveWithAnyArgs().SetRelation(default!, default);
    }

    [TestMethod]
    public void TrySeed_DedicatedServer_DoesNothing_AndStaysUnset()
    {
        _server.IsDedicatedServer.Returns(true);

        Assert.IsFalse(_sut.TrySeed(false));
        _adapter.DidNotReceiveWithAnyArgs().Snapshot();
        _adapter.DidNotReceiveWithAnyArgs().SetRelation(default!, default);
    }

    // The loaded flag belongs to the save, which another peer owns: it is handed back untouched, even with the switch off.
    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TrySeed_DefersToHost_KeepsTheLoadedFlag(bool loaded)
    {
        _coop.ShouldDeferToHost.Returns(true);
        _settings.Enabled.Returns(false);

        Assert.AreEqual(loaded, _sut.TrySeed(loaded));
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TrySeed_DedicatedServer_KeepsTheLoadedFlag(bool loaded)
    {
        _server.IsDedicatedServer.Returns(true);

        Assert.AreEqual(loaded, _sut.TrySeed(loaded));
    }

    // A co-op host is not single player: it keeps the loaded flag and seeds nothing, switch on or off.
    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TrySeed_CoopHost_KeepsTheLoadedFlag_AndSeedsNothing(bool loaded)
    {
        _coop.IsSessionActive.Returns(true);
        _coop.ShouldDeferToHost.Returns(false);

        Assert.AreEqual(loaded, _sut.TrySeed(loaded));
        _adapter.DidNotReceiveWithAnyArgs().Snapshot();
        _adapter.DidNotReceiveWithAnyArgs().SetRelation(default!, default);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TrySeed_CoopHost_SwitchOff_StillKeepsTheLoadedFlag(bool loaded)
    {
        _coop.IsSessionActive.Returns(true);
        _settings.Enabled.Returns(false);

        Assert.AreEqual(loaded, _sut.TrySeed(loaded));
    }

    // Seen off, the flag is cleared so that switching on later seeds again, whatever the save said.
    [TestMethod]
    public void TrySeed_SeenOff_ReArms()
    {
        _settings.Enabled.Returns(false);

        Assert.IsFalse(_sut.TrySeed(true));
        _adapter.DidNotReceiveWithAnyArgs().SetRelation(default!, default);
    }

    [TestMethod]
    public void TrySeed_SeenOffThenOn_SeedsAgain()
    {
        _settings.Enabled.Returns(false);
        var flag = _sut.TrySeed(true);
        _settings.Enabled.Returns(true);

        flag = _sut.TrySeed(flag);

        Assert.IsTrue(flag);
        _adapter.Received(1).SetRelation("faramir", 25);
    }

    // --- IsSinglePlayer ---

    [TestMethod]
    public void IsSinglePlayer_NoCoopNoServer_IsTrue()
    {
        Assert.IsTrue(_sut.IsSinglePlayer);
    }

    [TestMethod]
    public void IsSinglePlayer_ACoopSessionIsLive_IsFalse_HostIncluded()
    {
        _coop.IsSessionActive.Returns(true);

        Assert.IsFalse(_sut.IsSinglePlayer);
    }

    [TestMethod]
    public void IsSinglePlayer_DeferringToTheHost_IsFalse()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.IsFalse(_sut.IsSinglePlayer);
    }

    [TestMethod]
    public void IsSinglePlayer_ADedicatedServer_IsFalse()
    {
        _server.IsDedicatedServer.Returns(true);

        Assert.IsFalse(_sut.IsSinglePlayer);
    }

    [TestMethod]
    public void TrySeed_TheCampaignCannotBeRead_StaysUnset_SoItTriesAgain()
    {
        _adapter.Snapshot().Returns((IReadOnlyList<SeedRow>?)null);

        Assert.IsFalse(_sut.TrySeed(false));
        _adapter.DidNotReceiveWithAnyArgs().SetRelation(default!, default);
    }

    [TestMethod]
    public void TrySeed_AHeroTheAdapterRefuses_DoesNotStopTheRest_AndTheSeedingIsDone()
    {
        _adapter.SetRelation("faramir", Arg.Any<int>()).Returns(false);

        Assert.IsTrue(_sut.TrySeed(false));
        _adapter.Received(1).SetRelation("boromir", 25);
    }

    [TestMethod]
    public void TrySeed_NothingToCopy_IsStillDone()
    {
        _adapter.Snapshot().Returns(new List<SeedRow> { Row("l", "g", leader: true, relation: 5) });

        Assert.IsTrue(_sut.TrySeed(false));
        _adapter.DidNotReceiveWithAnyArgs().SetRelation(default!, default);
    }

    [TestMethod]
    public void TrySeed_LogsHowManyWereSeeded()
    {
        _sut.TrySeed(false);

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("seeded 2")));
    }

    [TestMethod]
    public void TrySeed_RunsOnce_TheSecondCallWithTheNewFlagDoesNothing()
    {
        var first = _sut.TrySeed(false);
        _adapter.ClearReceivedCalls();

        var second = _sut.TrySeed(first);

        Assert.IsTrue(second);
        _adapter.DidNotReceiveWithAnyArgs().Snapshot();
        _adapter.DidNotReceiveWithAnyArgs().SetRelation(default!, default);
    }
}
