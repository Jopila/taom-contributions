using System;
using TAOM.Core.Validation;

namespace TAOM.Features.BillsBurden;

/// <summary>An invalid fill carries Percent 0 and no warning, so callers may copy both without checking IsValid.</summary>
internal readonly record struct CargoFill(bool IsValid, int Percent, bool HasWarning);

/// <summary>Pure cargo-fill maths for the map-bar row: percent of capacity used, and the over-capacity flag.</summary>
internal static class CargoFillCalculator
{
    private const int MaxPercent = 999;

    /// <param name="capacity">The engine's int <c>MobileParty.InventoryCapacity</c>.</param>
    public static CargoFill Compute(float weight, int capacity)
    {
        if (capacity <= 0 || !FiniteFloatValidator.IsFinite(weight))
            return new CargoFill(false, 0, false);

        double used = weight < 0f ? 0.0 : weight;
        var ratio = Math.Round(100.0 * used / capacity, MidpointRounding.AwayFromZero);
        var percent = ratio >= MaxPercent ? MaxPercent : (int)ratio;

        // Warn on the raw values so 100.4% (shown as 100) is still red.
        return new CargoFill(true, percent, weight > capacity);
    }
}
