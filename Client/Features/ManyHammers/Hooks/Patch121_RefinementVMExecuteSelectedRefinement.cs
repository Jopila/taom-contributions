using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Refinement;
using TAOM.Adapters;

namespace TAOM.Features.ManyHammers.Hooks;

/// <summary>
/// Prefix on <c>RefinementVM.ExecuteSelectedRefinement(Hero)</c> (v1.5.5 RefinementVM.cs:136), the smithy's Refine button.
/// With Shift or Ctrl held it runs the extra units of the selected formula through the engine's own <c>DoRefinement</c> and
/// lets vanilla run, so it refines the last unit and refreshes the formula list and the screen itself. A formula has no
/// stack, so Ctrl means as many runs as the materials and stamina allow. The parameter MUST be named
/// <c>currentCraftingHero</c> (Harmony binds by name; pinned by ManyHammersWiringTests).
/// </summary>
[HarmonyPatch(typeof(RefinementVM), nameof(RefinementVM.ExecuteSelectedRefinement), new[] { typeof(Hero) })]
[HarmonyPatchCategory(ManyHammersModule.PatchCategory)]
public static class Patch121_RefinementVMExecuteSelectedRefinement
{
    [HarmonyPrefix]
    public static void Prefix(RefinementVM __instance, Hero currentCraftingHero)
    {
        try
        {
            var service = ManyHammersCalls.Service;
            var formula = __instance.CurrentSelectedAction?.RefineFormula;
            if (formula == null || currentCraftingHero == null) return;

            var desired = service.DesiredCount(ManyHammersCalls.ShiftHeld, ManyHammersCalls.CtrlHeld, int.MaxValue, selectionLocked: false);
            if (desired <= 1) return;

            var behavior = Campaign.Current?.GetCampaignBehavior<ICraftingCampaignBehavior>();
            if (behavior == null) return;

            service.RunExtras(new RefineBenchUnit(currentCraftingHero, formula, behavior), desired);
        }
        catch (Exception ex)
        {
            ManyHammersCalls.Error("ExecuteSelectedRefinement", ex);
        }
    }
}
