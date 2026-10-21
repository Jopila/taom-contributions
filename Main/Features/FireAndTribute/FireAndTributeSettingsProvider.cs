namespace TAOM.Features.FireAndTribute;

/// <summary>
/// MCM (<c>TaomSettings</c>) over the compiled defaults. Read on every hourly tick while a host rides, so the settings
/// object is taken once in the lazy accessor and read through (CampaignHotPathSettingsProvidersTests), the contract of
/// WatchfulHoldsSettingsProvider. <c>TaomSettings.Instance</c> can be null very early or when MCM fails to load; the
/// <c>?? true</c> fallback keeps every read safe and matches the MCM defaults.
/// </summary>
public sealed class FireAndTributeSettingsProvider : IFireAndTributeSettings
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public FireAndTributeSettingsProvider() { }

    internal FireAndTributeSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool Enabled => Settings?.EnableFireAndTribute ?? true;

    public bool FreeHostsEnabled => Settings?.FireAndTributeFreeHosts ?? true;

    public bool NeutralHostsEnabled => Settings?.FireAndTributeNeutralHosts ?? true;

    public bool EvilHostsEnabled => Settings?.FireAndTributeEvilHosts ?? true;
}
