using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;

namespace TAOM.Features.EyeOfSauron;

/// <summary>
/// The Eye of Sauron hunt's campaign entry point (#765 M8): routes six campaign events to
/// <see cref="EyeOfSauronService"/> and persists its record. Built with <c>new</c> by <see cref="EyeOfSauronModule"/>
/// for every campaign, before any event and before a load's SyncData, so its constructor is the per-campaign reset
/// (pinned by EyeOfSauronWiringTests). Each handler is guarded: a campaign event that throws takes the game down.
/// </summary>
public sealed class EyeOfSauronBehavior : CampaignBehaviorBase
{
    private readonly EyeOfSauronService _service;
    private readonly IModLogger _logger;

    private float _scoreVsEvil;
    private float _scoreVsFree;
    private float _lastDecayDay;
    private float _cooldownUntilDay;
    private string _hunterHeroId = string.Empty;
    private string _hunterPartyId = string.Empty;
    private bool _pinnedByUs;
    private float _huntStartDay;

    public EyeOfSauronBehavior(EyeOfSauronService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
        _service.ResetForNewSession();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnHourlyTickParty);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
        {
            var state = _service.CaptureState();
            _scoreVsEvil = state.ScoreVsEvil;
            _scoreVsFree = state.ScoreVsFree;
            _lastDecayDay = state.LastDecayDay;
            _cooldownUntilDay = state.CooldownUntilDay;
            _hunterHeroId = state.HunterHeroId ?? string.Empty;
            _hunterPartyId = state.HunterPartyId ?? string.Empty;
            _pinnedByUs = state.PinnedByUs;
            _huntStartDay = state.HuntStartDay;
        }

        dataStore.SyncData("_taom_eye_scoreVsEvil", ref _scoreVsEvil);
        dataStore.SyncData("_taom_eye_scoreVsFree", ref _scoreVsFree);
        dataStore.SyncData("_taom_eye_lastDecayDay", ref _lastDecayDay);
        dataStore.SyncData("_taom_eye_cooldownUntilDay", ref _cooldownUntilDay);
        dataStore.SyncData("_taom_eye_hunterHeroId", ref _hunterHeroId);
        dataStore.SyncData("_taom_eye_hunterPartyId", ref _hunterPartyId);
        dataStore.SyncData("_taom_eye_pinnedByUs", ref _pinnedByUs);
        dataStore.SyncData("_taom_eye_huntStartDay", ref _huntStartDay);

        if (dataStore.IsLoading)
        {
            _service.LoadFrom(new EyeOfSauronState
            {
                ScoreVsEvil = _scoreVsEvil,
                ScoreVsFree = _scoreVsFree,
                LastDecayDay = _lastDecayDay,
                CooldownUntilDay = _cooldownUntilDay,
                HunterHeroId = string.IsNullOrEmpty(_hunterHeroId) ? null : _hunterHeroId,
                HunterPartyId = string.IsNullOrEmpty(_hunterPartyId) ? null : _hunterPartyId,
                PinnedByUs = _pinnedByUs,
                HuntStartDay = _huntStartDay,
            });
        }
    }

    private void OnSessionLaunched(CampaignGameStarter starter) => Guard("session launch", _service.OnSessionLaunched);

    private void OnDailyTick() => Guard("daily tick", _service.OnDailyTick);

    private void OnHourlyTickParty(MobileParty party)
    {
        if (!_service.IsHunting) return;
        try { _service.OnHourlyTickParty(party?.StringId); }
        catch (Exception ex) { _logger.LogError($"[EyeOfSauron] hourly step failed: {ex}"); }
    }

    private void OnTick(float dt)
    {
        if (!_service.IsHunting) return;
        try { _service.OnTick(); }
        catch (Exception ex) { _logger.LogError($"[EyeOfSauron] sight update failed: {ex}"); }
    }

    private void OnMapEventEnded(MapEvent mapEvent)
    {
        try { _service.OnMapEventEnded(HuntBattleReader.Summarize(mapEvent, _service.HunterPartyId)); }
        catch (Exception ex) { _logger.LogError($"[EyeOfSauron] battle end failed: {ex}"); }
    }

    private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyer)
    {
        try { _service.OnPartyDestroyed(party?.StringId); }
        catch (Exception ex) { _logger.LogError($"[EyeOfSauron] party destroyed failed: {ex}"); }
    }

    private void Guard(string what, Action action)
    {
        try { action(); }
        catch (Exception ex) { _logger.LogError($"[EyeOfSauron] {what} failed: {ex}"); }
    }
}
