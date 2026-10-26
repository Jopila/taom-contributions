namespace TAOM.Features.SmithsMeasure;

/// <summary>The "Smithing" MCM values of Smith's Measure (#910), clamped. Read whenever the smithing model answers, never per frame.</summary>
public interface ISmithsMeasureSettingsProvider
{
    /// <summary>True to cap the smelting output. Default true.</summary>
    bool CapSmelting { get; }

    /// <summary>The most of one iron grade, of iron ore and of hardwood that a smelt returns, 1 to 10. A value outside the range reads as the default, 1.</summary>
    int YieldCap { get; }

    /// <summary>True to show the Make Tools button on the refinement tab. Default true.</summary>
    bool MakeToolsEnabled { get; }
}
