using System;

namespace TAOM.Features.BattleBalance;

/// <summary>Pure arithmetic of the survival roll's two TAOM adjustments; no engine types, so it is unit tested directly.</summary>
internal static class SurvivalChanceMath
{
    /// <summary>Scales the death chance by (1 - bonus), clamped to 0..1. A zero bonus returns vanilla's value untouched.</summary>
    internal static float ApplyCulturalSurvivalBonus(float vanillaSurvival, float culturalBonus)
    {
        if (culturalBonus == 0f)
            return vanillaSurvival;

        float deathChance = 1f - vanillaSurvival;
        float newDeathChance = deathChance * (1f - culturalBonus);
        float result = 1f - newDeathChance;
        return Math.Max(0f, Math.Min(1f, result));
    }

    /// <summary>
    /// The career TroopSurvival passive: survival times (1 + magnitude), capped at 1. A zero magnitude returns the value
    /// untouched (no cap), as the roll did before this was extracted. GetSurvivalChance returns a raw float, not an
    /// ExplainedNumber, so the multiplicative apply is plain math.
    /// </summary>
    internal static float ApplyCareerSurvival(float survival, float magnitude) =>
        magnitude == 0f ? survival : Math.Min(1f, survival * (1f + magnitude));
}
