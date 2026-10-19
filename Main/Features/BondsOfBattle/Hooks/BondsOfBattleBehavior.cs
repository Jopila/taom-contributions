using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.BondsOfBattle.Hooks;

/// <summary>
/// Thin boundary for the relation a battle earns (docs/features/bonds-of-battle.md, ADR-002): at the end of each player
/// battle it snapshots the map event and hands it to <see cref="BattleRelationService"/>, and on each daily tick it starts a new hand-over day. Yields to the co-op host
/// (the dedicated-server rule lives in the relation grant service). Nothing is saved. The event fires before the map
/// event commits its results (<c>PlayerEncounter.DoApplyMapEventResults</c>), so the sides and casualties are still readable.
/// </summary>
public sealed class BondsOfBattleBehavior : CampaignBehaviorBase
{
    private readonly BattleRelationService _service;
    private readonly TroopHandoverService _handover;
    private readonly IBattleRelationAdapter _battles;
    private readonly IBondsOfBattleSettingsProvider _settings;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;

    public BondsOfBattleBehavior(BattleRelationService service, TroopHandoverService handover, IBattleRelationAdapter battles,
        IBondsOfBattleSettingsProvider settings, ICoopSessionProvider coop, IModLogger logger)
    {
        _service = service;
        _handover = handover;
        _battles = battles;
        _settings = settings;
        _coop = coop;
        _logger = logger;
        // The module builds this behavior in every campaign's OnGameStart, and TroopHandoverService is a process singleton:
        // a new campaign, or a load mid-day, starts the daily hand-over ledger empty.
        _handover.ResetForNewSession();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    // Internal for TAOM.Tests (InternalsVisibleTo): the event cannot be raised outside a campaign.
    internal void OnDailyTick() => _handover.StartNewDay();

    public override void SyncData(IDataStore dataStore)
    {
    }

    // Internal for TAOM.Tests (InternalsVisibleTo): the event cannot be raised outside a campaign.
    internal void OnPlayerBattleEnd(MapEvent mapEvent)
    {
        try
        {
            if (mapEvent == null || _coop.ShouldDeferToHost)
                return;

            var settings = _settings.Get();
            if (!settings.Enabled)
                return;

            var snapshot = _battles.Capture(mapEvent, settings.BanditGratitudeEnabled ? settings.BanditGratitudeRadius : 0f);
            if (snapshot != null)
                _service.Apply(snapshot, settings);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[BondsOfBattle] battle end handling failed: {ex.Message}");
        }
    }
}
