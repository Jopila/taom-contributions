using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Features.ArmyTargeting.HoldTheWalls.Hooks;

/// <summary>
/// Hold the Walls' campaign entry point (#871): routes the settlement owner change, the army dispersal and the hourly and daily ticks to
/// <see cref="IHoldTheWallsService"/>. The service gates every call on single-player (a co-op session, a co-op mod TAOM cannot
/// probe or a dedicated server stands the feature down and ends the holds), so no handler here decides anything except <c>OnDailyTick</c>, which is not gated: it only writes the debug line. Built with
/// <c>new</c> by <see cref="HoldTheWallsModule"/> for every campaign, before any event, so its constructor is the per-campaign
/// reset (pinned by HoldTheWallsWiringTests). It saves nothing: the engine saves and expires the AI pause itself. Each handler is
/// guarded: a campaign event that throws takes the game down.
/// </summary>
public sealed class HoldTheWallsBehavior : CampaignBehaviorBase
{
    private readonly IHoldTheWallsService _service;
    private readonly IModLogger _logger;

    public HoldTheWallsBehavior(IHoldTheWallsService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
        _service.ResetForNewSession();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, OnArmyDispersed);
    }

    /// <summary>Nothing is saved: the pause is the engine's own and ends by itself, and a loaded save starts with no record.</summary>
    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero oldOwner, Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        try
        {
            bool fortification = settlement != null && (settlement.IsTown || settlement.IsCastle);
            _service.OnSettlementOwnerChanged(settlement?.StringId, fortification, detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege);
        }
        catch (Exception ex) { _logger.LogError($"[HoldTheWalls] owner change failed: {ex}"); }
    }

    private void OnArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isPlayersArmy)
    {
        try
        {
            if (reason == Army.ArmyDispersionReason.Inactivity)
                _service.OnInactivityDisband(isPlayersArmy);
        }
        catch (Exception ex) { _logger.LogError($"[HoldTheWalls] army dispersal failed: {ex}"); }
    }

    private void OnHourlyTick()
    {
        try { _service.OnHourlyTick(); }
        catch (Exception ex) { _logger.LogError($"[HoldTheWalls] hourly pass failed: {ex}"); }
    }

    private void OnDailyTick()
    {
        try { _service.LogDailySummary(); }
        catch (Exception ex) { _logger.LogError($"[HoldTheWalls] daily summary failed: {ex}"); }
    }
}
