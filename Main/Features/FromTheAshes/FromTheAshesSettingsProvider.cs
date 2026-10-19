namespace TAOM.Features.FromTheAshes;

/// <summary>The MCM "Fief Management/From the Ashes" switch. It hides only the two START options, never a running job.</summary>
public interface IFromTheAshesSettingsProvider
{
    /// <summary>
    /// True when the village-rebuild and project-help options may show. The host menus are plain menus, not wait menus, so
    /// the condition runs when a menu opens or refreshes, not every frame. The accessor still takes the settings object
    /// once: the read is cheap and matches the house rule for a provider a menu condition calls
    /// (CampaignHotPathSettingsProvidersTests). An MCM edit applies at the next refresh.
    /// </summary>
    bool Enabled { get; }
}

public sealed class FromTheAshesSettingsProvider : IFromTheAshesSettingsProvider
{
    private TaomSettings? _settings;

    public FromTheAshesSettingsProvider()
    {
    }

    internal FromTheAshesSettingsProvider(TaomSettings settings) => _settings = settings;

    // Take the settings object once and keep only a non-null instance, so a read before MCM is up cannot pin the default.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool Enabled => From(Settings);

    /// <summary>The setting, or on when MCM has not built its settings yet (its Instance is null until its own provider is up).</summary>
    public static bool From(TaomSettings? settings) => settings?.EnableFromTheAshes ?? true;
}
