using System;
using System.Collections.Generic;
using TAOM.Core.Validation;

namespace TAOM.Features.EyeOfSauron;

/// <summary>
/// The Eye of Sauron hunt's pure arithmetic (#765 M8): score decay, deed weights, the host top-up plan, the food target
/// and the out-of-sight step along a path. No state and no engine type; <see cref="EyeOfSauronService"/> decides when
/// each applies.
/// </summary>
internal static class HuntMath
{
    private const double CeilingSlack = 1e-3;
    private const int FoodDays = 3;
    private const int MenPerFood = 20;

    /// <summary>
    /// <c>score * 0.5^(elapsed / halfLife)</c>. A non-finite score is zero; a negative or non-finite elapsed time, or a
    /// half-life that is not a positive finite number, leaves the score as it is.
    /// </summary>
    internal static float Decay(float score, double elapsedDays, float halfLifeDays)
    {
        if (!FiniteFloatValidator.IsFinite(score)) return 0f;
        if (!(elapsedDays > 0d) || !FiniteFloatValidator.IsFinite(elapsedDays)) return score;
        if (!(halfLifeDays > 0f) || !FiniteFloatValidator.IsFinite(halfLifeDays)) return score;
        return (float)(score * Math.Pow(0.5d, elapsedDays / halfLifeDays));
    }

    internal static float DeedWeight(DeedKind kind) => kind switch
    {
        DeedKind.Assault => 3f,
        DeedKind.SallyOrOutside => 2f,
        _ => 1f,
    };

    /// <summary>
    /// Splits a strength deficit over the host's troops: <c>ceil(deficit * share / power)</c> each, skipping a troop
    /// whose power is not a positive finite number, then scaled down to fit <paramref name="room"/> men in list order.
    /// </summary>
    internal static IReadOnlyList<(string TroopId, int Count)> PlanTopUp(
        IReadOnlyList<HostTroop> host, float deficit, Func<string, float> troopPower, int room)
    {
        var plan = new List<(string TroopId, int Count)>();
        if (host == null || room <= 0 || !(deficit > 0f) || !FiniteFloatValidator.IsFinite(deficit)) return plan;

        long total = 0;
        foreach (var troop in host)
        {
            float power = troopPower(troop.TroopId);
            if (!(power > 0f) || !FiniteFloatValidator.IsFinite(power)) continue;
            // The small slack keeps float noise (1.3f * 100 is 130.000005) from adding a whole extra man.
            double wanted = Math.Ceiling((double)deficit * troop.Share / power - CeilingSlack);
            if (!(wanted >= 1d)) continue;
            int count = wanted > room ? room : (int)wanted;
            plan.Add((troop.TroopId, count));
            total += count;
        }

        if (total <= room) return plan;

        var scaled = new List<(string TroopId, int Count)>(plan.Count);
        int left = room;
        foreach (var (troopId, count) in plan)
        {
            int share = (int)((long)count * room / total);
            scaled.Add((troopId, share));
            left -= share;
        }
        for (int i = 0; left > 0 && i < scaled.Count; i++, left--)
            scaled[i] = (scaled[i].TroopId, scaled[i].Count + 1);
        scaled.RemoveAll(p => p.Count <= 0);
        return scaled;
    }

    /// <summary>Three days of food for the host: <c>ceil(men / 20 * 3)</c>. Never the whole hunt's food: grain is heavy.</summary>
    internal static int FoodTarget(int headCount) =>
        headCount <= 0 ? 0 : (int)Math.Ceiling(headCount * (double)FoodDays / MenPerFood);

    /// <summary><c>min(pathGap - 1.5 S - 3, cap)</c>; zero unless that is a positive finite number.</summary>
    internal static float ComputeStep(float pathGap, float seeingRange, float stepCap)
    {
        if (!FiniteFloatValidator.IsFinite(pathGap) || !FiniteFloatValidator.IsFinite(seeingRange)
            || !FiniteFloatValidator.IsFinite(stepCap))
            return 0f;
        float step = Math.Min(pathGap - EyeOfSauronService.SightMargin * seeingRange - EyeOfSauronService.StepSlack, stepCap);
        return step > 0f ? step : 0f;
    }

    internal static float PathLength(float startX, float startY, float[] xs, float[] ys)
    {
        int n = Math.Min(xs?.Length ?? 0, ys?.Length ?? 0);
        float total = 0f, px = startX, py = startY;
        for (int i = 0; i < n; i++)
        {
            total += Distance(px, py, xs![i], ys![i]);
            px = xs[i];
            py = ys[i];
        }
        return total;
    }

    /// <summary>The point <paramref name="distance"/> along the path; the last point when the path is shorter.</summary>
    internal static (float X, float Y) WalkPath(float startX, float startY, float[] xs, float[] ys, float distance)
    {
        int n = Math.Min(xs?.Length ?? 0, ys?.Length ?? 0);
        float px = startX, py = startY, left = distance;
        for (int i = 0; i < n; i++)
        {
            float leg = Distance(px, py, xs![i], ys![i]);
            if (leg >= left && leg > 0f)
            {
                float t = left / leg;
                return (px + (xs[i] - px) * t, py + (ys[i] - py) * t);
            }
            left -= leg;
            px = xs[i];
            py = ys[i];
        }
        return (px, py);
    }

    internal static float Distance(float ax, float ay, float bx, float by)
    {
        float dx = bx - ax, dy = by - ay;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }
}
