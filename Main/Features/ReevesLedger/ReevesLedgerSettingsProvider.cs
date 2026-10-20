namespace TAOM.Features.ReevesLedger;

/// <summary>
/// The two MCM "Fief Management/Reeve's Ledger" switches. Both are read once, when the town management window opens
/// (the view model's constructor), so an edit applies the next time the window opens and nothing reads them per frame.
/// </summary>
public sealed class ReevesLedgerSettingsProvider
{
    private TaomSettings? _settings;

    public ReevesLedgerSettingsProvider()
    {
    }

    internal ReevesLedgerSettingsProvider(TaomSettings settings) => _settings = settings;

    // Take the settings object once and keep only a non-null instance, so a read before MCM is up cannot pin the default
    // (CampaignHotPathSettingsProvidersTests).
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    /// <summary>True when the window shows the fief's name as its title, the Profit and Patrol rows and the stash and prison icons.</summary>
    public bool LedgerEnabled => LedgerFrom(Settings);

    /// <summary>True when the reserve panel shows the +10K and -10K buttons (single-player only, on top of this switch).</summary>
    public bool StepsEnabled => StepsFrom(Settings);

    /// <summary>The ledger switch, or on when MCM has not built its settings yet (its Instance is null until its own provider is up).</summary>
    public static bool LedgerFrom(TaomSettings? settings) => settings?.EnableReevesLedger ?? true;

    /// <summary>The step-button switch, or on when MCM has not built its settings yet.</summary>
    public static bool StepsFrom(TaomSettings? settings) => settings?.EnableReserveSteps ?? true;
}
