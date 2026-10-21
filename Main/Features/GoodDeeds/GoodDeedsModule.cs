using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.GoodDeeds.Hooks;
using TAOM.Features.GoodDeeds.Models;

namespace TAOM.Features.GoodDeeds;

/// <summary>
/// Good Deeds (docs/features/good-deeds.md, #864): relation earned by releasing a captured lord, donating a hero prisoner to a
/// settlement and winning a town's tournament, each scaled by alignment. Three entry points share one service and one MCM
/// group: a campaign behavior with a <c>HeroPrisonerReleased</c> listener and its own <c>TournamentFinished</c> listener, and
/// <c>TaomPrisonerDonationModel</c> on the <c>PrisonerDonationModel</c> slot (campaign only). TournamentRewards is untouched
/// and knows nothing of this feature. No Harmony patch and nothing saved; the service holds no per-campaign state.
/// Overlap check, 2026-10-20: no other TAOM code fills the <c>PrisonerDonationModel</c> slot or subscribes to
/// <c>HeroPrisonerReleased</c> to change relation (Enlistment and Hard Questions listen to clean their own state).
/// The shared pieces come from other registrations and are never registered here: <c>IRelationGrantService</c>
/// (<c>RelationsModule</c>, never a second default), <c>IAlignmentService</c> (<c>ExecutionIoC</c>), <c>ICoopSessionProvider</c>
/// and <c>IDedicatedServerProvider</c> (the kernel and CoopInterop).
/// </summary>
internal sealed class GoodDeedsModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new GoodDeedsBehavior(
            r.Resolve<GoodDeedsService>(),
            r.Resolve<IGoodDeedsHeroAdapter>(),
            r.Resolve<IModLogger>())),
    };

    private static readonly GameModelDecl[] Models =
    {
        GameModelDecl.Of<PrisonerDonationModel, TaomPrisonerDonationModel>(ModelTarget.Campaign,
            resolver => new TaomPrisonerDonationModel(resolver.Resolve<GoodDeedsService>())),
    };

    public override string Id => "GoodDeeds";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IGoodDeedsSettingsProvider, GoodDeedsSettingsProvider>(Reuse.Singleton);
        registrator.Register<IGoodDeedsHeroAdapter, GoodDeedsHeroAdapter>(Reuse.Singleton);
        registrator.Register<IGoodDeedsNoticeAdapter, GoodDeedsNoticeAdapter>(Reuse.Singleton);
        registrator.Register<GoodDeedsService>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;

    public override IReadOnlyList<GameModelDecl> GameModels => Models;
}
