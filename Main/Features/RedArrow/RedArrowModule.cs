using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.RedArrow.Hooks;

namespace TAOM.Features.RedArrow;

/// <summary>
/// The Red Arrow (#873, docs/features/red-arrow.md): while the player leads an army that besieges, the Army Management screen
/// keeps working. The eligibility rule is a pure service. The army-management model is not touched: the two view-model
/// patches (Patch115, applied at GameInit) clear the transfer block and redo the eligibility check on the candidate's own
/// facts, with the siege blocks lifted, over vanilla's refusal. This module declares no model. Singleplayer only. No
/// behavior, no save data, nothing in <c>IoC.cs</c> or <c>SubModule.cs</c>.
/// </summary>
internal sealed class RedArrowModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch115_RedArrow";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "RedArrow";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IRedArrowSettingsProvider, RedArrowSettingsProvider>(Reuse.Singleton);
        registrator.Register<IPlayerSiegeStateAdapter, PlayerSiegeStateAdapter>(Reuse.Singleton);
        registrator.Register<IArmyCallCandidateAdapter, ArmyCallCandidateAdapter>(Reuse.Singleton);
        registrator.Register<RedArrowService>(Reuse.Singleton);
        registrator.Register<RedArrowArmyBoundary>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        RedArrowCalls.Initialize(resolver.Resolve<RedArrowArmyBoundary>(), resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
