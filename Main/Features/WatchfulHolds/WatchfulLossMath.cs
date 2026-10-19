using System;
using TAOM.Core.Validation;

namespace TAOM.Features.WatchfulHolds;

/// <summary>
/// The counting rules of Watchful Holds that need no engine: how many men a share of a party is, how a request is
/// clamped to the healthy regulars, where army cohesion lands after a hit, and how a food loss is spread over stacks. The service uses the first; the engine
/// adapter applies the others to real rosters and armies, so they are tested here.
/// </summary>
public static class WatchfulLossMath
{
    /// <summary>
    /// Added before flooring (and taken off before a ceiling) so single-precision dust does not move a count: 200 x 0.01f
    /// is 1.9999999 as a double and must count as 2. Far below the 0.01 granularity of any share a player can see.
    /// </summary>
    private const double RoundingSlack = 1e-4;

    /// <summary>floor(men x fraction x multiplier), zero for a non-finite or negative input.</summary>
    public static int CountFromFraction(int men, float fraction, float multiplier)
    {
        if (men <= 0 || !FiniteFloatValidator.IsFinite(fraction) || !FiniteFloatValidator.IsFinite(multiplier)
            || fraction <= 0f || multiplier <= 0f)
            return 0;
        double product = (double)men * fraction * multiplier + RoundingSlack;
        return product >= int.MaxValue ? int.MaxValue : (int)Math.Floor(product);
    }

    /// <summary>
    /// A request limited to [0, healthy]. Only <c>RemoveNumberOfNonHeroTroopsRandomly</c> asserts on more men than are
    /// healthy (and then clamps itself); <c>WoundNumberOfNonHeroTroopsRandomly</c> silently wounds fewer when no healthy
    /// regular is left. The clamp keeps the reported counts true. Both pick only non-hero troops.
    /// </summary>
    public static int ClampToHealthy(int requested, int healthy)
    {
        if (requested <= 0 || healthy <= 0)
            return 0;
        return Math.Min(requested, healthy);
    }

    /// <summary>
    /// Cohesion after a hit: <paramref name="current"/> minus <paramref name="loss"/>, but never below
    /// <paramref name="floor"/>, and a cohesion already at or below the floor is left as it is (a hit never raises it).
    /// A non-finite loss or floor, or a non-positive loss, changes nothing.
    /// </summary>
    public static float CohesionAfterHit(float current, float loss, float floor)
    {
        if (!FiniteFloatValidator.IsFinite(current) || !FiniteFloatValidator.IsFinite(loss)
            || !FiniteFloatValidator.IsFinite(floor) || loss <= 0f || current <= floor)
            return current;
        return Math.Max(floor, current - loss);
    }

    /// <summary>
    /// ceil(total x fraction) capped at the total and at least one unit; zero for a non-finite or non-positive fraction.
    /// The same single-precision slack as <see cref="CountFromFraction"/> comes off first, so 5% of 100 is 5 units and
    /// not 6 (0.05f is 0.0500000007).
    /// </summary>
    public static int FoodUnitsToRemove(int total, float fraction)
    {
        if (total <= 0 || !FiniteFloatValidator.IsFinite(fraction) || fraction <= 0f)
            return 0;
        double wanted = Math.Max(1.0, Math.Ceiling((double)total * fraction - RoundingSlack));
        return wanted >= total ? total : (int)wanted;
    }

    /// <summary>
    /// Spreads <see cref="FoodUnitsToRemove"/> over random stacks and returns the units to take from each, never more
    /// than a stack holds. <paramref name="nextInt"/> returns a value in [0, n); anything else is clamped, so a bad
    /// source cannot stall the loop. A negative stack counts as empty.
    /// </summary>
    public static int[] PlanFoodLoss(int[] stacks, float fraction, Func<int, int> nextInt)
    {
        var plan = new int[stacks.Length];
        long total = 0;
        foreach (var stack in stacks)
            total += Math.Max(0, stack);
        int left = FoodUnitsToRemove((int)Math.Min(total, int.MaxValue), fraction);

        while (left > 0)
        {
            int open = 0;
            for (int i = 0; i < stacks.Length; i++)
            {
                if (stacks[i] - plan[i] > 0)
                    open++;
            }
            if (open == 0)
                break;

            int pick = Clamp(nextInt(open), open);
            int index = -1;
            for (int i = 0; i < stacks.Length && pick >= 0; i++)
            {
                if (stacks[i] - plan[i] > 0 && pick-- == 0)
                    index = i;
            }

            int take = Math.Min(stacks[index] - plan[index], 1 + Clamp(nextInt(left), left));
            plan[index] += take;
            left -= take;
        }
        return plan;
    }

    private static int Clamp(int value, int count) => Math.Max(0, Math.Min(count - 1, value));
}
