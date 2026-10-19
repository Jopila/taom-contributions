using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FromTheAshes.Hooks;

/// <summary>
/// The campaign entry point of From the Ashes (#856): registers the menus, runs the hourly reconcile and the map-event
/// listener, and owns the save halves of the one job (docs/features/from-the-ashes.md). Built fresh for every campaign start by
/// <see cref="FromTheAshesModule"/>, before any event and before a load's SyncData, so its constructor is the per-campaign
/// reset of the process-lifetime <see cref="AshesJobStore"/>. Deliberately no OnGameLoaded handler: a throw there loops the
/// loading screen (docs/reviews/lessons/campaign-mechanics.md), and a bad record is dropped inside
/// <see cref="AshesJobStore.LoadFrom"/> instead. Every decision lives in <see cref="FromTheAshesService"/>; the menu glue
/// lives in <see cref="FromTheAshesStartOptions"/>, <see cref="FromTheAshesWaitMenu"/>, <see cref="FromTheAshesReconcile"/> and <see cref="FromTheAshesLanding"/> (ADR-002).
/// </summary>
public sealed class FromTheAshesBehavior : CampaignBehaviorBase
{
    private readonly AshesJobStore _store;
    private readonly FromTheAshesStartOptions _options;
    private readonly FromTheAshesWaitMenu _wait;
    private readonly FromTheAshesReconcile _reconcile;

    public FromTheAshesBehavior(FromTheAshesService service, AshesJobStore store, IGameMenuAdapter menus, IEncounterAdapter encounter, IModLogger logger)
    {
        _store = store;
        _options = new FromTheAshesStartOptions(service, menus, logger);
        var landing = new FromTheAshesLanding(menus, encounter, logger);
        _wait = new FromTheAshesWaitMenu(service, landing, logger);
        _reconcile = new FromTheAshesReconcile(service, menus, landing, logger);
        _store.Clear();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddMenus);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, _reconcile.OnHourlyTick);
        CampaignEvents.MapEventStarted.AddNonSerializedListener(this, _reconcile.OnMapEventStarted);
    }

    private void AddMenus(CampaignGameStarter starter)
    {
        _options.AddOptions(starter);
        _wait.AddMenu(starter);
    }

    /// <summary>
    /// Five primitive keys. A load fills fresh locals first (a missing key leaves its default, so a save from before the
    /// feature loads as "no job"); a save never reads the store back into itself. The start is a double of campaign hours,
    /// which keeps the precision a float loses at a campaign's age.
    /// </summary>
    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsLoading)
        {
            string? site = null;
            var kind = 0;
            var start = 0d;
            var hours = 0f;
            var credited = 0;
            dataStore.SyncData("_taomAshesSite", ref site);
            dataStore.SyncData("_taomAshesKind", ref kind);
            dataStore.SyncData("_taomAshesStart", ref start);
            dataStore.SyncData("_taomAshesHours", ref hours);
            dataStore.SyncData("_taomAshesCredited", ref credited);
            _store.LoadFrom(site, kind, start, hours, credited);
        }
        else
        {
            _store.SaveInto(out var site, out var kind, out var start, out var hours, out var credited);
            dataStore.SyncData("_taomAshesSite", ref site);
            dataStore.SyncData("_taomAshesKind", ref kind);
            dataStore.SyncData("_taomAshesStart", ref start);
            dataStore.SyncData("_taomAshesHours", ref hours);
            dataStore.SyncData("_taomAshesCredited", ref credited);
        }
    }
}
