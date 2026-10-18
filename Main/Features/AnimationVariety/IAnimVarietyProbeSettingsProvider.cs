namespace TAOM.Features.AnimationVariety;

/// <summary>The MCM switch of the swing alternative probe (<see cref="AnimVarietyService"/>), as a plain value.</summary>
public interface IAnimVarietyProbeSettingsProvider
{
    /// <summary>
    /// True when the probe should sweep a mission's agents. Read once per mission, at the probe's first tick.
    /// On by default while the swing clips are tested (<c>TaomSettings.EnableSwingAlternativeProbe</c>); the
    /// sweep becomes a per-frame cost only once <c>act_taom_</c> codes are loaded, and the probe only logs.
    /// </summary>
    bool SwingAlternativeProbe { get; }
}
