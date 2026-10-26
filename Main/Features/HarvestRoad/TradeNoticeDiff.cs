using System.Collections.Generic;

namespace TAOM.Features.HarvestRoad;

/// <summary>
/// The roster diff behind Harvest Road (#912). It knows no engine type: the patches key it by <c>EquipmentElement</c>,
/// so two modifiers of one item stay two lines. A positive count left the roster (sold), a negative count arrived
/// (bought), which is the sign the map's trade notice reads to say "sold" or "bought".
/// </summary>
internal static class TradeNoticeDiff
{
    /// <summary>
    /// <c>before - after</c> per key. Duplicate keys are summed on each side, a line that nets to zero is dropped, and
    /// the order is stable: keys in the order <paramref name="before"/> lists them, then keys only
    /// <paramref name="after"/> lists. Either side null gives an empty result, never a guess. Sums are taken in
    /// <c>long</c> and a delta past the int range is clamped, never wrapped.
    /// </summary>
    internal static List<(TKey Key, int Count)> Compute<TKey>(
        IEnumerable<(TKey Key, int Count)>? before,
        IEnumerable<(TKey Key, int Count)>? after)
        where TKey : notnull
    {
        var result = new List<(TKey Key, int Count)>();
        if (before == null || after == null)
            return result;

        var order = new List<TKey>();
        var delta = new Dictionary<TKey, long>();
        foreach (var (key, count) in before)
            Add(delta, order, key, count);
        foreach (var (key, count) in after)
            Add(delta, order, key, -(long)count);

        foreach (var key in order)
        {
            var value = delta[key];
            if (value == 0)
                continue;
            result.Add((key, (int)System.Math.Max(-int.MaxValue, System.Math.Min(int.MaxValue, value))));
        }

        return result;
    }

    private static void Add<TKey>(Dictionary<TKey, long> delta, List<TKey> order, TKey key, long amount)
        where TKey : notnull
    {
        if (delta.TryGetValue(key, out var current))
        {
            delta[key] = current + amount;
            return;
        }

        order.Add(key);
        delta[key] = amount;
    }
}
