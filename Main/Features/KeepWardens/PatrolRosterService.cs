namespace TAOM.Features.KeepWardens;

/// <summary>
/// Fits a patrol's initial roster to its size limit (#991, docs/features/keep-wardens.md). Vanilla fills every template stack to
/// its maximum, so a TAOM patrol template can total more or fewer men than the Guard House level allows, and a patrol below
/// 90 percent of its limit never leaves home. Pure: stack counts in, stack counts out, no TaleWorlds type.
/// </summary>
public sealed class PatrolRosterService
{
    /// <summary>
    /// Scales each stack by <c>limit / total</c> (rounded down), then hands the remainder out one man at a time in stack order,
    /// so the result sums to <paramref name="limit"/> and keeps the template's proportions. A limit of 0 or less, an empty
    /// roster, no men at all, or a roster already at the limit returns <paramref name="counts"/> itself, unchanged.
    /// </summary>
    public int[] Fit(int[] counts, int limit)
    {
        long total = 0;
        foreach (var count in counts)
            total += count > 0 ? count : 0;
        if (limit <= 0 || total <= 0 || total == limit)
            return counts;

        var fitted = new int[counts.Length];
        var assigned = 0;
        for (var i = 0; i < counts.Length; i++)
        {
            fitted[i] = counts[i] > 0 ? (int)(counts[i] * (long)limit / total) : 0;
            assigned += fitted[i];
        }

        // Each floor loses less than one man, so the shortfall is smaller than the number of stacks holding men: one lap covers it.
        for (var i = 0; assigned < limit; i = (i + 1) % counts.Length)
        {
            if (counts[i] <= 0)
                continue;
            fitted[i]++;
            assigned++;
        }

        return fitted;
    }
}
