using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Core.Logging;

namespace TAOM.Features.HarvestRoad.Hooks;

/// <summary>
/// Patch120, #912: the trade pop-ups over a town's nameplate that vanilla raises only for caravans, raised for a villager's
/// delivery and for an AI party's food purchase too. Each patch keeps the party's item roster in <c>__state</c> before the
/// vanilla call and diffs it after, so the figures are what the trade moved. A body never throws into a trade: a fault leaves
/// the trade untouched and is logged once. Callers, thread and co-op notes: docs/reference/harmony-patch-registry.md
/// "Patch120_HarvestRoad".
/// </summary>
public static class HarvestRoadPatches
{
    private static readonly HashSet<string> Reported = new HashSet<string>();
    private static IModLogger? _logger;

    internal static HarvestRoadService? Service { get; private set; }

    internal static void Initialize(HarvestRoadService service, IModLogger logger)
    {
        Service = service;
        _logger = logger;
        Reported.Clear();
    }

    /// <summary>Logs <paramref name="message"/> at warning level the first time <paramref name="key"/> is seen.</summary>
    internal static void ReportOnce(string key, string message)
    {
        if (Reported.Add(key))
            _logger?.LogWarning("[HarvestRoad] " + message);
    }

    internal static List<(EquipmentElement, int)> SnapshotItems(ItemRoster roster)
    {
        var lines = new List<(EquipmentElement, int)>(roster.Count);
        for (var i = 0; i < roster.Count; i++)
        {
            var element = roster.GetElementCopyAtIndex(i);
            lines.Add((element.EquipmentElement, element.Amount));
        }

        return lines;
    }

    internal static void RaiseTradeNotice(MobileParty party, Town town, List<(EquipmentElement, int)> lines) =>
        CampaignEventDispatcher.Instance.OnCaravanTransactionCompleted(party, town, lines);

    /// <summary>A villager's delivery into a town: the lines that left the villager's roster, positive.</summary>
    [HarmonyPatch(typeof(SellGoodsForTradeAction), nameof(SellGoodsForTradeAction.ApplyByVillagerTrade), new[] { typeof(Settlement), typeof(MobileParty) })]
    [HarmonyPatchCategory(HarvestRoadModule.PatchCategory)]
    public static class VillagerSalePatch
    {
        [HarmonyPrefix]
        public static void Prefix(Settlement settlement, MobileParty villagerParty, out List<(EquipmentElement, int)>? __state)
        {
            __state = null;
            try
            {
                if (Service?.ShouldWatch(villagerParty.IsMainParty, settlement.Town != null) == true)
                    __state = SnapshotItems(villagerParty.ItemRoster);
            }
            catch (Exception ex)
            {
                __state = null;
                ReportOnce("sale-prefix", "villager delivery snapshot failed, no notice: " + ex.Message);
            }
        }

        [HarmonyPostfix]
        public static void Postfix(Settlement settlement, MobileParty villagerParty, List<(EquipmentElement, int)>? __state)
        {
            if (__state == null)
                return;
            try
            {
                var lines = Service?.Sold(TradeNoticeDiff.Compute(__state, SnapshotItems(villagerParty.ItemRoster)));
                if (lines != null && lines.Count > 0)
                    RaiseTradeNotice(villagerParty, settlement.Town, lines);
            }
            catch (Exception ex)
            {
                ReportOnce("sale-postfix", "villager delivery notice failed, trade unaffected: " + ex.Message);
            }
        }
    }

    /// <summary>An AI party buying food in a town: the lines that arrived in its roster, negative.</summary>
    [HarmonyPatch(typeof(PartiesBuyFoodCampaignBehavior), "BuyFoodInternal", new[] { typeof(MobileParty), typeof(Settlement), typeof(int) })]
    [HarmonyPatchCategory(HarvestRoadModule.PatchCategory)]
    public static class FoodPurchasePatch
    {
        [HarmonyPrefix]
        public static void Prefix(MobileParty mobileParty, Settlement settlement, int numberOfFoodItemsNeededToBuy, out List<(EquipmentElement, int)>? __state)
        {
            __state = null;
            try
            {
                if (Service?.ShouldWatchFoodPurchase(mobileParty.IsMainParty, settlement.Town != null, numberOfFoodItemsNeededToBuy) == true)
                    __state = SnapshotItems(mobileParty.ItemRoster);
            }
            catch (Exception ex)
            {
                __state = null;
                ReportOnce("food-prefix", "food purchase snapshot failed, no notice: " + ex.Message);
            }
        }

        [HarmonyPostfix]
        public static void Postfix(MobileParty mobileParty, Settlement settlement, List<(EquipmentElement, int)>? __state)
        {
            if (__state == null)
                return;
            try
            {
                var lines = Service?.Bought(TradeNoticeDiff.Compute(__state, SnapshotItems(mobileParty.ItemRoster)));
                if (lines != null && lines.Count > 0)
                    RaiseTradeNotice(mobileParty, settlement.Town, lines);
            }
            catch (Exception ex)
            {
                ReportOnce("food-postfix", "food purchase notice failed, purchase unaffected: " + ex.Message);
            }
        }
    }
}
