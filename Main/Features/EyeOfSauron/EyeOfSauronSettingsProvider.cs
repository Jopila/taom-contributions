using System;
using TAOM.Core.Validation;

namespace TAOM.Features.EyeOfSauron;

/// <summary>The Eye of Sauron hunt's MCM knobs (#765 M8), validated.</summary>
public interface IEyeOfSauronSettings
{
    bool Enabled { get; }

    /// <summary>The decayed deed score that draws a hunt.</summary>
    float Threshold { get; }

    /// <summary>Days for a deed score to halve.</summary>
    float HalfLifeDays { get; }

    int CooldownDays { get; }

    int HuntTimeoutDays { get; }

    /// <summary>The hunter's speed floor in sight, as a multiple of the player's speed.</summary>
    float HunterSpeedMultiplier { get; }

    /// <summary>The host's target strength, as a multiple of the player's.</summary>
    float HunterStrengthRatio { get; }

    int HunterMaxTroops { get; }

    /// <summary>The longest single out-of-sight step, in map units.</summary>
    float HunterStepCap { get; }
}

/// <summary>
/// MCM (<c>TaomSettings</c>) over the compiled defaults. A stale settings file can hold anything, so every float is
/// checked for NaN and infinity first (a non-finite value reverts to the default) and then clamped into its slider
/// range; ints are clamped. The multiplier is read every campaign frame while a hunt runs, so the settings object is
/// taken once in the lazy accessor and read through (CampaignHotPathSettingsProvidersTests).
/// </summary>
public sealed class EyeOfSauronSettingsProvider : IEyeOfSauronSettings
{
    public const float DefaultThreshold = 10f;
    public const float MinThreshold = 1f;
    public const float MaxThreshold = 100f;
    public const float DefaultHalfLifeDays = 30f;
    public const float MinHalfLifeDays = 1f;
    public const float MaxHalfLifeDays = 365f;
    public const int DefaultCooldownDays = 60;
    public const int MaxCooldownDays = 365;
    public const int DefaultHuntTimeoutDays = 20;
    public const int MaxHuntTimeoutDays = 120;
    public const float DefaultSpeedMultiplier = 1.3f;
    public const float MinSpeedMultiplier = 1.1f;
    public const float MaxSpeedMultiplier = 2.0f;
    public const float DefaultStrengthRatio = 1.3f;
    public const float MinStrengthRatio = 0.5f;
    public const float MaxStrengthRatio = 3.0f;
    public const int DefaultMaxTroops = 250;
    public const int MinMaxTroops = 20;
    public const int MaxMaxTroops = 1000;
    public const float DefaultStepCap = 15f;
    public const float MinStepCap = 1f;
    public const float MaxStepCap = 50f;

    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public EyeOfSauronSettingsProvider() { }

    internal EyeOfSauronSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool Enabled => Settings?.EyeOfSauronEnabled ?? true;

    public float Threshold => Clamp(Settings?.EyeThreshold, MinThreshold, MaxThreshold, DefaultThreshold);

    public float HalfLifeDays => Clamp(Settings?.EyeHalfLifeDays, MinHalfLifeDays, MaxHalfLifeDays, DefaultHalfLifeDays);

    public int CooldownDays => Clamp(Settings?.EyeCooldownDays ?? DefaultCooldownDays, 0, MaxCooldownDays);

    public int HuntTimeoutDays => Clamp(Settings?.HuntTimeoutDays ?? DefaultHuntTimeoutDays, 1, MaxHuntTimeoutDays);

    public float HunterSpeedMultiplier =>
        Clamp(Settings?.HunterSpeedMultiplier, MinSpeedMultiplier, MaxSpeedMultiplier, DefaultSpeedMultiplier);

    public float HunterStrengthRatio =>
        Clamp(Settings?.HunterStrengthRatio, MinStrengthRatio, MaxStrengthRatio, DefaultStrengthRatio);

    public int HunterMaxTroops => Clamp(Settings?.HunterMaxTroops ?? DefaultMaxTroops, MinMaxTroops, MaxMaxTroops);

    public float HunterStepCap => Clamp(Settings?.HunterStepCap, MinStepCap, MaxStepCap, DefaultStepCap);

    /// <summary>Non-finite reverts to the default BEFORE the range check (a NaN passes every comparison); then clamps.</summary>
    internal static float Clamp(float? raw, float min, float max, float fallback)
    {
        if (!raw.HasValue || !FiniteFloatValidator.IsFinite(raw.Value))
            return fallback;
        return Math.Min(Math.Max(raw.Value, min), max);
    }

    private static int Clamp(int raw, int min, int max) => Math.Min(Math.Max(raw, min), max);
}
