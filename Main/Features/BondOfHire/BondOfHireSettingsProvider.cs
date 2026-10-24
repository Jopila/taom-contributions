using TAOM.Core.Validation;

namespace TAOM.Features.BondOfHire;

/// <summary>
/// The "Bond of Hire" MCM group, clamped. The bounds match the slider range on <see cref="TaomSettings"/>; they exist for a
/// hand-edited MCM json, which MCM deserializes verbatim. The default lives here so the property initializer and the fallback
/// before MCM is up cannot drift apart. A clan signing on reads it once, so the settings reference is taken lazily and read through.
/// </summary>
public sealed class BondOfHireSettingsProvider : IBondOfHireSettingsProvider
{
    public const int DefaultMinimumDays = 30;
    public const int MinDays = 0;
    public const int MaxDays = 120;

    private TaomSettings? _settings;

    public BondOfHireSettingsProvider()
    {
    }

    internal BondOfHireSettingsProvider(TaomSettings settings) => _settings = settings;

    // Takes the settings reference once; a null (MCM not up yet) is not cached.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public int MinimumDays => From(Settings);

    internal static int From(TaomSettings? s) =>
        SettingClamp.Clamp(s?.BondOfHireMinimumDays, DefaultMinimumDays, MinDays, MaxDays);
}
