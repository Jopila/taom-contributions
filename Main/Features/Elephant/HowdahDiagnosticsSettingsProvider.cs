namespace TAOM.Features.Elephant;

/// <summary>
/// Wraps <see cref="TaomSettings"/> so the howdah code can read the toggle without MCM loaded. TaomSettings.Instance is
/// null in tests, before MCM initializes, or on a client whose MCM failed to load (a dedicated server always gets the
/// instance built from taom-server-settings.json), so the fallback must equal the compiled default: on, while the platform is being tested
/// (Mike, 2026-09-19, #627). HowdahDiagnosticsSettingsProviderTests pins both.
/// </summary>
public class HowdahDiagnosticsSettingsProvider : IHowdahDiagnosticsSettingsProvider
{
    // Read per seat per frame by TaomHowdahStandingPoint: cached on the first non-null read, read through
    // (BattleBalanceSettingsProvider pattern).
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public HowdahDiagnosticsSettingsProvider() { }
    internal HowdahDiagnosticsSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool IsEnabled => Settings?.EnableHowdahDiagnostics ?? true;
}
