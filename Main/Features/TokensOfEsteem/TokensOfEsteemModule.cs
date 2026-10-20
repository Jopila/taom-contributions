using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.TokensOfEsteem.Hooks;

namespace TAOM.Features.TokensOfEsteem;

/// <summary>
/// Tokens of Esteem (docs/features/tokens-of-esteem.md, #863) as a feature module: a service graph and one campaign behavior,
/// no patch and no model. The behavior is built with <c>new</c> for every campaign because it owns the saved cooldown ledger
/// (a container singleton is constructed once per process), and it persists that ledger in <c>SyncData</c>, so the module owns
/// save data and fails closed. The shared pieces come from other registrations and are never registered here:
/// <c>IRelationGrantService</c> (<c>RelationsModule</c>), <c>IAlignmentService</c> (<c>ExecutionIoC</c>),
/// <c>IPartyItemRosterAdapter</c> (<c>EnlistmentIoC</c>), <c>IRaceManager</c> and <c>ICoopSessionProvider</c> (the kernel).
/// </summary>
internal sealed class TokensOfEsteemModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new GiftBehavior(
            r.Resolve<GiftService>(),
            r.Resolve<IGiftConversationAdapter>(),
            r.Resolve<ICoopSessionProvider>(),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "TokensOfEsteem";

    /// <summary>The cooldown ledger is saved in SyncData.</summary>
    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IGiftSettingsProvider, GiftSettingsProvider>(Reuse.Singleton);
        registrator.Register<IGiftCatalogProvider, GiftCatalogProvider>(Reuse.Singleton);
        registrator.Register<IGiftHeroAdapter, GiftHeroAdapter>(Reuse.Singleton);
        registrator.Register<IGiftItemAdapter, GiftItemAdapter>(Reuse.Singleton);
        registrator.Register<IGiftConversationAdapter, GiftConversationAdapter>(Reuse.Singleton);
        registrator.Register<GiftService>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
