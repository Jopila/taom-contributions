namespace TAOM.Features.WatchfulHolds;

/// <summary>
/// MCM (<c>TaomSettings</c>) over the compiled defaults. Read for every party on every hourly tick, so the settings
/// object is taken once in the lazy accessor and read through (CampaignHotPathSettingsProvidersTests), the contract of
/// AlignmentDesertionSettingsProvider. <c>TaomSettings.Instance</c> can be null very early or when MCM fails to load;
/// the <c>?? true</c> fallback keeps every read safe and matches the MCM defaults.
/// </summary>
public sealed class WatchfulHoldsSettingsProvider : IWatchfulHoldsSettings
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public WatchfulHoldsSettingsProvider() { }

    internal WatchfulHoldsSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool Enabled => Settings?.EnableWatchfulHolds ?? true;

    public bool HarassAiLords => Settings?.WatchfulHoldsHarassAiLords ?? true;
}
