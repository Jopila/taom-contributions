namespace TAOM.Features.PosternGate;

/// <summary>The MCM "Fief Management/Postern Gate" switch. It gates only the loss cut, never registration or seeding.</summary>
public interface IPosternGateSettingsProvider
{
    /// <summary>
    /// True when the Postern Gate cuts troop losses when breaking into or out of a siege. Read at every break-out check
    /// (each frame in the siege menu), so an MCM edit applies at once.
    /// </summary>
    bool CutEnabled { get; }
}

public sealed class PosternGateSettingsProvider : IPosternGateSettingsProvider
{
    private TaomSettings? _settings;

    public PosternGateSettingsProvider()
    {
    }

    internal PosternGateSettingsProvider(TaomSettings settings) => _settings = settings;

    // The siege wait menu re-checks break-out options every frame; take the settings object once (CampaignHotPathSettingsProvidersTests);
    // keep only a non-null instance so a read before MCM is up cannot pin the default.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool CutEnabled => From(Settings);

    /// <summary>The setting, or on when MCM has not built its settings yet (its Instance is null until its own provider is up).</summary>
    public static bool From(TaomSettings? settings) => settings?.EnablePosternGate ?? true;
}
