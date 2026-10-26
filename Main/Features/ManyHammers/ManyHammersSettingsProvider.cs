namespace TAOM.Features.ManyHammers;

/// <summary>
/// The two Many Hammers switches of the "Smithing" MCM group. They are plain bools, so there is nothing to clamp; before MCM is
/// up they read their compiled defaults, which the MCM properties on <see cref="TaomSettings"/> are also initialised from. A
/// smithy click reads them once, so the settings reference is taken lazily and read through.
/// </summary>
public sealed class ManyHammersSettingsProvider : IManyHammersSettingsProvider
{
    public const bool DefaultBulkEnabled = true;
    public const bool DefaultHideLockedEnabled = true;

    private TaomSettings? _settings;

    public ManyHammersSettingsProvider()
    {
    }

    internal ManyHammersSettingsProvider(TaomSettings settings) => _settings = settings;

    // Takes the settings reference once; a null (MCM not up yet) is not cached.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool BulkEnabled => BulkFrom(Settings);

    public bool HideLockedEnabled => HideLockedFrom(Settings);

    internal static bool BulkFrom(TaomSettings? s) => s?.ManyHammersBulkEnabled ?? DefaultBulkEnabled;

    internal static bool HideLockedFrom(TaomSettings? s) => s?.ManyHammersHideLockedEnabled ?? DefaultHideLockedEnabled;
}
