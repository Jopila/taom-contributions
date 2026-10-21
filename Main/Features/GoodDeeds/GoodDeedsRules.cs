using System;
using TAOM.Features.Execution;

namespace TAOM.Features.GoodDeeds;

/// <summary>
/// The pure arithmetic of Good Deeds. Every scaled relation rounds half away from zero (2.5 is 3, 0.5 is 1), so a small amount
/// is not lost to the multiplier unless the multiplier itself is 0, and a grant of 0 is nothing at all.
/// </summary>
public static class GoodDeedsRules
{
    /// <summary>
    /// <c>amount</c> times <c>multiplier</c>, rounded half away from zero. The multiplier is read as a decimal so that a value
    /// such as 0.7 does not turn 5 into 3.4999999. A non-finite multiplier scales nothing (0).
    /// </summary>
    public static int Scale(int amount, float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
            return 0;
        return (int)Math.Round((decimal)amount * (decimal)multiplier, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// What a donation returns to the engine: vanilla's gain plus the bonus, both scaled, as a whole number (the engine applies
    /// <c>(int)</c> of it). A non-finite vanilla value counts as 0, so the bonus alone is scaled.
    /// </summary>
    public static float ScaleDonation(float vanillaGain, int bonus, float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
            return 0f;
        var vanilla = float.IsNaN(vanillaGain) || float.IsInfinity(vanillaGain) ? 0m : (decimal)vanillaGain;
        return (float)Math.Round((vanilla + bonus) * (decimal)multiplier, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// How two sides stand: Neutral when either is Neutral (a side with no alignment is nobody's ally or enemy), Same when they
    /// match, Opposite otherwise.
    /// </summary>
    public static SidePairing Pair(FactionSide first, FactionSide second)
    {
        if (first == FactionSide.Neutral || second == FactionSide.Neutral)
            return SidePairing.Neutral;
        return first == second ? SidePairing.Same : SidePairing.Opposite;
    }
}
