using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Core.Logging;
using TAOM.Features.ArmyTargeting.HoldTheWalls;
using TAOM.Features.ArmyTargeting.HoldTheWalls.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.ArmyTargeting.HoldTheWalls;

/// <summary>
/// #871: the service is a process-lifetime singleton, so the behavior's constructor is the per-campaign reset (the module
/// builds a fresh behavior for every campaign start, pinned in <see cref="HoldTheWallsWiringTests"/>), and the handlers only
/// route and guard. A behavior needs the engine's types, so these run with the game assemblies.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HoldTheWallsBehaviorTests
{
    private const ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail BySiege = ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege;

    private IHoldTheWallsService _service = null!;
    private IModLogger _logger = null!;
    private HoldTheWallsBehavior _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _service = Substitute.For<IHoldTheWallsService>();
        _logger = Substitute.For<IModLogger>();
        _sut = new HoldTheWallsBehavior(_service, _logger);
    }

    private void Invoke(string method, params object?[] args) =>
        typeof(HoldTheWallsBehavior).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(_sut, args);

    [TestMethod]
    public void Constructor_ResetsTheService_ForANewCampaign()
    {
        _service.Received(1).ResetForNewSession();
    }

    [TestMethod]
    public void SyncData_SavesAndLoadsNothing()
    {
        var store = new EngineDataStore { Loading = false };
        _sut.SyncData(store);
        store.Loading = true;
        _sut.SyncData(store);

        _service.DidNotReceiveWithAnyArgs().OnHourlyTick();
        _service.DidNotReceiveWithAnyArgs().OnSettlementOwnerChanged(null, false, false);
    }

    [TestMethod]
    public void HourlyAndDailyTicks_RouteToTheService()
    {
        Invoke("OnHourlyTick");
        Invoke("OnDailyTick");

        _service.Received(1).OnHourlyTick();
        _service.Received(1).LogDailySummary();
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnInactivityDisband_RoutesToTheService_WithWhoseArmyItWas(bool isPlayersArmy)
    {
        Invoke("OnArmyDispersed", null, Army.ArmyDispersionReason.Inactivity, isPlayersArmy);

        _service.Received(1).OnInactivityDisband(isPlayersArmy);
    }

    [DataTestMethod]
    [DataRow(Army.ArmyDispersionReason.Unknown)]
    [DataRow(Army.ArmyDispersionReason.CohesionDepleted)]
    [DataRow(Army.ArmyDispersionReason.FoodProblem)]
    [DataRow(Army.ArmyDispersionReason.ObjectiveFinished)]
    public void AnyOtherDispersalReason_IsNotCounted(Army.ArmyDispersionReason reason)
    {
        Invoke("OnArmyDispersed", null, reason, false);

        _service.DidNotReceiveWithAnyArgs().OnInactivityDisband(default);
    }

    [TestMethod]
    public void ANullSettlement_IsPassedAsNoFortification()
    {
        Invoke("OnSettlementOwnerChanged", null, false, null, null, null, BySiege);

        _service.Received(1).OnSettlementOwnerChanged(null, false, true);
    }

    [TestMethod]
    public void ANonSiegeDetail_IsPassedAsNotBySiege()
    {
        Invoke("OnSettlementOwnerChanged", null, false, null, null, null,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByKingDecision);

        _service.Received(1).OnSettlementOwnerChanged(null, false, false);
    }

    [TestMethod]
    public void AThrowingService_IsLoggedNotRaised_ForEveryHandler()
    {
        _service.When(s => s.OnHourlyTick()).Do(_ => throw new InvalidOperationException("boom"));
        _service.When(s => s.LogDailySummary()).Do(_ => throw new InvalidOperationException("boom"));
        _service.When(s => s.OnSettlementOwnerChanged(Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<bool>()))
            .Do(_ => throw new InvalidOperationException("boom"));
        _service.When(s => s.OnInactivityDisband(Arg.Any<bool>())).Do(_ => throw new InvalidOperationException("boom"));

        Invoke("OnHourlyTick");
        Invoke("OnDailyTick");
        Invoke("OnSettlementOwnerChanged", null, false, null, null, null, BySiege);
        Invoke("OnArmyDispersed", null, Army.ArmyDispersionReason.Inactivity, false);

        _logger.Received(4).LogError(Arg.Is<string>(s => s.Contains("[HoldTheWalls]") && s.Contains("boom")));
    }
}
