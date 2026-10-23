using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.HostReview.Hooks;

namespace TAOM.Features.HostReview;

/// <summary>
/// Host Review (#891, docs/features/host-review.md): a "Review the host" option on the Field Camp
/// sub-menu opens a battle scene with the healthy party standing in formation and no enemy. The player
/// commands as general, nobody can be hurt, and holding the leave key leaves. No Harmony patch: the option is a
/// campaign behavior, the scene is a mission of its own name, and the vanilla order bar is made
/// available to it by one registered order provider.
/// </summary>
internal sealed class HostReviewModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new HostReviewCampaignBehavior(r.Resolve<HostReviewService>())),
    };

    public override string Id => "HostReview";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IHostReviewPartyAdapter, HostReviewPartyAdapter>(Reuse.Singleton);
        registrator.Register<IHostReviewMissionLauncher, HostReviewMissionLauncher>(Reuse.Singleton);
        registrator.Register<HostReviewService>(Reuse.Singleton);
        registrator.Register<IHostReviewOrderProviderRegistrar, NullHostReviewOrderProviderRegistrar>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        // Once per process (the engine's factory rejects a second provider of the same type); the provider is
        // TAOM.Client's, behind the registrar seam.
        if (phase == ApplyPhase.ProcessLoad)
            resolver.Resolve<IHostReviewOrderProviderRegistrar>().Register();
    }
}
