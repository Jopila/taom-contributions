namespace TAOM.Features.AnimationVariety;

/// <summary>The MCM switch of the swing alternative probe (<see cref="AnimVarietyService"/>), as a plain value.</summary>
public interface IAnimVarietyProbeSettingsProvider
{
    /// <summary>
    /// True when the probe should sweep a mission's agents. Read once per mission, at the probe's first tick.
    /// Off by default: the sweep is a per-frame cost, and the probe only logs.
    /// </summary>
    bool SwingAlternativeProbe { get; }
}
