namespace TAOM.Features.AnimationVariety;

/// <summary>
/// Reads the swing alternative probe's switch. The probe asks once per mission, but the settings object is
/// still taken once, on the first non-null read, and read through (csharp-architecture.md rule 9;
/// <c>HotPathSettingsProvidersTests</c>), so a caller that polls it later pays nothing extra.
/// </summary>
public sealed class AnimVarietyProbeSettingsProvider : IAnimVarietyProbeSettingsProvider
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool SwingAlternativeProbe => Settings?.EnableSwingAlternativeProbe ?? false;
}
