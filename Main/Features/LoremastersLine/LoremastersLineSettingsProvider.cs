namespace TAOM.Features.LoremastersLine;

/// <summary>
/// The MCM "Character Screen/Loremaster's Line" switch (#877). It is read once per skill view model, when the character
/// screen builds it, never per click or frame. An edit applies the next time the screen opens.
/// </summary>
public sealed class LoremastersLineSettingsProvider
{
    private TaomSettings? _settings;

    public LoremastersLineSettingsProvider()
    {
    }

    internal LoremastersLineSettingsProvider(TaomSettings settings) => _settings = settings;

    // Take the settings object once and keep only a non-null instance, so a read before MCM is up cannot pin the default
    // (CampaignHotPathSettingsProvidersTests).
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    /// <summary>True when the skill bar shows the line and the learning-limit tooltip gains its extra line.</summary>
    public bool Enabled => EnabledFrom(Settings);

    /// <summary>The switch, or on when MCM has not built its settings yet (its Instance is null until its own provider is up).</summary>
    public static bool EnabledFrom(TaomSettings? settings) => settings?.EnableLoremastersLine ?? true;
}
