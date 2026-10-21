using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;

namespace TAOM.Features.CoopInterop.Hooks;

/// <summary>
/// Patch113, #966 T3: brackets BannerlordCoop's server-side
/// <c>GameInterface.Services.TroopRosters.Interfaces.TroopRosterInterface.HandleOnRecruitmentDone(string, TroopInfo[])</c>
/// (internal class, bound by reflection) so <see cref="RecruitAttributionScope.Current"/> names the
/// recruiting party while it raises <c>OnUnitRecruited</c>. The prefix saves the previous value into
/// Harmony's <c>__state</c>; the void finalizer restores it on every exit, a throw included. PatchShield
/// is never installed while a co-op module is active, so the prefix is not stripped in practice; if it
/// were, <c>__state</c> would be null and the finalizer would clear the scope. Applied only when Coop's
/// type is present. Notes:
/// docs/reference/harmony-patch-registry.md "Patch113_CoopRecruitAttribution".
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(PatchCategory)]
public static class CoopRecruitAttributionPatch
{
    internal const string PatchCategory = "Patch113_CoopRecruitAttribution";
    private const string TargetTypeName = "GameInterface.Services.TroopRosters.Interfaces.TroopRosterInterface";

    [HarmonyPrepare]
    public static bool Prepare()
    {
        if (TargetMethod() != null) return true;

        // Co-op is loaded but the method moved: say so once, since the patch then silently never applies.
        if (CoopPresence.IsActive)
        {
            try
            {
                IoC.Resolve<IModLogger>().LogWarning(
                    $"[CoopRecruitAttribution] Patch113 not applied: '{TargetTypeName}.HandleOnRecruitmentDone' " +
                    "was not found; if this is BannerlordCoop, its API may have changed. A guest's recruit_cost will not be attributed.");
            }
            catch { /* logging must never fail patch preparation */ }
        }
        return false;
    }

    // Coop keys a party "MobileParty_" + StringId (AutoRegistryBase), and the client sends that key.
    private const string CoopPartyKeyPrefix = "MobileParty_";

    internal static string? ToPartyStringId(string? coopKey)
        => coopKey != null && coopKey.StartsWith(CoopPartyKeyPrefix, System.StringComparison.Ordinal)
            ? coopKey.Substring(CoopPartyKeyPrefix.Length)
            : coopKey;

    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod()
        => ReflectionUtils.FindTypeAcrossLoadedAssemblies(TargetTypeName)?.GetMethod(
            "HandleOnRecruitmentDone",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPrefix]
    public static void Prefix(string __0, out string? __state)
    {
        __state = RecruitAttributionScope.Current;
        RecruitAttributionScope.Current = ToPartyStringId(__0);
    }

    [HarmonyFinalizer]
    public static void Finalizer(string? __state)
    {
        RecruitAttributionScope.Current = __state;
    }
}
