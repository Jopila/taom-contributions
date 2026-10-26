using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.BanditVoices.Hooks;

namespace TAOM.Features.BanditVoices;

/// <summary>
/// Bandit Voices (#900, docs/features/bandit-voices.md): the eight talking bandit clans threaten a robbed player in their
/// own words (data only: game-text variations vanilla already looks up), and on a first meeting open with their own
/// greeting a quarter of the time (one postfix, Patch122, applied at GameInit). No behavior, model or save data, nothing
/// in <c>IoC.cs</c> or <c>SubModule.cs</c>.
/// </summary>
internal sealed class BanditVoicesModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch122_BanditVoices";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "BanditVoices";

    public override void RegisterServices(IRegistrator registrator) => BanditVoicesIoC.RegisterBanditVoicesFeature(registrator);

    public override void InitializeStatics(IResolver resolver) =>
        BanditGreetingPatch.Initialize(resolver.Resolve<BanditVoicesService>(), resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
