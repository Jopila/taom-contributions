using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Validation;

namespace TAOM.Features.FarWatch;

/// <summary>
/// The arithmetic of Far Watch (docs/features/far-watch.md "Constants"). Pure: hours are doubles, factors floats, and every
/// gate on a clock or a distance is a positive requirement, so a NaN fails it. Nothing here reads the engine.
/// </summary>
internal static class FarWatchMath
{
    /// <summary>The share of base sight a surroundings ride earns by its end (+50 %).</summary>
    internal const float RampMax = 0.5f;

    /// <summary>The share of base sight a settlement ride costs while it runs (-35 %).</summary>
    internal const float SettlementPenalty = -0.35f;

    /// <summary>A scout rests this long after every ride, however it ended.</summary>
    internal const double RestHours = 8d;

    /// <summary>The settlements the pick offers, and the radius (map units, party to gate) they must lie within.</summary>
    internal const int MaxSites = 8;

    internal const float SiteRadius = 50f;

    /// <summary>Hosts listed in a surroundings report; the same hosts keep their names past the horizon.</summary>
    internal const int MaxRows = 6;

    /// <summary>Scouting past this adds nothing to a ride's length or to the sight it holds.</summary>
    internal const int SkillCap = 300;

    /// <summary>The Scouting skill clamp of the saved record and of the XP.</summary>
    internal const int StoredSkillCap = 1000;

    /// <summary>The longest ride: a surroundings ride with no Scouting. The saved record may not hold a longer one.</summary>
    internal const double MaxRideHours = 8d;

    /// <summary>The longest hold of a kept sight: <see cref="HoldHours"/> at <see cref="SkillCap"/> Scouting. The saved record may not hold a longer one.</summary>
    internal const double MaxHoldHours = BaseHoldHours * 2d;

    private const double SurroundingsBaseHours = MaxRideHours;
    private const double SurroundingsSkillHours = 4d;
    private const double SettlementShare = 0.7d;
    private const double BaseHoldHours = 24d;
    private const float BaseXp = 150f;
    private const float XpPerSkill = 0.5f;
    private const int SecondStageSkill = 150;

    /// <summary>8 hours less 4 for a full 300 Scouting; a settlement ride is 70 % of that. No kind rides no time.</summary>
    internal static double RideHours(ScoutKind kind, int skill)
    {
        if (kind != ScoutKind.Surroundings && kind != ScoutKind.Settlement) return 0d;
        double hours = SurroundingsBaseHours - SurroundingsSkillHours * Clamp(skill, 0, SkillCap) / SkillCap;
        return kind == ScoutKind.Settlement ? hours * SettlementShare : hours;
    }

    /// <summary>How far along a ride is, 0 to 1, from its stored start; 0 for any unusable input, so a bad clock never completes a ride.</summary>
    internal static float Fraction(double startHour, double durationHours, double nowHour)
    {
        if (!(FiniteFloatValidator.IsFinite(startHour) && FiniteFloatValidator.IsFinite(nowHour)
              && FiniteFloatValidator.IsFinite(durationHours) && durationHours > 0d)) return 0f;
        double progress = (nowHour - startHour) / durationHours;
        if (!(progress > 0d)) return 0f;
        return progress >= 1d ? 1f : (float)progress;
    }

    /// <summary>Half of base sight, reached in a straight line over the ride; 0 before the start, and for any unusable input.</summary>
    internal static float Ramp(double startHour, double durationHours, double nowHour) =>
        RampMax * Fraction(startHour, durationHours, nowHour);

    /// <summary>24 hours at full strength, widened up to double by Scouting.</summary>
    internal static double HoldHours(int skill) => BaseHoldHours * (1d + (double)Clamp(skill, 0, SkillCap) / SkillCap);

    /// <summary>
    /// A kept sight bonus of <paramref name="level"/>: full strength for <paramref name="holdHours"/> from its start, then a
    /// straight fade over half that time to nothing. 0 before the start and for any unusable input.
    /// </summary>
    internal static float BuffAt(float level, double startHour, double holdHours, double nowHour)
    {
        if (!(FiniteFloatValidator.IsFinite(level) && level > 0f && FiniteFloatValidator.IsFinite(startHour)
              && FiniteFloatValidator.IsFinite(nowHour) && FiniteFloatValidator.IsFinite(holdHours) && holdHours > 0d)) return 0f;
        double elapsed = nowHour - startHour;
        if (!(elapsed >= 0d)) return 0f;
        if (elapsed <= holdHours) return level;
        double fade = holdHours / 2d;
        double into = elapsed - holdHours;
        return into >= fade ? 0f : (float)(level * (1d - into / fade));
    }

    /// <summary>Digit stages a report adds to every host in sight: one, two from 150 Scouting.</summary>
    internal static int Stages(int skill) => skill >= SecondStageSkill ? 2 : 1;

    /// <summary>Raw Scouting XP for a full return: 150 plus half the skill.</summary>
    internal static float Xp(int skill) => BaseXp + XpPerSkill * Clamp(skill, 0, StoredSkillCap);

    /// <summary>
    /// The compass sector of an offset (+Y north, +X east): 0 north, 1 north-east, and on clockwise to 7 north-west, each
    /// 45 degrees wide and centred on its point, a boundary belonging to the next one clockwise. -1 for a zero or non-finite
    /// offset, which has no direction.
    /// </summary>
    internal static int CompassSector(float dx, float dy)
    {
        if (!(FiniteFloatValidator.IsFinite(dx) && FiniteFloatValidator.IsFinite(dy))) return -1;
        if (dx == 0f && dy == 0f) return -1;
        double degrees = Math.Atan2(dx, dy) * 180d / Math.PI;
        if (degrees < 0d) degrees += 360d;
        return (int)Math.Floor((degrees + 22.5d) / 45d) % 8;
    }

    /// <summary>
    /// The kind a formation class belongs to for the report: 0 foot (Infantry, HeavyInfantry), 1 archers (Ranged, Skirmisher),
    /// 2 horse (Cavalry, LightCavalry, HeavyCavalry), 3 horse archers; -1 for anything else. The ordinals are TaleWorlds.Core.FormationClass,
    /// pinned by a binding test.
    /// </summary>
    internal static int BucketOf(int formationClass)
    {
        switch (formationClass)
        {
            case 0:
            case 5:
                return 0;
            case 1:
            case 4:
                return 1;
            case 2:
            case 6:
            case 7:
                return 2;
            case 3:
                return 3;
            default:
                return -1;
        }
    }

    /// <summary>
    /// Adds <paramref name="healthy"/> men of one formation class to the four kind counts (<see cref="BucketOf"/>); a class the
    /// mapping leaves out, or no men, adds nothing. The adapter calls it for the party's own roster and for each party attached
    /// to it, so an army leader's kinds are the army's.
    /// </summary>
    internal static void AddMen(int formationClass, int healthy, ref int foot, ref int archers, ref int horse, ref int horseArchers)
    {
        if (healthy <= 0) return;
        switch (BucketOf(formationClass))
        {
            case 0: foot += healthy; break;
            case 1: archers += healthy; break;
            case 2: horse += healthy; break;
            case 3: horseArchers += healthy; break;
        }
    }

    /// <summary>
    /// The name a party's map plate prints, which the report must match: the army's name for a party in an army group (its
    /// leader or a party attached to it), else the leader hero's, else the party's own. Each argument is blank when absent.
    /// </summary>
    internal static string PlateName(bool inArmyGroup, string? armyName, string? leaderName, string? partyName)
    {
        if (inArmyGroup && !string.IsNullOrEmpty(armyName)) return armyName!;
        if (!string.IsNullOrEmpty(leaderName)) return leaderName!;
        return partyName ?? string.Empty;
    }

    /// <summary>The kind with the most men; a tie goes to foot, then archers, then horse, then horse archers. No men gives no hint.</summary>
    internal static ScoutComposition Composition(int foot, int archers, int horse, int horseArchers)
    {
        var best = ScoutComposition.None;
        int most = 0;
        if (foot > most) { best = ScoutComposition.Foot; most = foot; }
        if (archers > most) { best = ScoutComposition.Archers; most = archers; }
        if (horse > most) { best = ScoutComposition.Horse; most = horse; }
        if (horseArchers > most) best = ScoutComposition.HorseArchers;
        return best;
    }

    /// <summary>
    /// The report order: an army leader before a lord before the rest, then nearest first (a distance that is not finite last),
    /// then by id so the order is the same on every run.
    /// </summary>
    internal static int CompareSightings(ScoutSighting a, ScoutSighting b)
    {
        int byRank = Rank(a).CompareTo(Rank(b));
        if (byRank != 0) return byRank;
        int byDistance = Comparable(a.Intel.DistanceToMain).CompareTo(Comparable(b.Intel.DistanceToMain));
        return byDistance != 0 ? byDistance : string.CompareOrdinal(a.Intel.StringId, b.Intel.StringId);
    }

    /// <summary>
    /// Fills <paramref name="into"/> (cleared first) with the towns and castles the pick offers: not the player clan's, within
    /// <see cref="SiteRadius"/> of the party's position gate to gate (the edge is in), the nearest <see cref="MaxSites"/>,
    /// nearest first, ties by id. A distance that is not finite or is negative drops the site.
    /// </summary>
    internal static void PickSites(IReadOnlyList<ScoutSite> all, List<ScoutSite> into)
    {
        into.Clear();
        for (int i = 0; i < all.Count; i++)
        {
            var site = all[i];
            if (site.OwnedByPlayerClan || !FiniteFloatValidator.IsFiniteInRange(site.Distance, 0f, SiteRadius)) continue;
            into.Add(site);
        }

        into.Sort((x, y) =>
        {
            int byDistance = x.Distance.CompareTo(y.Distance);
            return byDistance != 0 ? byDistance : string.CompareOrdinal(x.Id, y.Id);
        });
        if (into.Count > MaxSites) into.RemoveRange(MaxSites, into.Count - MaxSites);
    }

    private static int Rank(ScoutSighting s) => s.Intel.LeadsArmy ? 0 : s.IsLordWithHero ? 1 : 2;

    private static float Comparable(float distance) => FiniteFloatValidator.IsFinite(distance) ? distance : float.MaxValue;

    private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
}
