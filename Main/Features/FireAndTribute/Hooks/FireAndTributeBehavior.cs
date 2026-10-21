using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Features.FireAndTribute.Hooks;

/// <summary>
/// Fire and Tribute's campaign entry point (#870): routes five campaign events to <see cref="IFireAndTributeService"/>
/// and persists its record as one JSON string. The service gates the spawn, the steering and the console spawn on single-player
/// (a co-op session, a co-op mod TAOM cannot probe or a dedicated server stands the feature down and releases the
/// hosts), so no handler here decides anything. The village and destroy handlers are not gated and do nothing, because
/// the records are emptied when a session goes live. Built with <c>new</c> by <see cref="FireAndTributeModule"/> for every campaign, before any
/// event and before a load's SyncData, so its constructor is the per-campaign reset (pinned by
/// FireAndTributeWiringTests). Each handler is guarded: a campaign event that throws takes the game down.
/// </summary>
public sealed class FireAndTributeBehavior : CampaignBehaviorBase
{
    private const string SaveKey = "_taom_fire_and_tribute_v1";

    private readonly IFireAndTributeService _service;
    private readonly IModLogger _logger;

    public FireAndTributeBehavior(IFireAndTributeService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
        _service.ResetForNewSession();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.VillageStateChanged.AddNonSerializedListener(this, OnVillageStateChanged);
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
    }

    /// <summary>One key, one JSON string. A save without it (from before the feature) never reaches a load, so it stays reset.</summary>
    public override void SyncData(IDataStore dataStore)
    {
        string json = dataStore.IsSaving ? _service.CaptureState() : string.Empty;
        dataStore.SyncData(SaveKey, ref json);
        if (dataStore.IsLoading)
            _service.LoadFrom(json);
    }

    private void OnSessionLaunched(CampaignGameStarter starter) => Guard("session launch", _service.OnSessionLaunched);

    private void OnDailyTick() => Guard("daily tick", _service.OnDailyTick);

    private void OnHourlyTick() => Guard("hourly step", _service.OnHourlyTick);

    private void OnVillageStateChanged(Village village, Village.VillageStates oldState, Village.VillageStates newState, MobileParty raiderParty)
    {
        try { _service.OnVillageStateChanged(newState == Village.VillageStates.Looted, raiderParty?.StringId); }
        catch (Exception ex) { _logger.LogError($"[FireAndTribute] village state failed: {ex}"); }
    }

    private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyerParty)
    {
        try { _service.OnPartyDestroyed(party?.StringId); }
        catch (Exception ex) { _logger.LogError($"[FireAndTribute] party destroyed failed: {ex}"); }
    }

    private void Guard(string what, Action action)
    {
        try { action(); }
        catch (Exception ex) { _logger.LogError($"[FireAndTribute] {what} failed: {ex}"); }
    }
}
