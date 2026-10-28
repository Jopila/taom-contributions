namespace TAOM.Features.WardensTally;

/// <summary>
/// The MCM switch for the Warden's Tally line in the town and castle dungeon menus (#903).
/// </summary>
public sealed class WardensTallySettingsProvider
{
    /// <summary>The shipped default, shared with the MCM property and the fallback before MCM is up.</summary>
    public const bool DefaultEnabled = true;

    private TaomSettings? _settings;

    public WardensTallySettingsProvider()
    {
    }

    internal WardensTallySettingsProvider(TaomSettings settings) => _settings = settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    /// <summary>True when the dungeon menu adds the held count, the lords' escape odds and the boost lines.</summary>
    public bool Enabled => From(Settings);

    /// <summary>The switch, or on when MCM has not built its settings yet (its Instance is null until its own provider is up).</summary>
    public static bool From(TaomSettings? settings) => settings?.WardensTallyEnabled ?? DefaultEnabled;
}
