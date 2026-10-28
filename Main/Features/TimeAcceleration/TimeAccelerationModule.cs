using System.Collections.Generic;
using TAOM.Composition;

namespace TAOM.Features.TimeAcceleration;

/// <summary>
/// Time acceleration's one patch as a feature module (#1040, docs/features/time-acceleration.md "Turbo clicks").
/// The services stay registered in <see cref="TimeAccelerationIoC"/> and the tick stays in SubModule. Patch124
/// applies at ProcessLoad (OnSubModuleLoad), before the installed Coop's PatchAll patches MapScreen.HandleMouse
/// (the target's only caller) on a co-op join or host start, and before any later mod: Harmony marks a patched
/// method not inlinable, so a caller compiled after the mark calls the patch. ClientOnly MapScreen patches
/// already apply this early (Patch89). Every class in the category is [ClientOnly], so a dedicated server
/// indexes none. No behavior, model, setting or save data.
/// </summary>
internal sealed class TimeAccelerationModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch124_TurboClick";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.ProcessLoad),
    };

    public override string Id => "TimeAcceleration";

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
