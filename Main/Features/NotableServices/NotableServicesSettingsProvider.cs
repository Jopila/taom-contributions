namespace TAOM.Features.NotableServices;

/// <summary>
/// The MCM switch over a default of on. The settings object is taken once and only a non-null instance is kept, so a read
/// before MCM is up cannot pin the default (CampaignHotPathSettingsProvidersTests). The switch is read by every dialog
/// condition, so it is cheap by construction.
/// </summary>
public sealed class NotableServicesSettingsProvider : INotableServicesSettingsProvider
{
    private TaomSettings? _settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool Enabled => EnabledFrom(Settings);

    /// <summary>The switch, or on when MCM has not built its settings yet.</summary>
    public static bool EnabledFrom(TaomSettings? settings) => settings?.EnableNotableServices ?? true;
}
