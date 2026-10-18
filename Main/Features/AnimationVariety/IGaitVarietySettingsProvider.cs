namespace TAOM.Features.AnimationVariety;

/// <summary>The three MCM switches of the gait variety feature, as plain values.</summary>
public interface IGaitVarietySettingsProvider
{
    /// <summary>Lever 1: scale each soldier's step size by a random factor.</summary>
    bool RandomStepSize { get; }

    /// <summary>
    /// The lowest step size factor, as the player set it. Not validated here: <see cref="GaitVarietyService"/>
    /// rejects a value outside <c>0.80..1.00</c> (and NaN or infinity) and falls back to its default.
    /// </summary>
    float StepSizeMinFactor { get; }

    /// <summary>Lever 2: replace each soldier's action set with one of its configured variants.</summary>
    bool RandomGaitSet { get; }
}
