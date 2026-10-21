using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.NamedCompanions;
using TAOM.Features.NamedCompanions.Domain;
using TAOM.Features.NamedCompanions.Oaths;
using TAOM.Features.NamedCompanions.Oaths.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.NamedCompanions.Oaths;

/// <summary>
/// Vanilla's SwapCompanions runs on the same daily tick after TAOM's listener (listeners run newest first), so an
/// anchor run on the daily tick is undone the same day. The daily tick only raises a flag; the next hourly tick
/// anchors once.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class OathsOfServiceCampaignBehaviorTests
{
    private const string Id = "named_companion_gimli";
    private const string Home = "town_E1";

    private INamedCompanionConfigProvider _config = null!;
    private INamedCompanionAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private OathLedger _ledger = null!;
    private HomeAnchorService _anchor = null!;
    private OathsOfServiceCampaignBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<INamedCompanionConfigProvider>();
        _adapter = Substitute.For<INamedCompanionAdapter>();
        _logger = Substitute.For<IModLogger>();
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        _config.GetCompanions().Returns(new List<NamedCompanionDefinition>
        {
            new() { CharacterId = Id, SpawnSettlement = Home, Enabled = true, StayAtHome = true },
        });
        _adapter.IsHeroActive(Id).Returns(true);
        _adapter.GetCurrentSettlementId(Id).Returns("town_X9");
        _ledger = new OathLedger(coop);
        _anchor = new HomeAnchorService(_config, _adapter, coop, _logger);
        _sut = new OathsOfServiceCampaignBehavior(_ledger, _anchor, _logger);
    }

    [TestMethod]
    public void DailyTick_Alone_MovesNobody()
    {
        _sut.OnDailyTick();

        _adapter.DidNotReceive().ReturnHome(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void HourlyTick_AfterTheDailyTick_AnchorsOnce_AndNotAgain()
    {
        _sut.OnDailyTick();

        _sut.OnHourlyTick();
        _sut.OnHourlyTick();

        _adapter.Received(1).ReturnHome(Id, Home);
    }

    [TestMethod]
    public void HourlyTick_WithoutADailyTick_MovesNobody()
    {
        _sut.OnHourlyTick();

        _adapter.DidNotReceive().ReturnHome(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void NextDay_AnchorsAgain()
    {
        _sut.OnDailyTick();
        _sut.OnHourlyTick();
        _sut.OnDailyTick();
        _sut.OnHourlyTick();

        _adapter.Received(2).ReturnHome(Id, Home);
    }

    [TestMethod]
    public void HourlyTick_AnchorThrows_LogsAndDoesNotRetryEveryHour()
    {
        _config.GetCompanions().Returns(_ => throw new System.InvalidOperationException("boom"));
        _sut.OnDailyTick();

        _sut.OnHourlyTick();
        _sut.OnHourlyTick();

        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Constructor_ResetsTheLedgerForANewCampaign()
    {
        _ledger.Set("from_the_last_campaign");
        _ledger.SetTopicLastDay("c", "t", 3);

        new OathsOfServiceCampaignBehavior(_ledger, _anchor, _logger);

        Assert.IsFalse(_ledger.Has("from_the_last_campaign"));
        Assert.IsFalse(_ledger.TryGetTopicLastDay("c", "t", out _));
    }

    [TestMethod]
    public void SaveThenLoad_IntoAFreshBehavior_RestoresFlagsAndTopicDays()
    {
        _ledger.Set("aragorn.dunedain_told");
        _ledger.SetTopicLastDay("named_companion_aragorn", "aragorn_first", 42);
        var store = new EngineDataStore { Loading = false };
        _sut.SyncData(store);

        store.Loading = true;
        var next = new OathsOfServiceCampaignBehavior(_ledger, _anchor, _logger);
        Assert.IsFalse(_ledger.Has("aragorn.dunedain_told"), "precondition: the fresh behavior's constructor reset the ledger");
        next.SyncData(store);

        Assert.IsTrue(_ledger.Has("aragorn.dunedain_told"));
        Assert.IsTrue(_ledger.TryGetTopicLastDay("named_companion_aragorn", "aragorn_first", out var day));
        Assert.AreEqual(42, day);
        Assert.IsTrue(store.Has("_taom_oath_flagList"));
        Assert.IsTrue(store.Has("_taom_oath_topicDays"));
    }

    [TestMethod]
    public void Load_OfASaveWithoutTheKeys_LoadsAnEmptyLedger()
    {
        _ledger.Set("leftover");

        new OathsOfServiceCampaignBehavior(_ledger, _anchor, _logger).SyncData(new EngineDataStore { Loading = true });

        Assert.IsFalse(_ledger.Has("leftover"));
        Assert.AreEqual(0, _ledger.Export().Flags.Count);
    }
}
