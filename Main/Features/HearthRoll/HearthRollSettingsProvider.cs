namespace TAOM.Features.HearthRoll;

/// <summary>
/// The MCM "Hearth Roll" switch for the red tint on an unhappy companion's row of the clan screen's Members list. Read when a row
/// refreshes (the clan screen opening, or the list rebuilding after an action such as a recall), never per frame.
/// </summary>
public sealed class HearthRollSettingsProvider
{
    private TaomSettings? _settings;

    public HearthRollSettingsProvider()
    {
    }

    internal HearthRollSettingsProvider(TaomSettings settings) => _settings = settings;

    // Take the settings object once and keep only a non-null instance, so a read before MCM is up cannot pin the default
    // (CampaignHotPathSettingsProvidersTests).
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    /// <summary>True when an unhappy companion's row is tinted.</summary>
    public bool Enabled => From(Settings);

    /// <summary>The switch, or on when MCM has not built its settings yet (its Instance is null until its own provider is up).</summary>
    public static bool From(TaomSettings? settings) => settings?.EnableUnhappyCompanionTint ?? true;
}
