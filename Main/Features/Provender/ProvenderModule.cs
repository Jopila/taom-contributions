using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.Provender.Hooks;

namespace TAOM.Features.Provender;

/// <summary>
/// The Provender-master (docs/features/provender.md, #908) as a feature module: a service graph and one campaign behavior, no
/// patch and no model. The behavior is built with <c>new</c> for every campaign because it owns the saved orders (a container
/// singleton is constructed once per process), so the module owns save data and fails closed. The shared pieces come from
/// other registrations and are never registered here: <c>IHeroSkillXpAdapter</c>, <c>IPartyItemRosterAdapter</c> and
/// <c>IEnlistmentStateQuery</c> (<c>EnlistmentIoC</c>), <c>ICampService</c> (<c>FieldCampIoC</c>), <c>ICoopSessionProvider</c>
/// and <c>IDedicatedServerProvider</c> (<c>CoopInteropIoC</c>), <c>IAlignmentService</c> (<c>ExecutionIoC</c>), and
/// <c>IRaceManager</c>, <c>IPathService</c> and <c>IModLogger</c> (<c>IoC.cs</c>).
/// </summary>
internal sealed class ProvenderModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new ProvenderBehavior(
            r.Resolve<ProvenderService>(), r.Resolve<ProvenderSaleService>(), r.Resolve<IProvenderWorldAdapter>())),
    };

    public override string Id => "Provender";

    /// <summary>The two standing orders are saved in SyncData.</summary>
    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IProvenderWorldAdapter, ProvenderWorldAdapter>(Reuse.Singleton);
        registrator.Register<IProvenderSettingsProvider, ProvenderSettingsProvider>(Reuse.Singleton);
        registrator.Register<IRationCatalogProvider, RationCatalogProvider>(Reuse.Singleton);
        registrator.Register<ProvenderService>(Reuse.Singleton);
        registrator.Register<IProvenderSaleAdapter, ProvenderSaleAdapter>(Reuse.Singleton);
        registrator.Register<ProvenderSaleService>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
