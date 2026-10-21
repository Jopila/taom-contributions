using System;
using TAOM.Core.Validation;
using TAOM.Features.Relations;

namespace TAOM.Features.GoodDeeds;

/// <summary>
/// The "Good Deeds" MCM group, clamped. The bounds match the slider ranges on <see cref="TaomSettings"/>; they exist for a
/// hand-edited MCM json, which MCM deserializes verbatim, and a non-finite float reverts to the compiled default. The defaults
/// live here so the property initializers and the fallback before MCM is up cannot drift apart. Each deed reads it once, so
/// the settings reference is taken lazily and read through.
/// </summary>
public sealed class GoodDeedsSettingsProvider : IGoodDeedsSettingsProvider
{
    public const bool DefaultEnabled = true;
    public const bool DefaultReleaseEnabled = true;
    public const bool DefaultDonationEnabled = true;
    public const bool DefaultTournamentEnabled = true;

    public const int DefaultReleaseLordAmount = 5;
    public const int DefaultReleaseSpouseAmount = 2;
    public const int DefaultReleaseLeaderAmount = 2;
    public const int DefaultDonationAmount = 5;
    public const int DefaultTournamentAmount = 1;
    public const int MinAmount = 0;
    public const int MaxAmount = 20;
    public const int MaxTournamentAmount = 10;

    public const float MinMultiplier = 0f;
    public const float MaxMultiplier = 5f;
    public const float DefaultReleaseSame = 1f;
    public const float DefaultReleaseNeutral = 0.75f;
    public const float DefaultReleaseOpposite = 0.5f;
    public const float DefaultDonationSame = 0f;
    public const float DefaultDonationNeutral = 1f;
    public const float DefaultDonationOpposite = 2f;
    public const float DefaultTournamentSame = 1f;
    public const float DefaultTournamentNeutral = 1f;
    public const float DefaultTournamentOpposite = 0f;

    private TaomSettings? _settings;

    public GoodDeedsSettingsProvider()
    {
    }

    internal GoodDeedsSettingsProvider(TaomSettings settings) => _settings = settings;

    // Takes the settings reference once; a null (MCM not up yet) is not cached.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public GoodDeedsSettings Get() => From(Settings);

    internal static GoodDeedsSettings From(TaomSettings? s) => new(
        s?.GoodDeedsEnabled ?? DefaultEnabled,
        s?.GoodDeedsReleaseEnabled ?? DefaultReleaseEnabled,
        s?.GoodDeedsDonationEnabled ?? DefaultDonationEnabled,
        s?.GoodDeedsTournamentEnabled ?? DefaultTournamentEnabled,
        SettingClamp.Clamp(s?.GoodDeedsReleaseLordAmount, DefaultReleaseLordAmount, MinAmount, MaxAmount),
        SettingClamp.Clamp(s?.GoodDeedsReleaseSpouseAmount, DefaultReleaseSpouseAmount, MinAmount, MaxAmount),
        SettingClamp.Clamp(s?.GoodDeedsReleaseLeaderAmount, DefaultReleaseLeaderAmount, MinAmount, MaxAmount),
        SettingClamp.Clamp(s?.GoodDeedsDonationAmount, DefaultDonationAmount, MinAmount, MaxAmount),
        SettingClamp.Clamp(s?.GoodDeedsTournamentAmount, DefaultTournamentAmount, MinAmount, MaxTournamentAmount),
        new SideMultipliers(
            Multiplier(s?.GoodDeedsReleaseSame, DefaultReleaseSame),
            Multiplier(s?.GoodDeedsReleaseNeutral, DefaultReleaseNeutral),
            Multiplier(s?.GoodDeedsReleaseOpposite, DefaultReleaseOpposite)),
        new SideMultipliers(
            Multiplier(s?.GoodDeedsDonationSame, DefaultDonationSame),
            Multiplier(s?.GoodDeedsDonationNeutral, DefaultDonationNeutral),
            Multiplier(s?.GoodDeedsDonationOpposite, DefaultDonationOpposite)),
        new SideMultipliers(
            Multiplier(s?.GoodDeedsTournamentSame, DefaultTournamentSame),
            Multiplier(s?.GoodDeedsTournamentNeutral, DefaultTournamentNeutral),
            Multiplier(s?.GoodDeedsTournamentOpposite, DefaultTournamentOpposite)));

    // The slider hands back a float such as 0.6999999 for the 0.7 it shows, which scales 5 to 3 once the half rounds down. A
    // multiplier is read as the hundredth the slider's "0.00" format displays.
    private static float Multiplier(float? value, float fallback) =>
        (float)Math.Round(SettingClamp.Clamp(value, fallback, MinMultiplier, MaxMultiplier), 2, MidpointRounding.AwayFromZero);
}
