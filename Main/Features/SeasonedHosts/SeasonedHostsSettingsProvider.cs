using System;
using TAOM.Core.Validation;

namespace TAOM.Features.SeasonedHosts;

/// <summary>
/// The "World/Seasoned Hosts" MCM group, validated (#890). The bounds match the slider ranges on <see cref="TaomSettings"/>; they
/// exist for a hand-edited MCM json. A non-finite float reverts to the compiled default. The defaults live here so the property
/// initializers and the fallback before MCM is up cannot drift apart. Registry: docs/reference/gamemodel-registry.md.
/// </summary>
public sealed class SeasonedHostsSettingsProvider : ISeasonedHostsSettingsProvider
{
    public const float MinTrainingBonus = 0f;
    public const float MaxTrainingBonus = 1f;
    public const float DefaultTrainingBonus = 0.25f;

    public const float MinSkillXpMultiplier = 1f;
    public const float MaxSkillXpMultiplier = 2f;
    public const float DefaultSkillXpMultiplier = 1.25f;

    public const bool DefaultLearningNeverStops = true;
    public const bool DefaultNoStewardXp = false;

    private TaomSettings? _settings;

    public SeasonedHostsSettingsProvider()
    {
    }

    internal SeasonedHostsSettingsProvider(TaomSettings settings) => _settings = settings;

    // Takes the settings reference once; a null (MCM not up yet) is not cached.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public float TrainingBonus => Slider(Settings?.SeasonedHostsTrainingBonus, DefaultTrainingBonus, MinTrainingBonus, MaxTrainingBonus);

    public float SkillXpMultiplier => Slider(Settings?.SeasonedHostsSkillXpMultiplier, DefaultSkillXpMultiplier, MinSkillXpMultiplier, MaxSkillXpMultiplier);

    public bool LearningNeverStops => Settings?.SeasonedHostsLearningNeverStops ?? DefaultLearningNeverStops;

    public bool NoStewardXp => Settings?.SeasonedHostsNoStewardXp ?? DefaultNoStewardXp;

    // The slider hands back a float such as 0.2999999 for the 0.30 it shows, so the result is read as the hundredth the
    // "0.00" format displays.
    private static float Slider(float? value, float fallback, float min, float max) =>
        (float)Math.Round(SettingClamp.Clamp(value, fallback, min, max), 2, MidpointRounding.AwayFromZero);
}
