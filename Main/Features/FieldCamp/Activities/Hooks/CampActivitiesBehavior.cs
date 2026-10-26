using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FieldCamp.Activities.Hooks;

/// <summary>
/// The campaign entry point of camp work (#860, docs/features/field-camp.md "Camp work"): registers the camp option, the pick and
/// the wait menu once the session has launched. It saves nothing of its own: the work rides in the camp's <c>CampState</c> and so
/// in the Field Camp behavior's <c>_taomFieldCamps</c> SyncData. Built fresh for every campaign start by
/// <see cref="CampActivitiesModule"/>, so its <see cref="ScreenFaultLog"/> latches are per campaign. Deliberately no OnGameLoaded
/// handler: a load that finds the wait menu open runs its init, which reads the saved hours, and the wait menu has its own tick.
/// The map tick and the character-creation event belong to <see cref="CampStartInCamp"/> alone (#860 part C). Every decision lives in <see cref="CampActivityService"/>; the menu glue is in the classes beside this one (ADR-002).
/// </summary>
internal sealed class CampActivitiesBehavior : CampaignBehaviorBase
{
    private readonly CampWorkMenu _pick;
    private readonly CampWorkWaitMenu _wait;
    private readonly CampStartInCamp _start;

    public CampActivitiesBehavior(CampActivityService service, ICampWorkAdapter work, IGameMenuAdapter menus, IModLogger logger,
        CampStartInCamp start)
    {
        _start = start;
        var faults = new ScreenFaultLog(logger, "FieldCamp");
        _pick = new CampWorkMenu(service, work, menus, logger, faults);
        _wait = new CampWorkWaitMenu(service, work, new CampWorkLanding(menus), faults);
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, AddMenus);
        // Start in camp: the engine raises the first from CharacterCreationState.FinalizeCharacterCreationState (ten times, once per
        // index); the tick is the retry that pitches the camp, and a hook that is not armed costs it one comparison.
        CampaignEvents.OnCharacterCreationIsOverEvent.AddNonSerializedListener(this, _start.OnCharacterCreationIsOver);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, _start.OnMapTick);
    }

    private void AddMenus(CampaignGameStarter starter)
    {
        _pick.AddOption(starter);
        _pick.AddMenus(starter);
        _wait.AddMenu(starter);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }
}
