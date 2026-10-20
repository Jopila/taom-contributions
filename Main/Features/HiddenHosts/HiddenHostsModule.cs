using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.HiddenHosts;

/// <summary>
/// Hidden Hosts as a feature module (#846, docs/features/hidden-hosts.md): another party's nameplate troop count is
/// blurred by its distance from the player. Patch105 is applied at GameInit: compiling the prefixes and the finalizer
/// runs no type initializer that needs a campaign (the six engine fields, four on the nameplate view model and two on the
/// tooltip base, are bound by reflection, which needs only the loaded assemblies). Nameplates, hover tooltips and the
/// remembered counts are all this module's. The behaviors are built with <c>new</c> inside the declarations' factories,
/// which the runner calls once per campaign start, so their constructors reset the singleton service for every campaign;
/// the module owns save data (the two one-time hint flags and the remembered counts, which the memory behavior saves), so a
/// fault fails closed.
/// </summary>
internal sealed class HiddenHostsModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch105_HiddenHosts";

    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new HiddenHostsBehavior(r.Resolve<HiddenHostsService>(),
            r.Resolve<IHiddenHostsHintAdapter>(), r.Resolve<IModLogger>())),
        CampaignBehaviorDecl.Of(r => new HiddenHostsMemoryBehavior(r.Resolve<HiddenHostsService>(),
            r.Resolve<IHiddenHostsCampaignAdapter>(), r.Resolve<ICoopSessionProvider>(), r.Resolve<IModLogger>())),
        CampaignBehaviorDecl.Of(r => new HiddenHostsPlainSightBehavior(r.Resolve<HiddenHostsService>(),
            r.Resolve<IPartyIntelAdapter>(), r.Resolve<IModLogger>())),
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
        registrator.Register<IHiddenHostsCampaignAdapter, HiddenHostsCampaignAdapter>(Reuse.Singleton);
        registrator.Register<HiddenHostsService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        HiddenHostsCalls.Initialize(resolver.Resolve<HiddenHostsService>(), resolver.Resolve<IPartyIntelAdapter>(),
            resolver.Resolve<IModLogger>());

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
