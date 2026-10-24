namespace TAOM.Features.UnlookedForAid;

/// <summary>
/// The Help Unlooked-For switch. <see cref="Enabled"/> is read on the AI worker thread, once per avoid question, so the MCM
/// settings object is taken ONCE, on the main thread: the constructor runs when the model is built in
/// <c>OnGameStart</c> and touches the lazy accessor. After that the getter is one bool read through the cached object (MCM
/// edits its one registered <see cref="TaomSettings"/> in place, so a change applies at once). If MCM was not up at that
/// moment the accessor stays empty and the first read takes it; until then the switch reads on.
/// </summary>
public sealed class UnlookedForAidSettingsProvider : IUnlookedForAidSettingsProvider
{
    private TaomSettings? _settings;

    public UnlookedForAidSettingsProvider()
    {
        _ = Settings;
    }

    internal UnlookedForAidSettingsProvider(TaomSettings settings) => _settings = settings;

    // Keep only a non-null instance so a read before MCM is up cannot pin the default (CampaignHotPathSettingsProvidersTests).
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool Enabled => From(Settings);

    /// <summary>The setting, or on when MCM has not built its settings yet.</summary>
    public static bool From(TaomSettings? settings) => settings?.EnableUnlookedForAid ?? true;
}
