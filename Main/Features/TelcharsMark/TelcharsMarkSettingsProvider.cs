namespace TAOM.Features.TelcharsMark;

public sealed class TelcharsMarkSettingsProvider : ITelcharsMarkSettingsProvider
{
    // MCM absent (no TaomSettings instance): the feature stays on and the box starts unticked.
    public bool IsEnabled => TaomSettings.Instance?.EnableTelcharsMark ?? true;

    public bool ShowOnlyUpgradesByDefault => TaomSettings.Instance?.TelcharsMarkShowOnlyUpgradesByDefault ?? false;
}
