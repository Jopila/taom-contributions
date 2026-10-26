using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;

namespace TAOM.Features.ManyHammers;

/// <summary>
/// Many Hammers (docs/features/many-hammers.md, #911): in the smithy, Shift smelts or refines five at once and Ctrl a whole
/// stack, and weapons the player locked in the inventory leave the smelting list. A pure service over adapters that repeat the
/// engine's own crafting behavior, plus three Harmony patches (Patch121, applied at GameInit) that live in TAOM.Client
/// because the smithy's view models are client-only. The patches resolve <see cref="ManyHammersService"/> lazily, so the
/// module has nothing to hand them. It declares no model (the cost of a unit comes from whichever smithing model is installed),
/// no behavior and no save data; nothing in <c>IoC.cs</c> or <c>SubModule.cs</c>.
/// </summary>
internal sealed class ManyHammersModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch121_ManyHammers";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "ManyHammers";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IManyHammersSettingsProvider, ManyHammersSettingsProvider>(Reuse.Singleton);
        registrator.Register<ManyHammersService>(Reuse.Singleton);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
