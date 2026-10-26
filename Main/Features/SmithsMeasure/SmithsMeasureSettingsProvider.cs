using TAOM.Core.Logging;

namespace TAOM.Features.SmithsMeasure;

/// <summary>
/// The "Smithing" MCM values of Smith's Measure (#910). The bounds match the slider on <see cref="TaomSettings"/>; they exist for
/// a hand-edited MCM json, which MCM deserializes verbatim. A cap outside them reads as the default with one warning per
/// distinct bad value, not one per read. The defaults live here so the property initializers and the fallback before MCM is up
/// cannot drift apart. The smelting list asks once per item, so the settings reference is taken lazily and read through.
/// </summary>
public sealed class SmithsMeasureSettingsProvider : ISmithsMeasureSettingsProvider
{
    public const bool DefaultCapSmelting = true;
    public const int DefaultYieldCap = 1;
    public const int MinYieldCap = 1;
    public const int MaxYieldCap = 10;
    public const bool DefaultMakeTools = true;

    private readonly IModLogger _logger;
    private TaomSettings? _settings;
    private int? _warnedCap;

    public SmithsMeasureSettingsProvider(IModLogger logger) => _logger = logger;

    internal SmithsMeasureSettingsProvider(TaomSettings settings, IModLogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    // Takes the settings reference once; a null (MCM not up yet) is not cached.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool CapSmelting => CapSmeltingFrom(Settings);

    public int YieldCap => YieldCapFrom(Settings);

    public bool MakeToolsEnabled => MakeToolsFrom(Settings);

    internal static bool CapSmeltingFrom(TaomSettings? s) => s?.SmithsMeasureCapSmelting ?? DefaultCapSmelting;

    internal static bool MakeToolsFrom(TaomSettings? s) => s?.SmithsMeasureMakeTools ?? DefaultMakeTools;

    internal int YieldCapFrom(TaomSettings? s)
    {
        var cap = s?.SmithsMeasureYieldCap ?? DefaultYieldCap;
        if (cap >= MinYieldCap && cap <= MaxYieldCap)
            return cap;

        if (_warnedCap != cap)
        {
            _warnedCap = cap;
            _logger.LogWarning(
                $"[SmithsMeasure] Smelting Yield Cap {cap} is outside {MinYieldCap} to {MaxYieldCap}; using {DefaultYieldCap} instead.");
        }

        return DefaultYieldCap;
    }
}
