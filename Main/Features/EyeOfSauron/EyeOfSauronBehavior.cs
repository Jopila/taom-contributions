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

    /// <summary>
    /// One key per field of <see cref="EyeOfSauronState"/>. A save captures the service; a load fills a fresh record
    /// (a missing key leaves its default) and hands it over the constructor's reset.
    /// </summary>
    public override void SyncData(IDataStore dataStore)
    {
        var state = dataStore.IsSaving ? _service.CaptureState() : new EyeOfSauronState();
        dataStore.SyncData("_taom_eye_scoreVsEvil", ref state.ScoreVsEvil);
        dataStore.SyncData("_taom_eye_scoreVsFree", ref state.ScoreVsFree);
        dataStore.SyncData("_taom_eye_lastDecayDay", ref state.LastDecayDay);
        dataStore.SyncData("_taom_eye_cooldownUntilDay", ref state.CooldownUntilDay);
        dataStore.SyncData("_taom_eye_hunterHeroId", ref state.HunterHeroId);
        dataStore.SyncData("_taom_eye_hunterPartyId", ref state.HunterPartyId);
        dataStore.SyncData("_taom_eye_huntStartDay", ref state.HuntStartDay);
        if (dataStore.IsLoading)
            _service.LoadFrom(state);
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
