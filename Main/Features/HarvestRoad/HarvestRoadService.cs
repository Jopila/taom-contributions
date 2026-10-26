using System.Collections.Generic;

namespace TAOM.Features.HarvestRoad;

/// <summary>
/// The gates and the sign filter for Harvest Road (#912). The patches hand it facts, never engine types. The map's
/// notice picks "sold" or "bought" from the sign of the FIRST line it is given, so every list raised carries one sign:
/// <see cref="Sold{TKey}"/> and <see cref="Bought{TKey}"/> drop the other sign, and an empty result raises nothing.
/// </summary>
internal sealed class HarvestRoadService
{
    private readonly IHarvestRoadSettingsProvider _settings;

    public HarvestRoadService(IHarvestRoadSettingsProvider settings) => _settings = settings;

    /// <summary>True for an AI party standing in a settlement that has a town component, with the toggle on. Vanilla reaches the villager seam only in towns and the food seam in towns and villages, never a castle. A village has no Town to raise the notice for, so on the food seam this gate drops village purchases.</summary>
    internal bool ShouldWatch(bool isMainParty, bool hasTown) => _settings.Enabled && !isMainParty && hasTown;

    /// <summary>
    /// <see cref="ShouldWatch"/> and a real purchase. Vanilla asks for food every hour with an amount of 0 for a party
    /// that is stocked, and a poisoned count (int.MinValue after a bad float cast) is not a purchase either.
    /// </summary>
    internal bool ShouldWatchFoodPurchase(bool isMainParty, bool hasTown, int amount) =>
        amount > 0 && ShouldWatch(isMainParty, hasTown);

    /// <summary>The lines that left the roster (positive counts), in order.</summary>
    internal List<(TKey Key, int Count)> Sold<TKey>(IEnumerable<(TKey Key, int Count)>? diff) => Keep(diff, sold: true);

    /// <summary>The lines that arrived (negative counts), in order.</summary>
    internal List<(TKey Key, int Count)> Bought<TKey>(IEnumerable<(TKey Key, int Count)>? diff) => Keep(diff, sold: false);

    private static List<(TKey Key, int Count)> Keep<TKey>(IEnumerable<(TKey Key, int Count)>? diff, bool sold)
    {
        var kept = new List<(TKey Key, int Count)>();
        if (diff == null)
            return kept;

        foreach (var line in diff)
        {
            if (sold ? line.Count > 0 : line.Count < 0)
                kept.Add(line);
        }

        return kept;
    }
}
