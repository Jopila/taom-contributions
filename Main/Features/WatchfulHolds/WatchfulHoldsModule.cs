using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.WatchfulHolds.Hooks;

namespace TAOM.Features.WatchfulHolds;

/// <summary>
/// Watchful Holds as a feature module (issue #822). The behavior is built with <c>new</c> inside
/// the declaration's factory, which the runner calls once per campaign start, so its constructor resets the singleton
/// service for every campaign. It saves nothing, so a fault in it does not fail the campaign closed.
/// </summary>
internal sealed class WatchfulHoldsModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new WatchfulHoldsBehavior(
            r.Resolve<IWatchfulHoldsService>(), r.Resolve<ICoopSessionProvider>(), r.Resolve<IModLogger>())),
    };

    public override string Id => "WatchfulHolds";

    public override void RegisterServices(IRegistrator registrator) => WatchfulHoldsIoC.RegisterWatchfulHoldsFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
