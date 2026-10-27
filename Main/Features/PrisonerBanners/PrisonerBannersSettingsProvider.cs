namespace TAOM.Features.PrisonerBanners;

/// <summary>
/// The MCM switch for the prisoner banners on the party screen (#902).
/// </summary>
public sealed class PrisonerBannersSettingsProvider
{
    /// <summary>The shipped default, shared with the MCM property and the fallback before MCM is up.</summary>
    public const bool DefaultEnabled = true;

    private TaomSettings? _settings;

    public PrisonerBannersSettingsProvider()
    {
    }

    internal PrisonerBannersSettingsProvider(TaomSettings settings) => _settings = settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    /// <summary>True when a captive hero's row shows the banners and the icon.</summary>
    public bool Enabled => From(Settings);

    /// <summary>The switch, or on when MCM has not built its settings yet (its Instance is null until its own provider is up).</summary>
    public static bool From(TaomSettings? settings) => settings?.PrisonerBannersEnabled ?? DefaultEnabled;
}
