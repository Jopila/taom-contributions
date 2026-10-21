namespace TAOM.Features.Namegiving;

/// <summary>
/// The MCM "Fief Management/Namegiving" switch, one for both halves of the feature: the Rename button and the lore names that
/// follow a capture. Read when the kingdom screen opens and when a capture, a change of realm or a session start reconciles a lore name, never per frame.
/// </summary>
public sealed class NamegivingSettingsProvider
{
    private TaomSettings? _settings;

    public NamegivingSettingsProvider()
    {
    }

    internal NamegivingSettingsProvider(TaomSettings settings) => _settings = settings;

    // Take the settings object once and keep only a non-null instance, so a read before MCM is up cannot pin the default
    // (CampaignHotPathSettingsProvidersTests).
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    /// <summary>True when the Rename button shows and the lore names apply.</summary>
    public bool Enabled => From(Settings);

    /// <summary>The switch, or on when MCM has not built its settings yet (its Instance is null until its own provider is up).</summary>
    public static bool From(TaomSettings? settings) => settings?.EnableNamegiving ?? true;
}
