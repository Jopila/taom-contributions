using System;

namespace TAOM.Features.LongMemory;

/// <summary>
/// The pure arithmetic of Long Memory (docs/features/long-memory.md, #865). A loss is a whole negative number rounded half away
/// from zero (12.5 is 13), computed in <c>decimal</c> so that 0.7 times 50 is 35 and not 34.99999, and it stops at the engine's
/// relation floor of -100. A value that is not a finite number, a magnitude or multiplier below 0, and a loss that rounds to 0
/// cost nothing, so no input can turn a penalty into a gain.
/// </summary>
public static class LongMemoryRules
{
    /// <summary>
    /// The option id of "Continue" on the <c>force_supplies_no_resist</c> menu, where the villagers do not resist and the engine
    /// applies its coercion with no map event. Pinned from VillageHostileActionCampaignBehavior.cs:145 (engine v1.5.5).
    /// </summary>
    public const string SuppliesNoResistOptionId = "force_supplies_no_resist_continue";

    /// <summary>The same for <c>force_volunteers_no_resist</c>, VillageHostileActionCampaignBehavior.cs:151.</summary>
    public const string VolunteersNoResistOptionId = "force_volunteers_no_resist_continue";

    /// <summary>The engine clamps a relation at -100, so no single loss is asked for more.</summary>
    public const int MaxPenalty = 100;

    // Keeps (decimal) from overflowing on a hand-edited multiplier; the result is capped at MaxPenalty anyway.
    private const float ConversionCap = 1000f;

    /// <summary>
    /// <c>magnitude</c> times <c>multiplier</c> times <c>scale</c>, negated, as a whole number between -100 and 0. 0 for a
    /// magnitude, multiplier or scale that is not above 0 or is not finite.
    /// </summary>
    public static int Penalty(int magnitude, float multiplier, float scale = 1f)
    {
        if (magnitude <= 0 || !IsUsable(multiplier) || !IsUsable(scale))
            return 0;
        var product = (decimal)magnitude * (decimal)Math.Min(multiplier, ConversionCap) * (decimal)Math.Min(scale, ConversionCap);
        var rounded = Math.Round(product, 0, MidpointRounding.AwayFromZero);
        return -(int)Math.Min(rounded, MaxPenalty);
    }

    /// <summary>True for the two "Continue" options after which the engine takes goods or recruits with no map event.</summary>
    public static bool IsNoResistCoercionOption(string? optionId) =>
        string.Equals(optionId, SuppliesNoResistOptionId, StringComparison.Ordinal)
        || string.Equals(optionId, VolunteersNoResistOptionId, StringComparison.Ordinal);

    private static bool IsUsable(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
}
