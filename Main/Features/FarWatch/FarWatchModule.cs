using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.FarWatch.Hooks;
using TAOM.Features.FieldCamp;
using TAOM.Features.HiddenHosts;

namespace TAOM.Features.FarWatch;

/// <summary>
/// Far Watch (#847, docs/features/far-watch.md): send a scout from the field camp. The module registers the services and
/// builds the behavior with <c>new</c> inside the declaration's factory, which the runner calls once per campaign start, so the
/// behavior's constructor resets the process-lifetime store for every campaign (pinned by FarWatchWiringTests). It owns save
/// data (the ride, the kept sight, the rest and the named hosts), so a fault in the factory fails closed. It is listed AFTER
/// Host Review: both insert a camp option at index 5, and the order decides which lands first (see
/// <see cref="FarWatchCampOption"/>). No Harmony patch: the sight rides the existing spotting-contributor seam, and the two
/// Hidden Hosts rules it needs are in that feature's service, reached through <see cref="IScoutIntelQuery"/>, which the store
/// implements.
/// </summary>
internal sealed class FarWatchModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new FarWatchBehavior(
            r.Resolve<FarWatchService>(), r.Resolve<FarWatchStore>(), r.Resolve<IGameMenuAdapter>(), r.Resolve<IFarWatchWorldAdapter>(),
            r.Resolve<IFarWatchNotifier>(), r.Resolve<IModLogger>())),
    };

    public override string Id => "FarWatch";

    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<FarWatchStore>(Reuse.Singleton);
        registrator.RegisterMapping<IScoutIntelQuery, FarWatchStore>();
        registrator.Register<IFarWatchWorldAdapter, FarWatchWorldAdapter>(Reuse.Singleton);
        registrator.Register<IFarWatchNotifier, FarWatchNotifier>(Reuse.Singleton);
        registrator.Register<FarWatchService>(Reuse.Singleton);
        registrator.Register<IPartySpottingContributor, FarWatchSightContributor>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
