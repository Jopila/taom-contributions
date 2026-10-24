using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.SettlementServices.Hooks;

namespace TAOM.Features.SettlementServices;

/// <summary>
/// Watch-Silver and Tidings of the Lists (#859, docs/features/settlement-services.md): a governor takes silver for the watch of
/// a fief the player's clan owns, and the arena master names the nearest tournaments for a small fee. Two services, one adapter, two
/// behaviors, each built with <c>new</c> inside its declaration's factory, which the runner calls once per campaign start. No
/// Harmony, no model, no prefab and no load-time work. The feature saves nothing of its own: a fief's loyalty and the player's gold
/// are saved by the engine, so <see cref="OwnsSaveData"/> is false and a fault never fails a campaign start.
/// </summary>
internal sealed class SettlementServicesModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new WatchSilverDialogBehavior(r.Resolve<WatchSilverService>(), r.Resolve<IModLogger>())),
        CampaignBehaviorDecl.Of(r => new TidingsMenuBehavior(r.Resolve<TidingsService>(), r.Resolve<IModLogger>())),
    };

    public override string Id => "SettlementServices";

    /// <summary>False: the engine saves the loyalty and the gold. TAOM writes no record and no id of its own.</summary>
    public override bool OwnsSaveData => false;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<SettlementServicesSettingsProvider>(Reuse.Singleton);
        registrator.Register<ISettlementServicesAdapter, SettlementServicesAdapter>(Reuse.Singleton);
        registrator.Register<WatchSilverService>(Reuse.Singleton);
        registrator.Register<TidingsService>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
