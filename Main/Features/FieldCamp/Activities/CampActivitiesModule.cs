using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FieldCamp.Hooks;
using TAOM.Features.FieldCamp.Activities.Hooks;

namespace TAOM.Features.FieldCamp.Activities;

/// <summary>
/// Camp work (#860, docs/features/field-camp.md "Camp work"): from a raised field or fortified camp the men hunt, fell timber or
/// fish for a timed stretch and bring goods back. The module registers the service and the adapter and builds the behavior with
/// <c>new</c> inside the declaration's factory, which the runner calls once per campaign start. It owns no save data of its own
/// (the work is a field of the camp record the Field Camp behavior already saves), so it need not fail closed. It is listed LAST
/// among the modules that put an option on the camp panel: all insert at index 5, the later listener runs first, and so ends
/// lowest, under Far Watch (pinned by FarWatchWiringTests). It also owns rest healing (<see cref="CampRestHealing"/>, asked by
/// <c>TaomPartyHealingModel</c>) and start in camp (<see cref="CampStartInCamp"/>, built fresh per campaign inside the factory).
/// No Harmony patch.
/// </summary>
internal sealed class CampActivitiesModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new CampActivitiesBehavior(
            r.Resolve<CampActivityService>(), r.Resolve<ICampWorkAdapter>(), r.Resolve<IGameMenuAdapter>(), r.Resolve<IModLogger>(),
            new CampStartInCamp(r.Resolve<ICampService>(), r.Resolve<ICampSettingsProvider>(), r.Resolve<ICoopSessionProvider>(),
                r.Resolve<IDedicatedServerProvider>(), r.Resolve<IModLogger>(),
                reason => r.Resolve<ICampWorkAdapter>().ShowMessage(CampRestStartTexts.StartBlocked(reason))))),
    };

    public override string Id => "CampActivities";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ICampWorkAdapter, CampWorkAdapter>(Reuse.Singleton);
        registrator.Register<CampActivityService>(Reuse.Singleton);
        registrator.Register<CampRestHealing>(Reuse.Singleton);
    }

    /// <summary>The camp panel asks this for its resting rates (its controller is built by SubModule with <c>new</c>, so it cannot take a parameter).</summary>
    public override void InitializeStatics(IResolver resolver) =>
        FieldCampMenuController.RestStatus = resolver.Resolve<CampRestHealing>();

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
