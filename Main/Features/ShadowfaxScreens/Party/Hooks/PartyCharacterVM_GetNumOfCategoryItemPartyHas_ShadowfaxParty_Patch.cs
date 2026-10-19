using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;

namespace TAOM.Features.ShadowfaxScreens.Party.Hooks;

/// <summary>
/// The per-window category-count cache. Vanilla walks the whole item roster for every row (a null category too); inside
/// InitializeTroopLists or RefreshTroopsUpgradeable the roster cannot change, so the count is the same for one
/// (roster, category) pair. A hit sets __result and skips vanilla; a miss runs it and the postfix stores. Outside a
/// window the service misses and ignores the store. The pair links through __state.
/// </summary>
[HarmonyPatch(typeof(PartyCharacterVM), nameof(PartyCharacterVM.GetNumOfCategoryItemPartyHas),
    new[] { typeof(ItemRoster), typeof(ItemCategory) })]
[HarmonyPatchCategory("PatchXXX_ShadowfaxParty")]
public static class PartyCharacterVM_GetNumOfCategoryItemPartyHas_ShadowfaxParty_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(ItemRoster items, ItemCategory itemCategory, ref int __result, out bool __state)
    {
        __state = false;
        try
        {
            var service = PartyFastOpenCalls.Service;
            if (service == null || items == null) return true;
            if (service.TryGetCount(items, itemCategory, out var cached))
            {
                __result = cached;
                return false;
            }
            __state = true;
        }
        catch
        {
            // Vanilla counts.
        }
        return true;
    }

    [HarmonyPostfix]
    public static void Postfix(ItemRoster items, ItemCategory itemCategory, int __result, bool __state)
    {
        if (!__state) return;
        try
        {
            PartyFastOpenCalls.Service?.StoreCount(items, itemCategory, __result);
        }
        catch
        {
            // The caller keeps vanilla's count.
        }
    }
}
