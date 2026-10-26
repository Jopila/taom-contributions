namespace TAOM.Features.HarvestRoad;

/// <summary>
/// The Harvest Road switch. Read on the main thread when a villager reaches a town and when an AI party in a town
/// might buy food (once per party per hour), so it is a row of <c>CampaignHotPathSettingsProvidersTests</c>: it takes the
/// MCM settings object through one lazy accessor, as its siblings do. A non-null instance only is kept, so a read before
/// MCM is up cannot pin the default, and MCM edits its one registered <see cref="TaomSettings"/> in place, so a change
/// applies at once.
/// </summary>
public sealed class HarvestRoadSettingsProvider : IHarvestRoadSettingsProvider
{
    private TaomSettings? _settings;

    public HarvestRoadSettingsProvider()
    {
    }

    internal HarvestRoadSettingsProvider(TaomSettings settings) => _settings = settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool Enabled => From(Settings);

    /// <summary>The setting, or on when MCM has not built its settings yet.</summary>
    public static bool From(TaomSettings? settings) => settings?.EnableHarvestRoadNotices ?? true;
}
