using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.ArmyManagement;
using TaleWorlds.Localization;

namespace TAOM.Features.RedArrow.Hooks;

/// <summary>
/// Patch115 (docs/reference/harmony-patch-registry.md "Patch115_RedArrow"), the eligibility half. Vanilla's
/// <c>ArmyManagementItemVM.UpdateEligibility</c> asks the army-management model about the party and then, if that passed,
/// asks <c>CampaignUIHelper.GetMapScreenActionIsEnabledWithReason</c>; the model refuses every party during a siege and the
/// helper every action. This postfix redoes the pair on the candidate's own facts with the siege blocks lifted
/// (<see cref="RedArrowArmyBoundary.TryRecheck"/>) and writes the result over vanilla's: the private
/// <c>_eligibilityReason</c> through a field reference and the public <c>IsEligible</c>. It runs only when the feature
/// applies and vanilla's own skip branch (a party that can rejoin free of charge, or one already added this session) did not
/// decide the row. Any fault, or a field the engine no longer has, leaves vanilla's refusal in place.
/// </summary>
[HarmonyPatch(typeof(ArmyManagementItemVM), nameof(ArmyManagementItemVM.UpdateEligibility))]
[HarmonyPatchCategory(RedArrowModule.PatchCategory)]
[ClientOnly]
public static class ArmyManagementItemVM_UpdateEligibility_RedArrow_Patch
{
    private static AccessTools.FieldRef<ArmyManagementItemVM, TextObject>? _reason;

    /// <summary>Resolves the private text field once. A missing field logs and skips the patch, so vanilla's block stays.</summary>
    public static bool Prepare()
    {
        try
        {
            _reason = AccessTools.FieldRefAccess<ArmyManagementItemVM, TextObject>("_eligibilityReason");
        }
        catch (Exception ex)
        {
            _reason = null;
            RedArrowCalls.ReportOnce(nameof(ArmyManagementItemVM_UpdateEligibility_RedArrow_Patch),
                "ArmyManagementItemVM._eligibilityReason not found, the Red Arrow eligibility patch is not applied: " + ex.Message);
        }

        return _reason != null;
    }

    [HarmonyPostfix]
    public static void Postfix(ArmyManagementItemVM __instance)
    {
        var boundary = RedArrowCalls.Boundary;
        var reasonField = _reason;
        if (boundary == null || reasonField == null || __instance == null)
            return;
        // Vanilla's skip branch in UpdateEligibility: neither check ran for this row, so there is nothing to redo.
        if (__instance.CanJoinBackWithoutCost || (__instance.IsInCart && !__instance.IsAlreadyWithPlayer))
            return;

        try
        {
            if (!boundary.TryRecheck(__instance.Party, out var eligible, out var text))
                return;
            reasonField(__instance) = text;
            __instance.IsEligible = eligible;
        }
        catch (Exception ex)
        {
            RedArrowCalls.ReportOnce(nameof(ArmyManagementItemVM_UpdateEligibility_RedArrow_Patch) + ".Postfix",
                "Postfix failed, vanilla's eligibility stands: " + ex);
        }
    }
}
