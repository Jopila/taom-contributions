using System;
using System.Collections.Generic;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Equipment;

namespace TAOM.Features.Enlistment.Content;

/// <summary>The rank an oath restores, and the kit tier the matched contract left with the player (null: none).</summary>
public readonly struct RestoredService
{
    public RestoredService(ServiceRank rank, EnlistmentKitTier? kitTier)
    {
        Rank = rank;
        KitTier = kitTier;
    }

    public ServiceRank Rank { get; }
    public EnlistmentKitTier? KitTier { get; }

    /// <summary>True when the oath starts above Private.</summary>
    public bool Restored => Rank > ServiceRank.Private;
}

/// <summary>
/// What a returning soldier is remembered for (#837). Pure. The best match wins: the same commander's old
/// rank minus one, the same clan's minus one, the same kingdom's minus two, never below Private. A
/// desertion gives no credit. Expired and future-dated entries, and another hero's, give nothing.
/// </summary>
public static class ReenlistmentPolicy
{
    public static RestoredService Restore(
        IReadOnlyList<ServiceHistoryEntry> history, double nowDays, int expiryDays,
        string commanderId, string clanId, string kingdomId, string heroId)
    {
        var best = 0;
        EnlistmentKitTier? kit = null;
        var bestDay = double.MinValue;
        if (history != null)
        {
            foreach (var e in history)
            {
                if (!Same(e.HeroId, heroId) || e.Reason == DischargeReason.Desertion || e.EndDay > nowDays || nowDays - e.EndDay > expiryDays)
                    continue;
                var credit = -1;
                if (Same(e.CommanderHeroId, commanderId))
                    credit = (int)e.Rank - 1;
                if (Same(e.ClanId, clanId))
                    credit = Math.Max(credit, (int)e.Rank - 1);
                if (Same(e.KingdomId, kingdomId))
                    credit = Math.Max(credit, (int)e.Rank - 2);
                credit = Math.Max(0, credit);
                if (credit > best || (credit == best && credit > 0 && e.EndDay > bestDay))
                {
                    best = credit;
                    kit = e.KitTier;
                    bestDay = e.EndDay;
                }
            }
        }
        return new RestoredService((ServiceRank)best, best > 0 ? kit : null);
    }

    /// <summary>This hero deserted from this commander less than <paramref name="cooldownDays"/> ago. 0 days is off.</summary>
    public static bool InDesertionCooldown(
        IReadOnlyList<ServiceHistoryEntry> history, string commanderId, double nowDays, int cooldownDays, string heroId)
    {
        if (history == null || cooldownDays <= 0)
            return false;
        foreach (var e in history)
        {
            if (Same(e.HeroId, heroId) && Same(e.CommanderHeroId, commanderId) && e.IsCoolingDownDesertion(nowDays, cooldownDays))
                return true;
        }
        return false;
    }

    private static bool Same(string a, string b) =>
        !string.IsNullOrEmpty(a) && string.Equals(a, b, StringComparison.Ordinal);
}
