using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.ArmyTargeting.HoldTheWalls.Hooks;

namespace TAOM.Features.ArmyTargeting.HoldTheWalls;

/// <summary>
/// Hold the Walls as a feature module (#871, docs/features/hold-the-walls.md). The behavior is built with <c>new</c> inside the
/// declaration's factory, which the runner calls once per campaign start, so its constructor resets the singleton service for
/// every campaign. It saves nothing (the engine saves and expires the AI pause), so a fault in it does not fail the
/// campaign closed.
/// </summary>
internal sealed class HoldTheWallsModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new HoldTheWallsBehavior(r.Resolve<IHoldTheWallsService>(), r.Resolve<IModLogger>())),
    };

    public override string Id => "HoldTheWalls";

    public override void RegisterServices(IRegistrator registrator) => HoldTheWallsIoC.RegisterHoldTheWallsFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
