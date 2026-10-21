using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.LongMemory.Hooks;

namespace TAOM.Features.LongMemory;

/// <summary>
/// Long Memory (docs/features/long-memory.md, #865): extra relation lost with a village owner's clan leader and the village's
/// notables for raiding the village or forcing it to give goods or recruits, scaled by alignment and stacked on vanilla's own
/// loss. One entry point, a campaign behavior with four listeners, over one service and one MCM group. No Harmony patch, no
/// game model and nothing saved; the service holds no per-campaign state. Overlap check, 2026-10-21: the raid listeners in
/// SpecialResources and WarOfTheRingMomentum handle rewards and momentum, not relations, and no other TAOM code subscribes to the
/// three completed events or to <c>GameMenuOptionSelectedEvent</c> to change relation. The shared pieces come from other
/// registrations and are never registered here: <c>IRelationGrantService</c> (<c>RelationsModule</c>, never a second default),
/// <c>IAlignmentService</c> (<c>ExecutionIoC</c>), <c>ICoopSessionProvider</c> and <c>IDedicatedServerProvider</c> (the kernel and
/// CoopInterop). The relation pieces Good Deeds also uses (<c>SideMultipliers</c>, <c>RelationTarget</c>,
/// <c>GrantStrongestPerOwner</c>) live in <c>Features/Relations</c>.
/// </summary>
internal sealed class LongMemoryModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new LongMemoryBehavior(
            r.Resolve<LongMemoryService>(),
            r.Resolve<ILongMemoryVillageAdapter>(),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "LongMemory";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ILongMemorySettingsProvider, LongMemorySettingsProvider>(Reuse.Singleton);
        registrator.Register<ILongMemoryVillageAdapter, LongMemoryVillageAdapter>(Reuse.Singleton);
        registrator.Register<ILongMemoryNoticeAdapter, LongMemoryNoticeAdapter>(Reuse.Singleton);
        registrator.Register<LongMemoryService>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
