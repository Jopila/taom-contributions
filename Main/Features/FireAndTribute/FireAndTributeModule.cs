using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.FireAndTribute.Hooks;

namespace TAOM.Features.FireAndTribute;

/// <summary>
/// Fire and Tribute's raiding hosts as a feature module (#870, docs/features/fire-and-tribute.md). The behavior is built
/// with <c>new</c> inside the declaration's factory, which the runner calls once per campaign start, so its constructor
/// resets the singleton service for every campaign. It owns save data (the hosts, the cooldowns and the first-seen day),
/// so a fault in it fails the campaign closed.
/// </summary>
internal sealed class FireAndTributeModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new FireAndTributeBehavior(r.Resolve<IFireAndTributeService>(), r.Resolve<IModLogger>())),
    };

    public override string Id => "FireAndTribute";

    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator) => FireAndTributeIoC.RegisterFireAndTributeFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
