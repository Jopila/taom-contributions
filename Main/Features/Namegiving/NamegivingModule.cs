using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.Namegiving.Hooks;

namespace TAOM.Features.Namegiving;

/// <summary>
/// Namegiving (#858, docs/features/namegiving.md): the realm's ruler renames its towns, castles and villages from the kingdom
/// Fiefs screen, and four fiefs take a lore name while the matching side holds them. The module registers the services and
/// builds the one behavior with <c>new</c> inside the declaration's factory, which the runner calls once per campaign start.
/// The mixin and the prefab insertion need no registration: UIExtenderEx finds them by assembly scan, and they resolve what
/// they need from the container when the kingdom screen opens. It saves nothing of its own (a custom name is saved by the
/// engine with the settlement), so <see cref="OwnsSaveData"/> is false and a fault never fails a campaign start. The saved names are
/// still a contract: a save holds the raw <c>{=taom_namegiving_lore_*}English</c> text, so never rename the key prefix, never remove a
/// lore row (see <see cref="LoreNames"/>) and never park this module, or a saved lore name is stranded.
/// </summary>
internal sealed class NamegivingModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new NamegivingBehavior(r.Resolve<NamegivingService>())),
    };

    public override string Id => "Namegiving";

    /// <summary>False: the engine saves the custom name with the settlement. The raw lore key text in a save is still a contract; never park this module.</summary>
    public override bool OwnsSaveData => false;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<NamegivingSettingsProvider>(Reuse.Singleton);
        registrator.Register<IFiefNamingAdapter, FiefNamingAdapter>(Reuse.Singleton);
        registrator.Register<NamegivingService>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
