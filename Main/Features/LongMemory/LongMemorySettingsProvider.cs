using System;
using TAOM.Core.Validation;
using TAOM.Features.Relations;

namespace TAOM.Features.LongMemory;

/// <summary>
/// The "Long Memory" MCM group, clamped. The bounds match the slider ranges on <see cref="TaomSettings"/>; they exist for a
/// hand-edited MCM json, which MCM deserializes verbatim, and a non-finite float reverts to the compiled default. An amount
/// below 0 is 0, because the sliders hold magnitudes and the service negates them. The defaults live here so the property
/// initializers and the fallback before MCM is up cannot drift apart. Each raid or forced taking reads it once, so the settings
/// reference is taken lazily and read through.
/// </summary>
public sealed class LongMemorySettingsProvider : ILongMemorySettingsProvider
{
    public const bool DefaultEnabled = true;
    public const bool DefaultRaidEnabled = true;
    public const bool DefaultCoercionEnabled = true;

    public const int DefaultRaidOwnerAmount = 50;
    public const int DefaultRaidNotableAmount = 50;
    public const int DefaultCoercionOwnerAmount = 20;
    public const int DefaultCoercionNotableAmount = 25;
    public const int MinAmount = 0;
    public const int MaxRaidAmount = 100;
    public const int MaxCoercionAmount = 50;

    public const float MinMultiplier = 0f;
    public const float MaxMultiplier = 5f;
    public const float DefaultSame = 1f;
    public const float DefaultNeutral = 0.5f;
    public const float DefaultOpposite = 0.1f;

    private TaomSettings? _settings;

    public LongMemorySettingsProvider()
    {
    }

    internal LongMemorySettingsProvider(TaomSettings settings) => _settings = settings;

    // Takes the settings reference once; a null (MCM not up yet) is not cached.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public LongMemorySettings Get() => From(Settings);

    internal static LongMemorySettings From(TaomSettings? s) => new(
        s?.LongMemoryEnabled ?? DefaultEnabled,
        s?.LongMemoryRaidEnabled ?? DefaultRaidEnabled,
        s?.LongMemoryCoercionEnabled ?? DefaultCoercionEnabled,
        SettingClamp.Clamp(s?.LongMemoryRaidOwnerAmount, DefaultRaidOwnerAmount, MinAmount, MaxRaidAmount),
        SettingClamp.Clamp(s?.LongMemoryRaidNotableAmount, DefaultRaidNotableAmount, MinAmount, MaxRaidAmount),
        SettingClamp.Clamp(s?.LongMemoryCoercionOwnerAmount, DefaultCoercionOwnerAmount, MinAmount, MaxCoercionAmount),
        SettingClamp.Clamp(s?.LongMemoryCoercionNotableAmount, DefaultCoercionNotableAmount, MinAmount, MaxCoercionAmount),
        new SideMultipliers(
            Multiplier(s?.LongMemorySame, DefaultSame),
            Multiplier(s?.LongMemoryNeutral, DefaultNeutral),
            Multiplier(s?.LongMemoryOpposite, DefaultOpposite)));

    // The slider hands back a float such as 0.6999999 for the 0.7 it shows, which scales 50 to 35 only once it is read as the
    // hundredth the slider's "0.00" format displays.
    private static float Multiplier(float? value, float fallback) =>
        (float)Math.Round(SettingClamp.Clamp(value, fallback, MinMultiplier, MaxMultiplier), 2, MidpointRounding.AwayFromZero);
}
