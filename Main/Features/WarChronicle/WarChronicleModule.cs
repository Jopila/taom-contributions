using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.WarChronicle;

/// <summary>
/// The War Chronicle as a feature module (docs/features/war-chronicle.md, issue #765): the timed-effect
/// registry, the baselines, the rally, the war ledger and the chronicle's event chain with its army-focus
/// windows; the Eye of Sauron hunt comes in a later milestone.
/// Both campaign behaviors are built fresh for each campaign (a lambda body, not a container singleton)
/// because their constructors reset the per-campaign singletons; WarChronicleWiringTests pins the shape.
/// </summary>
internal sealed class WarChronicleModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new WarChronicleBehavior(
            r.Resolve<WarChronicleTickService>(),
            r.Resolve<WarChronicleStateService>(),
            r.Resolve<Ledger.WarLedgerService>(),
            r.Resolve<ICoopSessionProvider>(),
            r.Resolve<IModLogger>())),

        // #765 M3: the event chain's router, also built per campaign: its constructor clears the
        // announcement queue and the army focus. WarChronicleBehavior stays the one SyncData owner.
        CampaignBehaviorDecl.Of(r => new Events.WarChronicleEventsBehavior(
            r.Resolve<Events.ChronicleService>(),
            r.Resolve<Events.ChronicleAnnouncementQueue>(),
            r.Resolve<ICoopSessionProvider>())),
    };

    public override string Id => "WarChronicle";

    /// <summary>The campaign behavior persists the effects, baselines and rally tiers in SyncData.</summary>
    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator) =>
        WarChronicleIoC.RegisterWarChronicleFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
