using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;

namespace TAOM.Features.HiddenHosts;

/// <summary>
/// Hidden Hosts as a feature module (#846, docs/features/hidden-hosts.md): another party's nameplate troop count is
/// blurred by its distance from the player. Patch105 is applied at GameInit: compiling the prefixes and the finalizer
/// runs no type initializer that needs a campaign (the four engine fields are bound by reflection, which needs only the
/// loaded SandBox assembly). The behavior is built with <c>new</c> inside the declaration's factory, which the runner
/// calls once per campaign start, so its constructor resets the singleton service for every campaign; it owns save data
/// (the one-time hint flag), so a fault fails closed.
/// </summary>
internal sealed class HiddenHostsModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch105_HiddenHosts";

    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new HiddenHostsBehavior(r.Resolve<HiddenHostsService>(),
            r.Resolve<IHiddenHostsHintAdapter>(), r.Resolve<IModLogger>())),
    };

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "HiddenHosts";

    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IHiddenHostsSettings, HiddenHostsSettingsProvider>(Reuse.Singleton);
        registrator.Register<IPartyIntelAdapter, PartyIntelAdapter>(Reuse.Singleton);
        registrator.Register<IHiddenHostsHintAdapter, HiddenHostsHintAdapter>(Reuse.Singleton);
        registrator.Register<HiddenHostsService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        HiddenHostsCalls.Initialize(resolver.Resolve<HiddenHostsService>(), resolver.Resolve<IPartyIntelAdapter>(),
            resolver.Resolve<IModLogger>());

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
