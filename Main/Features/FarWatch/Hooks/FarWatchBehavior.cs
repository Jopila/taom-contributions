using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FarWatch.Hooks;

/// <summary>
/// The campaign entry point of Far Watch (#847, docs/features/far-watch.md): registers the camp option and the two menus, runs
/// the hourly look and the encounter and map-event listeners, and owns the save halves of the store. Built fresh for every campaign start by
/// <see cref="FarWatchModule"/>, before any event and before a load's SyncData, so its constructor is the per-campaign reset of
/// the process-lifetime <see cref="FarWatchStore"/> (pinned by FarWatchWiringTests). Deliberately no OnGameLoaded handler: a
/// throw there loops the loading screen, and a bad record is dropped inside <see cref="FarWatchStore.LoadFrom"/> instead. Every
/// decision lives in <see cref="FarWatchService"/>; the menu glue is in the classes beside this one (ADR-002).
/// </summary>
internal sealed class FarWatchBehavior : CampaignBehaviorBase
{
    private readonly FarWatchStore _store;
    private readonly FarWatchCampOption _option;
    private readonly FarWatchPickMenu _pick;
    private readonly FarWatchWaitMenu _wait;
    private readonly FarWatchReconcile _reconcile;

    public FarWatchBehavior(FarWatchService service, FarWatchStore store, IGameMenuAdapter menus, IFarWatchWorldAdapter world,
        IFarWatchNotifier notifier, IModLogger logger)
    {
        _store = store;
        var presenter = new FarWatchPresenter(notifier);
        var landing = new FarWatchLanding(menus, world, logger);
        _option = new FarWatchCampOption(service, world, menus, logger);
        _pick = new FarWatchPickMenu(service, world, menus, notifier, logger);
        _wait = new FarWatchWaitMenu(service, world, landing, presenter, logger);
        _reconcile = new FarWatchReconcile(service, world, presenter, logger);
        _store.ResetForNewSession();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, AddMenus);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.MapEventStarted.AddNonSerializedListener(this, _reconcile.OnMapEventStarted);
        CampaignEvents.OnPartyEncounterEvent.AddNonSerializedListener(this, _reconcile.OnPartyEncounter);
    }

    /// <summary>The ride is judged first, then the camp panel is refreshed if the judgement (or a rest ending) changed what it shows.</summary>
    private void OnHourlyTick()
    {
        _reconcile.OnHourlyTick();
        _option.RefreshWhenStale();
    }

    private void AddMenus(CampaignGameStarter starter)
    {
        _option.AddOption(starter);
        _pick.AddMenus(starter);
        _wait.AddMenu(starter);
    }

    /// <summary>
    /// Sixteen primitive keys. A load fills fresh locals first (a missing key leaves its default, so a save from before the
    /// feature loads as "no ride, no sight"); a save never reads the store back into itself. Hours are doubles of campaign
    /// hours, which keeps the precision a float loses at a campaign's age. Strings and the id list are never null on a save.
    /// </summary>
    public override void SyncData(IDataStore dataStore)
    {
        var record = dataStore.IsLoading ? new FarWatchRecord() : _store.SaveInto();
        record.HeroId ??= string.Empty;
        record.HeroName ??= string.Empty;
        record.TargetId ??= string.Empty;
        record.TargetName ??= string.Empty;
        record.RestRiderName ??= string.Empty;
        if (dataStore.IsSaving) record.NamedIds ??= new List<string>();

        dataStore.SyncData("_taomFarWatchKind", ref record.Kind);
        dataStore.SyncData("_taomFarWatchHero", ref record.HeroId);
        dataStore.SyncData("_taomFarWatchName", ref record.HeroName);
        dataStore.SyncData("_taomFarWatchSelf", ref record.SelfScout);
        dataStore.SyncData("_taomFarWatchTarget", ref record.TargetId);
        dataStore.SyncData("_taomFarWatchTargetName", ref record.TargetName);
        dataStore.SyncData("_taomFarWatchStart", ref record.StartHour);
        dataStore.SyncData("_taomFarWatchHours", ref record.DurationHours);
        dataStore.SyncData("_taomFarWatchSkill", ref record.Skill);
        dataStore.SyncData("_taomFarWatchBuff", ref record.BuffLevel);
        dataStore.SyncData("_taomFarWatchBuffStart", ref record.BuffStartHour);
        dataStore.SyncData("_taomFarWatchBuffHold", ref record.BuffHoldHours);
        dataStore.SyncData("_taomFarWatchRest", ref record.RestUntilHour);
        dataStore.SyncData("_taomFarWatchRestName", ref record.RestRiderName);
        dataStore.SyncData("_taomFarWatchNamed", ref record.NamedIds);
        dataStore.SyncData("_taomFarWatchNamedUntil", ref record.NamedUntilHour);

        if (dataStore.IsLoading) _store.LoadFrom(record);
    }
}
