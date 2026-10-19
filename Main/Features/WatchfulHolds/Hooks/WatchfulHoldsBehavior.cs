using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.WatchfulHolds.Hooks;

/// <summary>
/// Watchful Holds' campaign entry point: routes the hourly party tick and the daily tick to
/// <see cref="IWatchfulHoldsService"/> in singleplayer only: it stands down in any co-op session and under a co-op mod
/// TAOM cannot probe (<c>IsAuthority</c> fails open there), because it changes the outcome of parties other peers control. Built with <c>new</c> by <see cref="WatchfulHoldsModule"/> for every campaign, so its
/// constructor is the per-campaign reset of the service's horn clock, counters and fort list (pinned by WatchfulHoldsWiringTests).
/// Nothing is saved. Each handler is guarded: a campaign event that throws takes the game down.
/// </summary>
public sealed class WatchfulHoldsBehavior : CampaignBehaviorBase
{
    private readonly IWatchfulHoldsService _service;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IModLogger _logger;

    public WatchfulHoldsBehavior(IWatchfulHoldsService service, ICoopSessionProvider coopSession, IModLogger logger)
    {
        _service = service;
        _coopSession = coopSession;
        _logger = logger;
        _service.ResetForNewSession();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnHourlyTickParty);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnHourlyTickParty(MobileParty party)
    {
        // The cheap filter first: hundreds of parties tick every hour and only lords and the player matter.
        if (party == null || !(party.IsLordParty || party.IsMainParty))
            return;
        if (_coopSession.IsSessionActive || _coopSession.ShouldDeferToHost)
            return;
        try { _service.OnHourlyTickParty(party); }
        catch (Exception ex) { _logger.LogError($"[WatchfulHolds] hourly check failed: {ex}"); }
    }

    private void OnDailyTick()
    {
        if (_coopSession.IsSessionActive || _coopSession.ShouldDeferToHost)
            return;
        try { _service.LogDailySummary(); }
        catch (Exception ex) { _logger.LogError($"[WatchfulHolds] daily summary failed: {ex}"); }
    }
}
