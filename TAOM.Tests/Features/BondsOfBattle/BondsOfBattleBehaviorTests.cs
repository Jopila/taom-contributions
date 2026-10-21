using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaleWorlds.CampaignSystem.MapEvents;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.BondsOfBattle;
using TAOM.Features.BondsOfBattle.Hooks;
using TAOM.Features.BondsOfBattle.Models;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Execution;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.BondsOfBattle;

/// <summary>
/// The handler is reachable because it is internal + InternalsVisibleTo. A MapEvent cannot be built in a unit test, so an
/// uninitialized one stands in: the adapter is substituted and never reads it, which is where the real decisions are.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class BondsOfBattleBehaviorTests
{
    private static readonly BondsOfBattleSettings Defaults = new(true, 1, false, 5, true, 25f, true, 15);

    private IBattleRelationAdapter _battles = null!;
    private IBondsOfBattleSettingsProvider _settings = null!;
    private ICoopSessionProvider _coop = null!;
    private IRelationGrantService _grants = null!;
    private IModLogger _logger = null!;
    private BondsOfBattleBehavior _sut = null!;
    private TroopHandoverService _handover = null!;
    private IRelationGrantService _handoverGrants = null!;
    private MapEvent _mapEvent = null!;

    [TestInitialize]
    public void Setup()
    {
        _battles = Substitute.For<IBattleRelationAdapter>();
        _settings = Substitute.For<IBondsOfBattleSettingsProvider>();
        _settings.Get().Returns(Defaults);
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.ShouldDeferToHost.Returns(false);
        _grants = Substitute.For<IRelationGrantService>();
        _grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(1);
        _logger = Substitute.For<IModLogger>();
        var enlistment = Substitute.For<IEnlistmentStateQuery>();
        var service = new BattleRelationService(enlistment, Substitute.For<IRandomSource>(), _grants, Substitute.For<IBondsNoticeAdapter>(),
            Substitute.For<TAOM.Features.Execution.IAlignmentService>());
        _handoverGrants = Substitute.For<IRelationGrantService>();
        _handoverGrants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(call => call.ArgAt<int>(1));
        _handover = NewHandover(_handoverGrants);
        _sut = new BondsOfBattleBehavior(service, _handover, _battles, _settings, _coop, _logger);
        _mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
        _battles.Capture(Arg.Any<MapEvent>(), Arg.Any<float>()).Returns(AllyBattle());
    }

    private TroopHandoverService NewHandover(IRelationGrantService grants) =>
        new(grants, _settings, Substitute.For<IEnlistmentStateQuery>(), Substitute.For<IRandomSource>(),
            Substitute.For<IBondsNoticeAdapter>(), _coop);

    private static BattleSnapshot AllyBattle() =>
        new(new List<BattleParticipant> { new("ally", "Ally", true, false, false, false, true, "ally", false) },
            true, 0, new List<SettlementNotables>(), false, new SideIds("free_f", string.Empty), false, 0);

    [TestMethod]
    public void OnPlayerBattleEnd_AnAllyBattle_GrantsTheAllyBonus()
    {
        _sut.OnPlayerBattleEnd(_mapEvent);

        _grants.Received(1).Grant("ally", 1, Arg.Any<string>());
    }

    [TestMethod]
    public void OnDailyTick_ClearsTheHandoverDay()
    {
        Assert.IsTrue(_handover.OnHandover("lord_1", "Lord One", 20, false, false, null));
        Assert.IsFalse(_handover.OnHandover("lord_1", "Lord One", 20, false, false, null), "the day is full");

        _sut.OnDailyTick();

        Assert.IsTrue(_handover.OnHandover("lord_1", "Lord One", 20, false, false, null), "a new day pays again");
    }

    [TestMethod]
    public void Construction_ResetsTheHandoverDay_SoANewCampaignStartsEmpty()
    {
        Assert.IsTrue(_handover.OnHandover("lord_1", "Lord One", 20, false, false, null));
        Assert.IsFalse(_handover.OnHandover("lord_1", "Lord One", 20, false, false, null));

        // The module builds the behavior again in the next campaign's OnGameStart, against the same process singleton.
        var service = new BattleRelationService(Substitute.For<IEnlistmentStateQuery>(), Substitute.For<IRandomSource>(), _grants,
            Substitute.For<IBondsNoticeAdapter>(), Substitute.For<TAOM.Features.Execution.IAlignmentService>());
        _ = new BondsOfBattleBehavior(service, _handover, _battles, _settings, _coop, _logger);

        Assert.IsTrue(_handover.OnHandover("lord_1", "Lord One", 20, false, false, null));
    }

    [TestMethod]
    public void OnPlayerBattleEnd_NullMapEvent_DoesNothing()
    {
        _sut.OnPlayerBattleEnd(null!);

        _battles.DidNotReceiveWithAnyArgs().Capture(default!, default);
    }

    [TestMethod]
    public void OnPlayerBattleEnd_CoopClientDefersToTheHost_DoesNothing()
    {
        _coop.ShouldDeferToHost.Returns(true);

        _sut.OnPlayerBattleEnd(_mapEvent);

        _battles.DidNotReceiveWithAnyArgs().Capture(default!, default);
        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void OnPlayerBattleEnd_FeatureOff_ReadsNothingAndGrantsNothing()
    {
        _settings.Get().Returns(Defaults with { Enabled = false });

        _sut.OnPlayerBattleEnd(_mapEvent);

        _battles.DidNotReceiveWithAnyArgs().Capture(default!, default);
        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
    }

    [TestMethod]
    public void OnPlayerBattleEnd_GratitudeOff_AsksTheAdapterForRadiusZero()
    {
        _settings.Get().Returns(Defaults with { BanditGratitudeEnabled = false });

        _sut.OnPlayerBattleEnd(_mapEvent);

        _battles.Received(1).Capture(_mapEvent, 0f);
    }

    [TestMethod]
    public void OnPlayerBattleEnd_GratitudeOn_AsksTheAdapterForTheConfiguredRadius()
    {
        _settings.Get().Returns(Defaults with { BanditGratitudeRadius = 40f });

        _sut.OnPlayerBattleEnd(_mapEvent);

        _battles.Received(1).Capture(_mapEvent, 40f);
    }

    [TestMethod]
    public void OnPlayerBattleEnd_TheAdapterCannotReadTheBattle_GrantsNothing()
    {
        _battles.Capture(Arg.Any<MapEvent>(), Arg.Any<float>()).Returns((BattleSnapshot?)null);

        _sut.OnPlayerBattleEnd(_mapEvent);

        _grants.DidNotReceiveWithAnyArgs().Grant(default!, default, default!);
        _logger.DidNotReceiveWithAnyArgs().LogError(default!);
    }

    [TestMethod]
    public void OnPlayerBattleEnd_TheServiceThrows_IsLoggedAndSwallowed()
    {
        _grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Throws(new InvalidOperationException("boom"));

        _sut.OnPlayerBattleEnd(_mapEvent);

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("boom")));
    }
}
