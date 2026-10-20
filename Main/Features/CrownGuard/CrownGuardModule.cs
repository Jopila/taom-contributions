using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;

namespace TAOM.Features.CrownGuard;

/// <summary>
/// Crown Guard (#825, docs/features/crown-guard.md): the two guards beside a new ruler in the become-king scene are
/// troops picked for the kingdom's culture, in its colours, instead of vanilla's Sturgian fighter or a Calradian elite,
/// through one postfix. No model, behavior, setting or save data.
/// </summary>
internal sealed class CrownGuardModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch112_CrownGuard";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "CrownGuard";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<CrownGuardConfigProvider>(Reuse.Singleton);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
