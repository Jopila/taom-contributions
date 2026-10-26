using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting;
using TAOM.Adapters;

namespace TAOM.Features.ManyHammers.Hooks;

/// <summary>
/// Prefix on <c>SmeltingVM.TrySmeltingSelectedItems(Hero)</c> (v1.5.5 SmeltingVM.cs:229), the smithy's Smelt button. With
/// Shift or Ctrl held it runs the extra units through the engine's own <c>DoSmelting</c> and lets vanilla run, so it smelts
/// the last unit and refreshes the list and the screen itself. A locked selection, a count of one and a switched-off feature
/// pass through untouched. The parameter MUST be named <c>currentCraftingHero</c> (Harmony binds by name; pinned by
/// ManyHammersWiringTests).
/// </summary>
[HarmonyPatch(typeof(SmeltingVM), nameof(SmeltingVM.TrySmeltingSelectedItems), new[] { typeof(Hero) })]
[HarmonyPatchCategory(ManyHammersModule.PatchCategory)]
public static class Patch121_SmeltingVMTrySmeltingSelectedItems
{
    [HarmonyPrefix]
    public static void Prefix(SmeltingVM __instance, Hero currentCraftingHero)
    {
        try
        {
            var service = ManyHammersCalls.Service;
            var selected = __instance.CurrentSelectedItem;
            if (selected == null || currentCraftingHero == null) return;

            var desired = service.DesiredCount(ManyHammersCalls.ShiftHeld, ManyHammersCalls.CtrlHeld, selected.NumOfItems, selected.IsLocked);
            if (desired <= 1) return;

            var behavior = Campaign.Current?.GetCampaignBehavior<ICraftingCampaignBehavior>();
            if (behavior == null) return;

            service.RunExtras(new SmeltBenchUnit(currentCraftingHero, selected.EquipmentElement, behavior), desired);
        }
        catch (Exception ex)
        {
            ManyHammersCalls.Error("TrySmeltingSelectedItems", ex);
        }
    }
}
