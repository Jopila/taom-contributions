namespace TAOM.Features.AnimationVariety;

/// <summary>
/// Reads the three MCM switches. Read on every spawn, so the settings object is taken once, on the first
/// non-null read, and read through (csharp-architecture.md rule 9; <c>HotPathSettingsProvidersTests</c>).
/// Range and NaN validation of the factor is in <see cref="GaitVarietyService"/>, the one consumer.
/// </summary>
public sealed class GaitVarietySettingsProvider : IGaitVarietySettingsProvider
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool RandomStepSize => Settings?.EnableRandomStepSize ?? true;

    public float StepSizeMinFactor => Settings?.GaitStepSizeMinFactor ?? GaitVarietyService.DefaultMinFactor;

    public bool RandomGaitSet => Settings?.EnableRandomGaitSet ?? true;
}
