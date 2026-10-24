using System;
using System.Collections.Generic;
using TAOM.Core.Validation;
using TAOM.Features.Provender.Models;

namespace TAOM.Features.Provender;

/// <summary>
/// The arithmetic of the Provender-master (docs/features/provender.md): how much of each food to hold, what a purchase and a
/// slaughter should be, the XP they earn and which ration the host is given. Pure functions over plain values; nothing here
/// reads the campaign, so every rule runs under test with no game.
/// </summary>
public static class ProvenderRules
{
    /// <summary>Steward XP for each head slaughtered.</summary>
    public const float SlaughterXpPerHead = 10f;

    /// <summary>Gold spent per point of Trade XP.</summary>
    public const float GoldPerTradeXp = 100f;

    /// <summary>Gold spent per point of Steward XP.</summary>
    public const float GoldPerStewardXp = 200f;

    /// <summary>
    /// The units of each food type to hold: the eaters divided by the food types the market has in stock, rounded up. Zero with
    /// no eaters or no stocked type, so a bare market or an empty party buys nothing.
    /// </summary>
    public static int FoodTargetPerType(int eaters, int stockedTypes)
    {
        if (eaters <= 0 || stockedTypes <= 0)
            return 0;
        return (int)(((long)eaters + stockedTypes - 1) / stockedTypes);
    }

    /// <summary>
    /// How many of one offered food to buy toward <paramref name="target"/>: the shortfall from what is held, capped by the
    /// stock and by what <paramref name="gold"/> covers at the offer's first price. Zero for no target, no gold, no stock or a
    /// price that is not positive. The service then cuts it to the market's exact rising total.
    /// </summary>
    public static int FoodToBuy(FoodOffer offer, int held, int target, int gold)
    {
        if (target <= 0 || gold <= 0 || offer.Stock <= 0 || offer.Price <= 0)
            return 0;
        long shortfall = (long)target - Math.Max(held, 0);
        return (int)Math.Max(0L, Math.Min(Math.Min(shortfall, offer.Stock), gold / offer.Price));
    }

    /// <summary>
    /// What to slaughter when the stores run low. <paramref name="edibleFood"/> is the food in the stores without the herd: the
    /// engine's own days of food count each live head's meat, so they never move when a head becomes meat. Nothing unless that
    /// food lasts fewer than <paramref name="lowDays"/> days at <paramref name="foodPerDay"/>; then one head at a time, always
    /// from the stack with the most to spare, until it lasts that long again. Each type keeps <paramref name="keepPerType"/>
    /// heads. A negative store is a hungry party and slaughters; a store or burn that is not a finite number, or a burn that is
    /// not positive, slaughters nothing.
    /// </summary>
    public static List<SlaughterLine> PlanSlaughter(
        IReadOnlyList<LivestockStack> stacks, float edibleFood, float foodPerDay, int lowDays, int keepPerType)
    {
        var plan = new List<SlaughterLine>();
        if (lowDays <= 0 || !FiniteFloatValidator.IsFinite(edibleFood))
            return plan;
        if (!FiniteFloatValidator.IsFinite(foodPerDay) || !(foodPerDay > 0f))
            return plan;

        // Food units still wanted. Meat is counted in whole units against it, so no float sum of fractional days can take an
        // extra head.
        float needed = lowDays * foodPerDay - edibleFood;
        if (!FiniteFloatValidator.IsFinite(needed) || !(needed > 0f))
            return plan;

        int keep = Math.Max(keepPerType, 0);
        var spare = new int[stacks.Count];
        var heads = new int[stacks.Count];
        for (int i = 0; i < stacks.Count; i++)
            spare[i] = stacks[i].MeatPerHead > 0 && stacks[i].Count > keep ? stacks[i].Count - keep : 0;

        long meat = 0;
        while (meat < needed)
        {
            int pick = -1;
            for (int i = 0; i < stacks.Count; i++)
            {
                if (spare[i] > 0 && (pick < 0 || spare[i] > spare[pick]))
                    pick = i;
            }
            if (pick < 0)
                break;
            spare[pick]--;
            heads[pick]++;
            meat += stacks[pick].MeatPerHead;
        }

        for (int i = 0; i < stacks.Count; i++)
        {
            if (heads[i] > 0)
                plan.Add(new SlaughterLine(stacks[i].ItemId, heads[i]));
        }
        return plan;
    }

    /// <summary>
    /// How many of <paramref name="unitPrices"/> (the next units in buying order, each price rise included) the
    /// <paramref name="budget"/> covers, taken in order. Every engine price is at least 1, so the affordable units are always a
    /// prefix and one pass finds it. The sum is kept in long, so no list overflows it.
    /// </summary>
    public static int AffordableCount(IReadOnlyList<int> unitPrices, int budget)
    {
        long total = 0;
        var count = 0;
        while (count < unitPrices.Count && total + unitPrices[count] <= budget)
            total += unitPrices[count++];
        return count;
    }

    /// <summary>The total of <paramref name="unitPrices"/>, or -1 past int range (the engine adds its charge in an int).</summary>
    public static int TotalCost(IReadOnlyList<int> unitPrices)
    {
        long total = 0;
        for (var i = 0; i < unitPrices.Count; i++)
            total += unitPrices[i];
        return total > int.MaxValue ? -1 : (int)total;
    }

    /// <summary>
    /// The gold the food order may spend: the purse less <paramref name="wageDays"/> days of the party's daily wage, never below
    /// zero. A negative wage or day count keeps nothing back.
    /// </summary>
    public static int FoodBudget(int gold, int dailyWage, int wageDays)
    {
        if (gold <= 0)
            return 0;
        long reserve = (long)Math.Max(dailyWage, 0) * Math.Max(wageDays, 0);
        return (int)Math.Max(gold - reserve, 0L);
    }

    public static float TradeXp(int goldSpent) => goldSpent > 0 ? goldSpent / GoldPerTradeXp : 0f;

    public static float StewardXp(int goldSpent) => goldSpent > 0 ? goldSpent / GoldPerStewardXp : 0f;

    public static float SlaughterXp(int heads) => heads > 0 ? heads * SlaughterXpPerHead : 0f;

    /// <summary>How many of the ration the troops need: the troops over the ratio, rounded up. Zero for no troops or a bad ratio.</summary>
    public static int RationItems(int troops, int troopsPerItem)
    {
        if (troops <= 0 || troopsPerItem <= 0)
            return 0;
        return (int)(((long)troops + troopsPerItem - 1) / troopsPerItem);
    }

    /// <summary>The race with the most troops, or null when none has any. A tie goes to the name that sorts first.</summary>
    public static string? MajorityRace(IReadOnlyDictionary<string, int> raceCounts)
    {
        string? best = null;
        var bestCount = 0;
        foreach (var pair in raceCounts)
        {
            if (pair.Value <= 0 || string.IsNullOrEmpty(pair.Key))
                continue;
            if (pair.Value > bestCount || (pair.Value == bestCount && string.CompareOrdinal(pair.Key, best) < 0))
            {
                best = pair.Key;
                bestCount = pair.Value;
            }
        }
        return best;
    }

    /// <summary>
    /// The ration the host is given: the first one the majority troop race prefers, else the first the leader's culture prefers,
    /// else the default one. Null when the catalog holds none of them.
    /// </summary>
    public static RationEntry? SelectRation(RationCatalog catalog, IReadOnlyDictionary<string, int> raceCounts, string? leaderCulture)
    {
        var race = MajorityRace(raceCounts);
        if (race != null)
        {
            foreach (var entry in catalog.Entries)
            {
                if (entry.PrefersRace(race))
                    return entry;
            }
        }
        foreach (var entry in catalog.Entries)
        {
            if (entry.PrefersCulture(leaderCulture))
                return entry;
        }
        foreach (var entry in catalog.Entries)
        {
            if (entry.IsDefault)
                return entry;
        }
        return null;
    }
}
