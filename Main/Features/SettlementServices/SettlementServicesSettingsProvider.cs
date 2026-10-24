namespace TAOM.Features.SettlementServices;

/// <summary>
/// The two MCM switches of Watch-Silver and Tidings of the Lists (#859, docs/features/settlement-services.md), each in the group a
/// player looks in. Read when a conversation or a menu is built and when a line is paid for, never per frame.
/// </summary>
public sealed class SettlementServicesSettingsProvider
{
    private TaomSettings? _settings;

    public SettlementServicesSettingsProvider()
    {
    }

    internal SettlementServicesSettingsProvider(TaomSettings settings) => _settings = settings;

    // Take the settings object once and keep only a non-null instance, so a read before MCM is up cannot pin the default
    // (CampaignHotPathSettingsProvidersTests).
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    /// <summary>True when the governor's silver line is offered.</summary>
    public bool WatchSilverEnabled => WatchSilverFrom(Settings);

    /// <summary>True when the arena's tournament question is offered.</summary>
    public bool TidingsEnabled => TidingsFrom(Settings);

    /// <summary>The Watch-Silver switch, or on when MCM has not built its settings yet.</summary>
    public static bool WatchSilverFrom(TaomSettings? settings) => settings?.EnableWatchSilver ?? true;

    /// <summary>The Tidings switch, or on when MCM has not built its settings yet.</summary>
    public static bool TidingsFrom(TaomSettings? settings) => settings?.EnableTidingsOfTheLists ?? true;
}
