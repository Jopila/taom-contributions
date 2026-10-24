using System;
using TAOM.Core.Validation;

namespace TAOM.Features.LoremastersLine;

/// <summary>
/// Where a skill's learning rate stops falling (#877). <see cref="Level"/> is the lowest skill whose rate is within
/// <see cref="LearningFloorCalculator.Tolerance"/> of <see cref="FloorRate"/>; <see cref="FullRate"/> is the rate at skill 0.
/// The none value carries zeros, so a caller may copy the fields without checking <see cref="HasFloor"/> first.
/// </summary>
internal readonly record struct LearningFloor(bool HasFloor, int Level, float FloorRate, float FullRate)
{
    public static LearningFloor None => new LearningFloor(false, 0, 0f, 0f);
}

/// <summary>
/// Pure maths for the Loremaster's Line on the character screen. It never names the hero or the game model: the caller
/// passes the learning rate as a function of skill, so a model override (or a future one) is followed for free.
/// </summary>
internal static class LearningFloorCalculator
{
    /// <summary>Two rates closer than this count as the same rate.</summary>
    internal const float Tolerance = 1e-4f;

    /// <summary>The skill bar is MaxLevel + this wide (PerkSelectionBarWidget.GetXPositionOfLevelOnBar; pinned by LoremastersLineEngineMirrorTests).</summary>
    internal const int BarOverhang = 25;

    private const int Ceiling = 4096;
    private const int MaxPercent = 999;

    /// <summary>
    /// Probes skill 0 and 4096, then bisects for the lowest skill whose rate matches the 4096 rate: 14 calls at most.
    /// A rate that is not a finite number at any probe, or a curve that does not fall from skill 0 to 4096 (flat or
    /// rising: there is no slowest point to mark), gives none. A curve that is not
    /// monotonic still gets a boundary (the rate at the level matches the floor, the rate one level below does not),
    /// though not always the lowest one.
    /// </summary>
    public static LearningFloor Find(Func<int, float> rateAt)
    {
        var full = rateAt(0);
        var floor = rateAt(Ceiling);
        if (!FiniteFloatValidator.IsFinite(full) || !FiniteFloatValidator.IsFinite(floor))
            return LearningFloor.None;
        if (!(full - floor > Tolerance))
            return LearningFloor.None;

        // Invariant: the rate at lo is not at the floor, the rate at hi is.
        var lo = 0;
        var hi = Ceiling;
        while (hi - lo > 1)
        {
            var mid = lo + (hi - lo) / 2;
            var rate = rateAt(mid);
            if (!FiniteFloatValidator.IsFinite(rate))
                return LearningFloor.None;

            if (Math.Abs(rate - floor) <= Tolerance) hi = mid;
            else lo = mid;
        }

        return new LearningFloor(true, hi, floor, full);
    }

    /// <summary>The bar's width in levels: MaxLevel + 25. Callers gate on a result above 0.</summary>
    public static int BarLevels(int maxLevel) => maxLevel + BarOverhang;

    /// <summary>True when the floor exists and lies before the end of the bar, so the marker has somewhere to draw.</summary>
    public static bool IsOnBar(LearningFloor floor, int maxLevel)
    {
        var bar = BarLevels(maxLevel);
        return floor.HasFloor && bar > 0 && floor.Level < bar;
    }

    /// <summary>How many levels the red band spans: from the floor to the end of the bar.</summary>
    public static int BandLevels(LearningFloor floor, int maxLevel) => BarLevels(maxLevel) - floor.Level;

    /// <summary>
    /// The percent the tooltip shows, or null when the skill cannot learn at the floor (the capped line shows instead)
    /// or the share has no meaning.
    /// </summary>
    public static int? ShownPercent(LearningFloor floor) =>
        CannotLearn(floor.FloorRate) ? null : PercentOfFull(floor.FloorRate, floor.FullRate);

    /// <summary>
    /// The floor rate as a whole percent of the full rate, 0 to 999; a negative floor counts as 0. Null when the full
    /// rate is not a positive finite number, since a share of nothing has no meaning.
    /// </summary>
    public static int? PercentOfFull(float floorRate, float fullRate)
    {
        if (!FiniteFloatValidator.IsFinite(floorRate) || !FiniteFloatValidator.IsFinite(fullRate) || !(fullRate > 0f))
            return null;

        var share = 100.0 * Math.Max(0f, floorRate) / fullRate;
        var rounded = Math.Round(share, MidpointRounding.AwayFromZero);
        return rounded >= MaxPercent ? MaxPercent : (int)rounded;
    }

    /// <summary>Vanilla's own rule for a skill that cannot learn (SkillVM.CanLearnSkill): the rate rounds to 0.00. NaN cannot learn.</summary>
    public static bool CannotLearn(float floorRate) => !(Math.Round((double)floorRate, 2) > 0.0);
}
