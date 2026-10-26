namespace TAOM.Features.KeepWardens;

public sealed class CastlePatrolSettingsProvider : ICastlePatrolSettingsProvider
{
    private TaomSettings? _settings;

    public CastlePatrolSettingsProvider()
    {
    }

    internal CastlePatrolSettingsProvider(TaomSettings settings) => _settings = settings;

    // The patrol queue asks for every settlement it visits; take the settings object once (CampaignHotPathSettingsProvidersTests)
    // and keep only a non-null instance so a read before MCM is up cannot pin the default.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool Enabled => From(Settings);

    public bool BuildPriorityEnabled => BuildPriorityFrom(Settings);

    /// <summary>The build priority setting, or on when MCM has not built its settings yet.</summary>
    public static bool BuildPriorityFrom(TaomSettings? settings) => settings?.EnableGuardHouseBuildPriority ?? true;

    /// <summary>The setting, or on when MCM has not built its settings yet (its Instance is null until its own provider is up).</summary>
    public static bool From(TaomSettings? settings) => settings?.EnableCastlePatrols ?? true;
}
