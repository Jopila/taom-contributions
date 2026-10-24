namespace TAOM.Features.RedArrow;

/// <summary>
/// The Red Arrow switch. Read from the Army Management screen on the main thread (a few times per party row when the
/// screen opens, and once per click), never per frame or per tick. It is a row of
/// <c>CampaignHotPathSettingsProvidersTests</c> all the same, because it takes the MCM settings object through one lazy
/// accessor, as its siblings do: a non-null instance only is kept, so a read before MCM is up cannot pin the default, and
/// MCM edits its one registered <see cref="TaomSettings"/> in place, so a change applies at once.
/// </summary>
public sealed class RedArrowSettingsProvider : IRedArrowSettingsProvider
{
    private TaomSettings? _settings;

    public RedArrowSettingsProvider()
    {
    }

    internal RedArrowSettingsProvider(TaomSettings settings) => _settings = settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool Enabled => From(Settings);

    /// <summary>The setting, or on when MCM has not built its settings yet.</summary>
    public static bool From(TaomSettings? settings) => settings?.EnableRedArrow ?? true;
}
