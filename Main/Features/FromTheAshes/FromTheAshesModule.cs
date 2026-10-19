using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.FromTheAshes.Hooks;

namespace TAOM.Features.FromTheAshes;

/// <summary>
/// From the Ashes (#856, docs/features/from-the-ashes.md): the player's company rebuilds a looted village of his realm, or
/// helps with a fief's construction project. The module registers the services and builds the behavior with <c>new</c>
/// inside the declaration's factory, which the runner calls once per campaign start, so the behavior's constructor resets
/// the process-lifetime job store for every campaign (pinned by FromTheAshesWiringTests). It owns save data (the one job),
/// so a fault in the factory fails closed.
/// </summary>
internal sealed class FromTheAshesModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new FromTheAshesBehavior(
            r.Resolve<FromTheAshesService>(), r.Resolve<AshesJobStore>(), r.Resolve<IGameMenuAdapter>(), r.Resolve<IEncounterAdapter>(), r.Resolve<IModLogger>())),
    };

    public override string Id => "FromTheAshes";

    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IFromTheAshesConfigProvider, FromTheAshesConfigProvider>(Reuse.Singleton);
        registrator.Register<IFromTheAshesSettingsProvider, FromTheAshesSettingsProvider>(Reuse.Singleton);
        registrator.Register<IAshesWorldAdapter, AshesWorldAdapter>(Reuse.Singleton);
        registrator.Register<AshesJobStore>(Reuse.Singleton);
        registrator.Register<FromTheAshesService>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
