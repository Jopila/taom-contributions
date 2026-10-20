using System;
using System.Collections.Generic;
using TAOM.Core.Validation;

namespace TAOM.Features.FellFoes.Domain;

/// <summary>
/// Every number the Fell Foes rules read, validated once. <see cref="Create"/> checks each field for NaN
/// and infinity first, then range and sign, then the ordering between related fields; a field that fails
/// reverts to its default and adds a line to <see cref="Warnings"/>, so a stale json2 value or a hand edit
/// never reaches a comparison. Pure: no engine types.
/// </summary>
public sealed class FellFoesTunables
{
    public const int DefaultCorneredMaxFoes = 3;
    public const float DefaultCorneredRadius = 5f;
    public const float DefaultCorneredMinHealthFraction = 0.3f;
    public const int DefaultCorneredRallyCap = 4;
    public const float DefaultArcherChance = 0.25f;
    public const int DefaultArcherMaxHolding = 8;
    public const int DefaultArcherNewPerPass = 3;
    public const float DefaultArcherHoldSeconds = 8f;
    public const float DefaultArcherMinMorale = 50f;
    public const float DefaultArcherFieldRange = 35f;
    public const float DefaultArcherSiegeRange = 20f;
    public const int DefaultStragglerMaxCount = 3;
    public const int DefaultStragglerPeakMin = 40;
    public const float DefaultStragglerHoldSeconds = 30f;

    /// <summary>
    /// A cheap bound on the foe-count query. <c>Mission.GetNearbyEnemyAgentCount</c> wraps the native
    /// <c>GetAgentCountAroundPosition</c>, which walks only the grid cells under the query circle, so a bigger radius
    /// costs more cells, not a whole-battle scan; 15 m keeps the cost small and a cornered fight is a few steps wide.
    /// </summary>
    public const float MaxNearbyRadius = 15f;

    private FellFoesTunables(List<string> warnings) => Warnings = warnings;

    public int CorneredMaxFoes { get; private set; }
    public float CorneredRadius { get; private set; }
    public float CorneredMinHealthFraction { get; private set; }

    /// <summary>
    /// How far from a turned soldier a foe may stand and still keep him turned: twice the cornered radius, clamped to
    /// <see cref="MaxNearbyRadius"/>. A rallied man stands up to a cornered radius from the hit router, so the retention
    /// test is wider than the admission test or he would run again a second later.
    /// </summary>
    public float HoldRadius => Math.Min(2f * CorneredRadius, MaxNearbyRadius);

    /// <summary>Most other routers one newly cornered router rallies, so a rout cannot be undone wholesale.</summary>
    public int CorneredRallyCap { get; private set; }
    public float ArcherChance { get; private set; }
    public int ArcherMaxHolding { get; private set; }
    public int ArcherNewPerPass { get; private set; }
    public float ArcherHoldSeconds { get; private set; }
    public float ArcherMinMorale { get; private set; }
    public float ArcherFieldRange { get; private set; }
    public float ArcherSiegeRange { get; private set; }
    public int StragglerMaxCount { get; private set; }
    public int StragglerPeakMin { get; private set; }
    public float StragglerHoldSeconds { get; private set; }

    /// <summary>One line per field that reverted to its default; empty when every input was valid.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Builds the validated set; a null argument means "use the default" and is not a warning.</summary>
    public static FellFoesTunables Create(
        int? corneredMaxFoes = null, float? corneredRadius = null, float? corneredMinHealthFraction = null,
        float? archerChance = null, int? archerMaxHolding = null, int? archerNewPerPass = null,
        float? archerHoldSeconds = null, float? archerMinMorale = null, float? archerFieldRange = null,
        float? archerSiegeRange = null, int? stragglerMaxCount = null, int? stragglerPeakMin = null,
        float? stragglerHoldSeconds = null, int? corneredRallyCap = null)
    {
        var w = new List<string>();
        var t = new FellFoesTunables(w)
        {
            CorneredMaxFoes = Int(corneredMaxFoes, 1, 10, DefaultCorneredMaxFoes, "CorneredMaxFoes", w),
            CorneredRadius = Float(corneredRadius, 1f, MaxNearbyRadius, DefaultCorneredRadius, "CorneredRadius", w),
            CorneredMinHealthFraction = Float(corneredMinHealthFraction, 0f, 1f, DefaultCorneredMinHealthFraction, "CorneredMinHealthFraction", w),
            CorneredRallyCap = Int(corneredRallyCap, 0, 10, DefaultCorneredRallyCap, "CorneredRallyCap", w),
            ArcherChance = Float(archerChance, 0f, 1f, DefaultArcherChance, "ArcherChance", w),
            ArcherMaxHolding = Int(archerMaxHolding, 1, 30, DefaultArcherMaxHolding, "ArcherMaxHolding", w),
            ArcherNewPerPass = Int(archerNewPerPass, 1, 10, DefaultArcherNewPerPass, "ArcherNewPerPass", w),
            ArcherHoldSeconds = Float(archerHoldSeconds, 1f, 30f, DefaultArcherHoldSeconds, "ArcherHoldSeconds", w),
            ArcherMinMorale = Float(archerMinMorale, 0f, 100f, DefaultArcherMinMorale, "ArcherMinMorale", w),
            ArcherFieldRange = Float(archerFieldRange, 5f, 100f, DefaultArcherFieldRange, "ArcherFieldRange", w),
            ArcherSiegeRange = Float(archerSiegeRange, 5f, 100f, DefaultArcherSiegeRange, "ArcherSiegeRange", w),
            StragglerMaxCount = Int(stragglerMaxCount, 1, 20, DefaultStragglerMaxCount, "StragglerMaxCount", w),
            StragglerPeakMin = Int(stragglerPeakMin, 2, 5000, DefaultStragglerPeakMin, "StragglerPeakMin", w),
            StragglerHoldSeconds = Float(stragglerHoldSeconds, 5f, 300f, DefaultStragglerHoldSeconds, "StragglerHoldSeconds", w),
        };

        if (t.ArcherSiegeRange > t.ArcherFieldRange)
        {
            w.Add("Fell Foes: the siege archer range is above the field range; both revert to their defaults.");
            t.ArcherFieldRange = DefaultArcherFieldRange;
            t.ArcherSiegeRange = DefaultArcherSiegeRange;
        }

        if (t.StragglerMaxCount > t.StragglerPeakMin)
        {
            w.Add("Fell Foes: the straggler count is above the peak army size it needs; both revert to their defaults.");
            t.StragglerMaxCount = DefaultStragglerMaxCount;
            t.StragglerPeakMin = DefaultStragglerPeakMin;
        }

        return t;
    }

    private static float Float(float? raw, float min, float max, float fallback, string name, List<string> warnings)
    {
        if (!raw.HasValue) return fallback;
        if (FiniteFloatValidator.IsFiniteInRange(raw.Value, min, max)) return raw.Value;
        warnings.Add($"Fell Foes: {name} {raw.Value} is outside {min} to {max} (or not a number); using {fallback}.");
        return fallback;
    }

    private static int Int(int? raw, int min, int max, int fallback, string name, List<string> warnings)
    {
        if (!raw.HasValue) return fallback;
        if (raw.Value >= min && raw.Value <= max) return raw.Value;
        warnings.Add($"Fell Foes: {name} {raw.Value} is outside {min} to {max}; using {fallback}.");
        return fallback;
    }
}
