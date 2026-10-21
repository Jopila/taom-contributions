using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Validation;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.Execution;

namespace TAOM.Features.FireAndTribute;

/// <summary>A living kingdom with at least one raidable village, as the target picker sees it.</summary>
internal readonly struct TargetCandidate
{
    public TargetCandidate(string id, FactionSide side, float strength)
    {
        Id = id;
        Side = side;
        Strength = strength;
    }

    public string Id { get; }

    public FactionSide Side { get; }

    /// <summary>The kingdom's total strength; a negative or non-finite reading counts as none.</summary>
    public float Strength { get; }
}

/// <summary>
/// The raiding hosts' pure arithmetic (#870): the target picker, the raidable-village rule and its selection, the host's
/// size, the raid quota, the spawn window and the cooldown. No state and no engine type; <see cref="FireAndTributeService"/>
/// decides when each applies.
/// </summary>
internal static class FireAndTributeMath
{
    /// <summary>
    /// The kingdom a host will raid, from <paramref name="candidates"/> (living, with a raidable village) of the host's
    /// <paramref name="targetSides"/>: the first of <paramref name="preferred"/> that qualifies, then the strongest, ties
    /// broken by ordinal id. Null when none qualifies.
    /// </summary>
    internal static string? PickTarget(
        IReadOnlyList<TargetCandidate> candidates, IReadOnlyList<FactionSide> targetSides, IReadOnlyList<string> preferred)
    {
        if (candidates == null || targetSides == null || candidates.Count == 0)
            return null;

        var pool = candidates.Where(c => targetSides.Contains(c.Side) && !string.IsNullOrEmpty(c.Id)).ToList();
        if (pool.Count == 0)
            return null;

        if (preferred != null)
        {
            foreach (var id in preferred)
            {
                if (pool.Any(c => c.Id == id))
                    return id;
            }
        }

        return pool
            .OrderByDescending(c => ReadableStrength(c.Strength))
            .ThenBy(c => c.Id, StringComparer.Ordinal)
            .First().Id;
    }

    private static float ReadableStrength(float strength) =>
        FiniteFloatValidator.IsFinite(strength) && strength > 0f ? strength : 0f;

    /// <summary>
    /// Whether a host of <paramref name="kingdomId"/>'s enemies may raid this village: it exists, is in its normal state
    /// (not being raided, looted or forced for supplies), belongs to that kingdom, and has a finite hearth of at least
    /// <paramref name="minHearth"/>.
    /// </summary>
    internal static bool IsRaidable(VillageReading village, string kingdomId, float minHearth) =>
        Raidable(village, minHearth) && village.KingdomId == kingdomId;

    private static bool Raidable(VillageReading village, float minHearth) =>
        village.Exists && village.IsNormal && village.KingdomId != null
        && FiniteFloatValidator.IsFinite(village.Hearth) && village.Hearth >= minHearth;

    /// <summary>The kingdoms that own at least one raidable village.</summary>
    internal static IReadOnlyCollection<string> KingdomsWithRaidable(IReadOnlyList<VillageReading> villages, float minHearth)
    {
        var kingdoms = new HashSet<string>(StringComparer.Ordinal);
        if (villages == null)
            return kingdoms;
        foreach (var village in villages)
        {
            if (Raidable(village, minHearth))
                kingdoms.Add(village.KingdomId!);
        }
        return kingdoms;
    }

    /// <summary>
    /// The ids of up to <paramref name="count"/> raidable villages of the kingdom nearest to the point, nearest first, ties
    /// broken by ordinal id. A village or query point whose distance is not a finite number is left out.
    /// </summary>
    internal static IReadOnlyList<string> NearestRaidable(
        IReadOnlyList<VillageReading> villages, string kingdomId, float x, float y, float minHearth, int count)
    {
        if (villages == null || count <= 0)
            return new List<string>();
        return villages
            .Where(v => IsRaidable(v, kingdomId, minHearth))
            .Select(v => (Id: v.Id, DistanceSquared: (v.X - x) * (v.X - x) + (v.Y - y) * (v.Y - y)))
            .Where(v => FiniteFloatValidator.IsFinite(v.DistanceSquared))
            .OrderBy(v => v.DistanceSquared)
            .ThenBy(v => v.Id, StringComparer.Ordinal)
            .Take(count)
            .Select(v => v.Id)
            .ToList();
    }

    /// <summary>
    /// The host's roster: <c>targetStrength * strengthShare</c> split over the troops by share (HuntMath.PlanTopUp),
    /// capped at <paramref name="maxTroops"/> men, and lifted to exactly <paramref name="minTroops"/> men by share when it
    /// falls short, so no stack is zero or negative. A troop whose power is unknown is left out; empty when none has a
    /// usable power or the bounds are not ordered.
    /// </summary>
    internal static IReadOnlyList<(string TroopId, int Count)> PlanHost(
        IReadOnlyList<HostTroop> troops, float targetStrength, float strengthShare,
        int minTroops, int maxTroops, Func<string, float> troopPower)
    {
        var none = new List<(string TroopId, int Count)>();
        if (troops == null || troopPower == null || minTroops < 1 || maxTroops < minTroops)
            return none;

        var usable = troops.Where(t => HasPower(troopPower(t.TroopId)) && t.Share > 0f && FiniteFloatValidator.IsFinite(t.Share)).ToList();
        if (usable.Count == 0)
            return none;

        float strength = ReadableStrength(targetStrength);
        float share = FiniteFloatValidator.IsFinite(strengthShare) && strengthShare > 0f ? strengthShare : 0f;
        float deficit = strength * share;
        var plan = FiniteFloatValidator.IsFinite(deficit) && deficit > 0f
            ? HuntMath.PlanTopUp(usable, deficit, troopPower, maxTroops)
            : none;

        return plan.Sum(p => p.Count) >= minTroops ? plan : FloorRoster(usable, minTroops);
    }

    private static bool HasPower(float power) => FiniteFloatValidator.IsFinite(power) && power > 0f;

    /// <summary>Exactly <paramref name="men"/> men split by share, the largest remainders taking the leftovers.</summary>
    private static IReadOnlyList<(string TroopId, int Count)> FloorRoster(List<HostTroop> troops, int men)
    {
        double total = troops.Sum(t => (double)t.Share);
        var exact = troops.Select(t => men * (double)t.Share / total).ToList();
        var counts = exact.Select(e => (int)Math.Floor(e)).ToList();
        int left = men - counts.Sum();
        foreach (int i in Enumerable.Range(0, troops.Count)
                     .OrderByDescending(i => exact[i] - counts[i]).ThenBy(i => i).Take(Math.Max(0, left)))
            counts[i]++;

        return troops.Select((t, i) => (t.TroopId, counts[i])).Where(p => p.Item2 > 0).ToList();
    }

    /// <summary>
    /// <c>clamp(round(targetStrength / strengthPerRaid) + extra, quotaMin, quotaMax)</c>. A target strength that is not a
    /// positive finite number counts as zero, so the quota is then the floor plus nothing.
    /// </summary>
    internal static int Quota(float targetStrength, float strengthPerRaid, int extra, int quotaMin, int quotaMax)
    {
        if (quotaMin < 1 || quotaMax < quotaMin)
            return Math.Max(1, quotaMin);

        double raids = 0d;
        if (FiniteFloatValidator.IsFinite(strengthPerRaid) && strengthPerRaid > 0f)
        {
            // Range-checked before the cast: (int)NaN and (int)huge are int.MinValue.
            double ratio = ReadableStrength(targetStrength) / (double)strengthPerRaid;
            raids = Math.Round(Math.Min(ratio, quotaMax), MidpointRounding.AwayFromZero);
        }
        long total = (long)raids + Math.Max(0, extra);
        return (int)Math.Max(quotaMin, Math.Min(quotaMax, total));
    }

    /// <summary>
    /// Whether a host may ride today. With the War of the Ring on: only in Full War or after it ended, from
    /// <paramref name="firstHostDelayDays"/> days after the later of the phase's effective day and the day this save first
    /// saw it (<paramref name="warSeenDay"/>, negative for never). With it off: from <paramref name="warlessStartDay"/>.
    /// </summary>
    internal static bool SpawnWindowOpen(
        bool warOfTheRingOn, bool fullWarOrEnded, int? fullWarEffectiveDay, int warSeenDay,
        int today, int firstHostDelayDays, int warlessStartDay)
    {
        if (!warOfTheRingOn)
            return today >= warlessStartDay;
        if (!fullWarOrEnded)
            return false;

        long anchor = Math.Max(fullWarEffectiveDay ?? 0, Math.Max(warSeenDay, 0));
        return today >= anchor + Math.Max(0, firstHostDelayDays);
    }

    /// <summary>The first day the side may ride again: <paramref name="today"/> plus the cooldown, never overflowing.</summary>
    internal static int CooldownEnd(int today, int cooldownDays) =>
        (int)Math.Min((long)today + Math.Max(0, cooldownDays), int.MaxValue);
}
