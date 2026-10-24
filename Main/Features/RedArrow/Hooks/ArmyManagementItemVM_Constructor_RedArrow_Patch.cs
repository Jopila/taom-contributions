using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.ArmyManagement;

namespace TAOM.Features.RedArrow.Hooks;

/// <summary>
/// Patch115 (docs/reference/harmony-patch-registry.md "Patch115_RedArrow"), the transfer half. The
/// <c>ArmyManagementItemVM</c> constructor sets <c>IsTransferDisabled = IsMainHero || PlayerSiege.PlayerSiegeEvent != null</c>
/// (then ends with <c>RefreshValues()</c>), which switches off both the add and the remove button of every row during a siege.
/// When the feature applies, this postfix sets it to <c>IsMainHero</c>, so the player's own row stays fixed and every other
/// party can be added or released. This half defers to the eligibility patch's <c>Prepare</c>, which binds the private
/// <c>_eligibilityReason</c>, so a missing field never leaves the remove buttons on while the add buttons stay refused.
/// The two postfixes can still part if one target moves alone; <c>HarmonyPatchBindingTests</c> is the guard for that.
/// </summary>
[HarmonyPatch(typeof(ArmyManagementItemVM), MethodType.Constructor,
    new[] { typeof(Action<ArmyManagementItemVM>), typeof(Action<ArmyManagementItemVM>), typeof(Action<ArmyManagementItemVM>), typeof(MobileParty) })]
[HarmonyPatchCategory(RedArrowModule.PatchCategory)]
[ClientOnly]
public static class ArmyManagementItemVM_Constructor_RedArrow_Patch
{
    /// <summary>Applies only when the eligibility half does: it owns the one engine field both halves depend on.</summary>
    public static bool Prepare() => ArmyManagementItemVM_UpdateEligibility_RedArrow_Patch.Prepare();

    [HarmonyPostfix]
    public static void Postfix(ArmyManagementItemVM __instance)
    {
        var boundary = RedArrowCalls.Boundary;
        if (boundary == null || __instance == null)
            return;

        try
        {
            var disabled = boundary.TransferDisabled(__instance.IsMainHero);
            if (disabled.HasValue)
                __instance.IsTransferDisabled = disabled.Value;
        }
        catch (Exception ex)
        {
            RedArrowCalls.ReportOnce(nameof(ArmyManagementItemVM_Constructor_RedArrow_Patch) + ".Postfix",
                "Postfix failed, vanilla's transfer state stands: " + ex);
        }
    }
}
