using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;

namespace TAOM.Features.HiddenHosts.Hooks;

/// <summary>
/// Patch105 (docs/reference/harmony-patch-registry.md "Patch105_HiddenHosts", tooltips). Both private overloads of
/// <c>AddEncounterParties</c> (the siege tooltip's and the battle tooltip's) add the per-troop rows only when their
/// <c>isExtended</c> argument is true. While the frame is masked or at the horizon the prefix turns it off, so those rows
/// are not built; the two side totals still are, and the <c>GetPartySizeText</c> postfix masks them. The tooltip's own
/// "hold the key" line reads the view model's <c>IsExtended</c>, not this argument. Main thread.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(HiddenHostsModule.PatchCategory)]
[ClientOnly]
public static class TooltipRefresherCollection_AddEncounterParties_HiddenHosts_Patch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        typeof(TooltipRefresherCollection).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "AddEncounterParties");

    [HarmonyPrefix]
    public static void Prefix(ref bool isExtended)
    {
        var frame = HiddenHostsTooltipScope.Current;
        if (frame != null && frame.TrimsTroopRows && !HiddenHostsCalls.Stopped)
            isExtended = false;
    }
}
