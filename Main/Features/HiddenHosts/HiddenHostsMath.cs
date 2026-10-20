using System;
using System.Globalization;
using TAOM.Core.Validation;

namespace TAOM.Features.HiddenHosts;

/// <summary>
/// The arithmetic behind the nameplate mask (#846, docs/features/hidden-hosts.md). A digit of a party's troop count is
/// known when the party is closer than that digit's threshold, a fraction of the player's seeing range; the digits are
/// read from the left, so the known ones are always a prefix ("1??", "12?"). Every float gate is written as a positive
/// requirement, so a NaN fails it.
/// </summary>
internal static class HiddenHostsMath
{
    internal const string HorizonText = "...";

    /// <summary>The horizon starts past this share of the party's own spotting reach (nine tenths).</summary>
    internal const float HorizonFraction = 0.9f;

    /// <summary>A digit already known stays known out to this multiple of its threshold, so a party at the edge does not flicker.</summary>
    internal const float Hysteresis = 1.1f;

    internal const float MaxScoutMultiplier = 2.25f;

    private const float SkillBonusPerFifty = 0.12f;

    // Thresholds by digit count (index = digits - 1), left digit first. Six or more digits reuse the five-digit row, and
    // every digit past its end takes the last value.
    private static readonly float[][] Rows =
    {
        new[] { 0.28f },
        new[] { 0.50f, 0.26f },
        new[] { 0.62f, 0.32f, 0.20f },
        new[] { 0.70f, 0.45f, 0.28f, 0.16f },
        new[] { 0.75f, 0.50f, 0.32f, 0.20f, 0.14f },
    };

    /// <summary>Decimal digits of <paramref name="count"/>; zero (and anything below) is one digit.</summary>
    internal static int DigitCount(int count)
    {
        int digits = 1;
        for (long n = count; n >= 10; n /= 10)
            digits++;
        return digits;
    }

    /// <summary>The threshold, as a fraction of the seeing range, for digit <paramref name="index"/> (0 = leftmost) of a number of <paramref name="digits"/> digits.</summary>
    internal static float Threshold(int digits, int index)
    {
        var row = Rows[Math.Min(Math.Max(digits, 1), Rows.Length) - 1];
        return row[Math.Min(Math.Max(index, 0), row.Length - 1)];
    }

    /// <summary>How much farther than the base thresholds the player reads: Scouting skill, doubled for a host of the player's own realm, capped.</summary>
    internal static float ScoutMultiplier(int scoutingSkill, bool sameRealm)
    {
        float skill = 1f + SkillBonusPerFifty * Math.Max(0, scoutingSkill) / 50f;
        return Math.Min(skill * (sameRealm ? 2f : 1f), MaxScoutMultiplier);
    }

    /// <summary>
    /// How many leading digits of <paramref name="count"/> the player knows, 0 to the digit count. A digit below
    /// <paramref name="previouslyKnown"/> is held out to <see cref="Hysteresis"/> times its threshold.
    /// </summary>
    internal static int KnownDigits(int count, float distanceRatio, float multiplier, int previouslyKnown)
    {
        if (!FiniteFloatValidator.IsFinite(distanceRatio)) return 0;
        if (!(FiniteFloatValidator.IsFinite(multiplier) && multiplier > 0f)) return 0;

        int digits = DigitCount(count);
        int known = 0;
        for (int k = 0; k < digits; k++)
        {
            float reach = Threshold(digits, k) * multiplier;
            bool inside = distanceRatio < reach || (k < previouslyKnown && distanceRatio < reach * Hysteresis);
            if (!inside) break;
            known++;
        }
        return known;
    }

    /// <summary>
    /// True when the party is past nine tenths of its own spotting reach. A besieger or raider of a settlement the player can
    /// see stays visible farther out than its spotting ratio says, so for those parties the horizon band is wider.
    /// </summary>
    internal static bool IsHorizon(float distanceRatio, float spottingRatio)
    {
        if (!FiniteFloatValidator.IsFinite(distanceRatio)) return false;
        if (!(FiniteFloatValidator.IsFinite(spottingRatio) && spottingRatio > 0f)) return false;
        return distanceRatio > HorizonFraction * spottingRatio;
    }

    /// <summary>The count with its unknown trailing digits replaced by "?"; all digits known gives the plain number.</summary>
    internal static string FormatMask(int count, int knownDigits)
    {
        int digits = DigitCount(count);
        string text = Math.Max(0, count).ToString(CultureInfo.InvariantCulture);
        if (knownDigits >= digits) return text;
        int known = Math.Max(0, knownDigits);
        return text.Substring(0, known) + new string('?', digits - known);
    }

    /// <summary>Marks a count the player remembers rather than reads now.</summary>
    internal const string MemoryPrefix = "~";

    /// <summary>Scouting skill past this adds nothing to the hold or the fade step.</summary>
    private const int MaxScoutForMemory = 1000;

    /// <summary>The XP for reading the first, second and third digit stage of an enemy host's count (raw Scouting XP).</summary>
    private static readonly int[] StageXp = { 20, 30, 50 };

    /// <summary>A remembered count: "~" and the count with its forgotten trailing digits replaced by "?" (all digits kept gives "~150").</summary>
    internal static string FormatMemory(int count, int knownDigits) => MemoryPrefix + FormatMask(count, knownDigits);

    /// <summary>
    /// Whole hours a counted figure is held before it starts to lose digits: six, widened by Scouting (6 x (1 + skill / 150)),
    /// rounded up, the skill clamped to 0 to 1000. Integer arithmetic, so no float is cast and every row is exact.
    /// </summary>
    internal static int HoldHours(int scoutingSkill) =>
        CeilingDivide(6 * (150 + ClampScout(scoutingSkill)), 150);

    /// <summary>Whole hours between two lost digits: eight, widened by Scouting (8 x (1 + skill / 300)), rounded up.</summary>
    internal static int FadeStepHours(int scoutingSkill) =>
        CeilingDivide(8 * (300 + ClampScout(scoutingSkill)), 300);

    /// <summary>The campaign hour as a whole number, or -1 when it is not finite, negative or past <c>int.MaxValue</c> (no clock).</summary>
    internal static int ToWholeHours(double hours)
    {
        if (!(FiniteFloatValidator.IsFinite(hours) && hours >= 0.0 && hours <= int.MaxValue)) return -1;
        return (int)Math.Floor(hours);
    }

    /// <summary>The XP for reaching digit stage <paramref name="stage"/> (1 to 3); 0 for any other stage.</summary>
    internal static int XpForStage(int stage) => stage >= 1 && stage <= StageXp.Length ? StageXp[stage - 1] : 0;

    private static int ClampScout(int scoutingSkill) => Math.Min(Math.Max(scoutingSkill, 0), MaxScoutForMemory);

    private static int CeilingDivide(int numerator, int denominator) => (numerator + denominator - 1) / denominator;

    /// <summary>
    /// The first <paramref name="known"/> decimal digits of <paramref name="count"/> as a number: 12 for (123, 2), the whole
    /// count when <paramref name="known"/> covers it, 0 for none or for a count of zero or less. Integer arithmetic only: it
    /// runs for every nameplate every frame.
    /// </summary>
    internal static int LeadingDigits(int count, int known)
    {
        if (count <= 0 || known <= 0) return 0;
        int dropped = DigitCount(count) - known;
        long value = count;
        for (int i = 0; i < dropped; i++)
            value /= 10;
        return (int)value;
    }

    /// <summary>
    /// Whether a reading of <paramref name="known"/> leading digits of <paramref name="count"/> is consistent with a
    /// remembered count: the same number of digits and the same leading digits. A reading that disagrees means the host is not
    /// the size the player remembers, so the live view replaces the memory; one that agrees only shows less than the memory.
    /// </summary>
    internal static bool Agrees(int count, int known, int remembered) =>
        DigitCount(count) == DigitCount(remembered) && LeadingDigits(count, known) == LeadingDigits(remembered, known);
}
