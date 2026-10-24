using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanPartyItem;
using TaleWorlds.Localization;
using HarmonyLib;

namespace TAOM.Features.Refuge.Hooks;

/// <summary>
/// Postfix on <see cref="ClanPartyItemWithPartyVM"/>.<c>UpdateProperties</c> (#994): re-applies the
/// refuge row decoration after every property refresh. <see cref="RefugeClanScreenPatch"/> used to
/// decorate rows once, at list build, but <c>ClanPartiesVM.RefreshValues</c> runs each row's
/// <c>RefreshValues</c> afterwards, and <c>UpdateProperties</c> resets <c>Name</c> to the
/// plain party name and rebuilds the wage state. The row
/// constructor calls <c>RefreshValues</c> too, so a freshly built row is decorated here as well.
///
/// <para><c>UpdateProperties</c> has just rewritten <c>Name</c> when this runs, so appending the
/// "(being raised)" suffix once per call never stacks. The wage slider is hidden, because the
/// Garrison-typed row builds one whose figure is not charged for a refuge
/// (DefaultClanFinanceModel processes neither WarPartyComponents nor OwnedCaravans) and whose
/// SetWagePaymentLimit is a silent no-op on the base PartyComponent. Setting
/// `ShouldPartyHaveExpense = false` (bound at ClanPartiesRightPanel.xml:271 and :280) hides the wage
/// slider and shows the plain party-size line instead; v1.5.5 binds `PartyWageSubTitleText` nowhere.</para>
///
/// <para>Whole body try/catch: a clan-screen refresh must never die on a refuge row; the failure
/// mode is an undecorated row, which is the pre-patch state. Disjoint from the TroopWeight
/// postfix on the same method, which restates the party-size fields.</para>
/// </summary>
[HarmonyPatch(typeof(ClanPartyItemWithPartyVM), nameof(ClanPartyItemWithPartyVM.UpdateProperties))]
[HarmonyPatchCategory("Patch75_Refuge")]
[ClientOnly]
public static class RefugeClanRowRefreshPatch
{
    private static IRefugeBook? _refuges;

    /// <summary>Called once from RefugeIoC at container build time.</summary>
    public static void Initialize(IRefugeBook refuges)
    {
        _refuges = refuges;
    }

    [HarmonyPostfix]
    public static void Postfix(ClanPartyItemWithPartyVM __instance)
    {
        try
        {
            var refugeService = _refuges;
            if (refugeService == null || __instance == null)
                return;

            var partyId = __instance.Party?.MobileParty?.StringId;
            var refuge = partyId == null ? null : refugeService.GetByPartyId(partyId);
            if (refuge == null)
                return;

            SuppressWagePanel(__instance);
            if (refuge.Building)
            {
                __instance.Name = new TextObject("{=taom_rf_clan_row_building}{NAME} (being raised)")
                    .SetTextVariable("NAME", __instance.Name).ToString();
            }
        }
        catch
        {
            // Decoration is cosmetic; an undecorated row is the safe degradation, and a throw here
            // would take the whole clan-screen refresh down.
        }
    }

    private static void SuppressWagePanel(ClanPartyItemVM row)
    {
        row.ShouldPartyHaveExpense = false;
        row.ExpenseItem = null;
    }
}
