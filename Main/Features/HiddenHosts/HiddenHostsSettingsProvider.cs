namespace TAOM.Features.HiddenHosts;

/// <summary>
/// MCM (<c>TaomSettings</c>) over the compiled default. The toggle is read for every visible nameplate every map frame,
/// so the settings object is taken once in the lazy accessor and read through (CampaignHotPathSettingsProvidersTests).
/// Falls back to on while MCM has no instance.
/// </summary>
public sealed class HiddenHostsSettingsProvider : IHiddenHostsSettings
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public HiddenHostsSettingsProvider() { }

    internal HiddenHostsSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool Enabled => Settings?.HiddenHostsEnabled ?? true;
}
