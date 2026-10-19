namespace TAOM.Features.Relations;

/// <summary>
/// The "Personal Relations" MCM group, clamped. The bounds match the slider ranges on <see cref="TaomSettings"/> and exist for
/// a hand-edited MCM json, which MCM deserializes verbatim. A leader range given the wrong way round is swapped, not clamped,
/// so a player who typed 40 and 30 gets 30 to 40. The defaults live here so the property initializers and the fallback
/// before MCM is up cannot drift apart. <see cref="Enabled"/> is read on every relation read, so the settings reference is
/// taken once, lazily, and read through (the settings pattern of <c>CampaignHotPathSettingsProvidersTests</c>).
/// </summary>
public sealed class PersonalRelationSettingsProvider : IPersonalRelationSettingsProvider
{
    public const bool DefaultEnabled = true;
    public const bool DefaultDisbandCostEnabled = true;
    public const int MinCost = 0;
    public const int MaxCost = 50;
    public const int DefaultLeaderMin = 28;
    public const int DefaultLeaderMax = 32;

    private TaomSettings? _settings;

    public PersonalRelationSettingsProvider()
    {
    }

    internal PersonalRelationSettingsProvider(TaomSettings settings) => _settings = settings;

    // Takes the settings reference once; a null (MCM not up yet) is not cached.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool Enabled => Settings?.PersonalRelationsEnabled ?? DefaultEnabled;

    public PersonalRelationSettings Get() => From(Settings);

    internal static PersonalRelationSettings From(TaomSettings? s)
    {
        var leaderA = Core.Validation.SettingClamp.Clamp(s?.PersonalDisbandLeaderMin, DefaultLeaderMin, MinCost, MaxCost);
        var leaderB = Core.Validation.SettingClamp.Clamp(s?.PersonalDisbandLeaderMax, DefaultLeaderMax, MinCost, MaxCost);
        return new PersonalRelationSettings(
            s?.PersonalDisbandCostEnabled ?? DefaultDisbandCostEnabled,
            System.Math.Min(leaderA, leaderB), System.Math.Max(leaderA, leaderB));
    }
}
