using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.SwornAnew.Hooks;
using TAOM.Features.SwornAnew.Models;

namespace TAOM.Features.SwornAnew;

/// <summary>
/// Sworn Anew (docs/features/sworn-anew.md, #869): a captured lord can be asked to join the player's kingdom, subject to an
/// alignment gate and the lore heroes. Three entry points share one service and one MCM switch: <c>TaomDefectionModel</c> on the
/// <c>DefectionModel</c> slot (campaign only), which opens the diplomacy-menu persuasion for a prisoner; a postfix on vanilla's
/// prisoner line, which applies the same verdict to it (Patch116); and a campaign behavior that commits the party
/// screen after the lord has joined. Nothing is saved, and the service holds no per-campaign state.
/// Overlap check, 2026-10-23: no other TAOM code fills the <c>DefectionModel</c> slot, patches <c>LordDefectionCampaignBehavior</c>
/// or listens to <c>OnClanChangedKingdomEvent</c> to commit a party screen (Namegiving, RealmBorders and three LotrIssues
/// templates listen to it for their own state). The shared pieces come from other registrations and are never registered here:
/// <c>IUncapturableRegistry</c> (<c>UncapturableHeroesIoC</c>), <c>IAlignmentService</c> (<c>ExecutionIoC</c>),
/// <c>ICoopSessionProvider</c> and <c>IDedicatedServerProvider</c> (the kernel and CoopInterop).
/// </summary>
internal sealed class SwornAnewModule : TaomFeatureModule
{
    // LordDefectionCampaignBehavior lives in TaleWorlds.CampaignSystem, which loads with the process, and PatchShield's second
    // pass wraps a patched method only from the process's first game start (Patch114 applies the same way).
    internal const string PatchCategory = "Patch116_SwornAnew";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.ProcessLoad),
    };

    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new SwornAnewBehavior(
            r.Resolve<SwornAnewService>(),
            r.Resolve<ISwornAnewAdapter>(),
            r.Resolve<IModLogger>())),
    };

    // The slot's generic argument is DefaultDefectionModel, not DefectionModel: the engine declares DefectionModel as
    // MBGameModel<DefaultDefectionModel>, and AddModel<T> chains the previous model of type T.
    private static readonly GameModelDecl[] Models =
    {
        GameModelDecl.Of<DefaultDefectionModel, TaomDefectionModel>(ModelTarget.Campaign,
            resolver => new TaomDefectionModel(resolver.Resolve<SwornAnewService>(), resolver.Resolve<ISwornAnewAdapter>())),
    };

    public override string Id => "SwornAnew";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ISwornAnewSettingsProvider, SwornAnewSettingsProvider>(Reuse.Singleton);
        registrator.Register<ISwornAnewAdapter, SwornAnewAdapter>(Reuse.Singleton);
        registrator.Register<SwornAnewService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        Patch116_PrisonerDefectionLine_Postfix.Initialize(
            resolver.Resolve<SwornAnewService>(), resolver.Resolve<ISwornAnewAdapter>(), resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;

    public override IReadOnlyList<GameModelDecl> GameModels => Models;
}
