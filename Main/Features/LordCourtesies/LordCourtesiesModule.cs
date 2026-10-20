using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Features.LordCourtesies.Hooks;

namespace TAOM.Features.LordCourtesies;

/// <summary>
/// Lord Courtesies (docs/features/lord-courtesies.md) as a feature module: a service graph and one dialog
/// behavior, no patch, no model and no save data. The behavior is built with <c>new</c> for every campaign,
/// because it holds the last domain answer and a container singleton is constructed once per process.
/// </summary>
internal sealed class LordCourtesiesModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new LordCourtesiesDialogBehavior(r.Resolve<LordCourtesiesService>())),
    };

    public override string Id => "LordCourtesies";

    public override void RegisterServices(IRegistrator registrator) =>
        LordCourtesiesIoC.RegisterLordCourtesiesFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
