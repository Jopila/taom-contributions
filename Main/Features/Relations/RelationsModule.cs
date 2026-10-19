using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Relations.Hooks;

namespace TAOM.Features.Relations;

/// <summary>
/// Relations (docs/features/relations.md, docs/features/personal-relations.md): the shared seam for new relation code
/// (<see cref="IRelationGrantService"/>, <see cref="IHeroRelationAdapter"/>) and Personal Relations (#862). This module owns
/// the seam registration, which Bonds of Battle used to carry. Never register a second <see cref="IRelationGrantService"/>
/// or <see cref="IHeroRelationAdapter"/>: a second unkeyed default makes every resolve throw (Main/IoC.cs).
/// <para>
/// <c>TaomDiplomacyModel</c> is still hand-wired in <c>SubModule.OnGameStart</c> and resolves
/// <see cref="IPersonalRelationService"/> there. That is safe because <c>OnSubModuleLoad</c> runs <c>IoC.Configure</c>, which
/// runs every module's <see cref="RegisterServices"/>, before any campaign starts (<c>RelationsWiringTests</c> pins the order).
/// </para>
/// <para>
/// The seeding behavior persists one flag in <c>SyncData</c>, so the module owns save data and fails closed. Patch107 targets
/// a SandBox behavior's private handler and applies at GameInit like the other campaign-menu patches.
/// </para>
/// </summary>
internal sealed class RelationsModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch107_PersonalRelationNotice";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new PersonalRelationSeedBehavior(
            r.Resolve<PersonalRelationSeedService>(),
            r.Resolve<IPersonalRelationService>(),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "Relations";

    /// <summary>The seeding behavior persists its flag in SyncData.</summary>
    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IHeroRelationAdapter, HeroRelationAdapter>(Reuse.Singleton);
        registrator.Register<IRelationGrantService, RelationGrantService>(Reuse.Singleton);
        registrator.Register<IPersonalRelationSettingsProvider, PersonalRelationSettingsProvider>(Reuse.Singleton);
        registrator.Register<IPersonalRelationService, PersonalRelationService>(Reuse.Singleton);
        registrator.Register<IPersonalRelationSeedAdapter, PersonalRelationSeedAdapter>(Reuse.Singleton);
        registrator.Register<IPersonalRelationNoticeAdapter, PersonalRelationNoticeAdapter>(Reuse.Singleton);
        registrator.Register<PersonalRelationSeedService>(Reuse.Singleton);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
