using System;
using TAOM.Core.Validation;
using TaleWorlds.Core;

namespace TAOM.Features.FieldCamp.Activities;

/// <summary>
/// The rules of camp work (#860, docs/features/field-camp.md "Camp work"), pure: what each work yields on what ground, where
/// fishing is allowed, the one injury roll, the morale and the tools. Ground arrives as the integer value of
/// <see cref="TerrainType"/> (the adapter's <c>TerrainTypeAt</c> answer, -1 for none), so a value this build has never heard of
/// falls through every table to "nothing here" instead of inheriting a default. Every float gate is a positive requirement, so a
/// NaN fails it.
/// </summary>
public static class CampActivityRules
{
    /// <summary>Chance that the one roll at the midpoint wounds anyone.</summary>
    public const float InjuryChance = 0.1f;

    /// <summary>Share of the healthy regulars a hit wounds (at least one man).</summary>
    public const float InjuredShare = 0.05f;

    /// <summary>Men who work for free; above this, tools are used up.</summary>
    public const int FreeMen = 10;

    private const float MoralePerClanTier = 0.1f;
    private const int MaxClanTier = 10;
    private const int MaxActivity = (int)CampActivity.Fish;

    /// <summary>How many goods a finished work pays: <c>min(cap, round(perTen x men / 10 x factor))</c>; 0 for any bad number.</summary>
    public static int Yield(float perTenMen, int cap, int healthyMen, float groundFactor)
    {
        if (cap <= 0 || healthyMen <= 0)
            return 0;
        if (!FiniteFloatValidator.IsFinite(perTenMen) || !FiniteFloatValidator.IsFinite(groundFactor))
            return 0;
        // In double: a finite float times an int can pass float's range, and a cast of an infinity is int.MinValue.
        double raw = (double)perTenMen * healthyMen / 10d * groundFactor;
        if (!(raw > 0d))
            return 0;
        if (raw >= cap)
            return cap;
        return (int)Math.Round(raw, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// How well this ground serves this work, 0 to 1. Hunting follows the cover and the game: forest best, plain, steppe and
    /// swamp good, high ground and snow middling, desert and dune poor, water and anything unnamed none. Timber is forest only.
    /// Fishing is allowed on any ground with water near (<paramref name="waterNearby"/>).
    /// </summary>
    public static float GroundFactor(CampActivity activity, int terrain, bool waterNearby)
    {
        switch (activity)
        {
            case CampActivity.Hunt:
                return HuntFactor(terrain);
            case CampActivity.Timber:
                return terrain == (int)TerrainType.Forest ? 1f : 0f;
            case CampActivity.Fish:
                return waterNearby ? 1f : 0f;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// The one table of the ground for hunting: the factor of every terrain this build names, null for a value it has never heard
    /// of. Forest 1.0; plain, steppe and swamp 0.7; farmland, mountain, canyon and snow 0.5; desert and dune 0.2; water, shores,
    /// crossings, cliffs and the movement-restriction faces 0 (named, and nothing to hunt).
    /// </summary>
    public static float? HuntFactorOrNull(int terrain)
    {
        switch (terrain)
        {
            case (int)TerrainType.Forest:
                return 1f;
            case (int)TerrainType.Plain:
            case (int)TerrainType.Steppe:
            case (int)TerrainType.Swamp:
                return 0.7f;
            case (int)TerrainType.RuralArea:
            case (int)TerrainType.Mountain:
            case (int)TerrainType.Canyon:
            case (int)TerrainType.Snow:
                return 0.5f;
            case (int)TerrainType.Desert:
            case (int)TerrainType.Dune:
                return 0.2f;
            case (int)TerrainType.Lake:
            case (int)TerrainType.Water:
            case (int)TerrainType.River:
            case (int)TerrainType.Fording:
            case (int)TerrainType.Bridge:
            case (int)TerrainType.UnderBridge:
            case (int)TerrainType.CoastalSea:
            case (int)TerrainType.OpenSea:
            case (int)TerrainType.Beach:
            case (int)TerrainType.Cliff:
            case (int)TerrainType.NonNavigableRiver:
            case (int)TerrainType.LandRestriction:
            case (int)TerrainType.SeaRestriction:
                return 0f;
            default:
                return null;
        }
    }

    /// <summary>The hunting factor; a terrain the table does not name has none.</summary>
    public static float HuntFactor(int terrain) => HuntFactorOrNull(terrain) ?? 0f;

    /// <summary>True for a terrain this build names in the hunting table; the guard test walks the enum against it.</summary>
    public static bool IsClassified(int terrain) => HuntFactorOrNull(terrain).HasValue;

    /// <summary>Whether a sample counts as water for fishing: rivers, lakes, open water, a ford, the coast, a beach or a bridge.</summary>
    public static bool IsWater(int terrain)
    {
        switch (terrain)
        {
            case (int)TerrainType.River:
            case (int)TerrainType.Lake:
            case (int)TerrainType.Water:
            case (int)TerrainType.Fording:
            case (int)TerrainType.NonNavigableRiver:
            case (int)TerrainType.CoastalSea:
            case (int)TerrainType.Beach:
            case (int)TerrainType.Bridge:
                return true;
            default:
                return false;
        }
    }

    /// <summary>True once, when progress passes the midpoint and the roll has not been made.</summary>
    public static bool InjuryRollDue(float before, float after, bool alreadyRolled) =>
        !alreadyRolled && before < 0.5f && after >= 0.5f;

    /// <summary>The roll (0 to 1, from the engine's random) wounds when it is below <see cref="InjuryChance"/>.</summary>
    public static bool InjuryHits(float roll) => roll >= 0f && roll < InjuryChance;

    /// <summary>About five percent of the healthy regulars, at least one; 0 when there are none. Heroes are never counted.</summary>
    public static int InjuredMen(int healthyRegulars)
    {
        if (healthyRegulars <= 0)
            return 0;
        int hit = (int)Math.Round(healthyRegulars * (double)InjuredShare, MidpointRounding.AwayFromZero);
        return Math.Min(healthyRegulars, Math.Max(1, hit));
    }

    /// <summary>
    /// What a long work costs the party in morale: one per full ten men above ten, less ten percent per clan tier, never below 0.
    /// A positive amount to SUBTRACT; the work never raises morale.
    /// </summary>
    public static float MoraleCost(int healthyMen, int clanTier)
    {
        if (healthyMen <= FreeMen)
            return 0f;
        int tier = Math.Min(Math.Max(clanTier, 0), MaxClanTier);
        float cost = (healthyMen - FreeMen) / 10 * (1f - MoralePerClanTier * tier);
        return cost > 0f ? cost : 0f;
    }

    /// <summary>Tools used up at the end: none for ten men or fewer, then one per ten men started.</summary>
    public static int ToolsNeeded(int healthyMen) =>
        healthyMen <= FreeMen ? 0 : (healthyMen + 9) / 10;

    /// <summary>0 to 1 progress from the saved hours; a degenerate target counts as finished, a non-finite done as not started.</summary>
    public static float Fraction(float hoursDone, float targetHours)
    {
        if (!FiniteFloatValidator.IsFinite(hoursDone) || hoursDone <= 0f)
            return 0f;
        if (!FiniteFloatValidator.IsFinite(targetHours) || targetHours <= 0f)
            return 1f;
        float fraction = hoursDone / targetHours;
        return fraction >= 1f ? 1f : fraction;
    }

    /// <summary>Whether a loaded or live activity record is usable: none, or a known kind with finite, non-negative hours and a positive target.</summary>
    public static bool IsSane(int activity, float hoursDone, float targetHours)
    {
        if (activity == (int)CampActivity.None)
            return true;
        if (activity < 0 || activity > MaxActivity)
            return false;
        return FiniteFloatValidator.IsFiniteAtLeast(hoursDone, 0f) && FiniteFloatValidator.IsFinite(targetHours) && targetHours > 0f;
    }
}
