using TAOM.Core.Validation;

namespace TAOM.Features.TokensOfEsteem;

/// <inheritdoc cref="IGiftSettingsProvider"/>
public sealed class GiftSettingsProvider : IGiftSettingsProvider
{
    public const bool DefaultEnabled = true;
    public const int DefaultCooldownDays = 7;
    public const int MinCooldownDays = 1;
    public const int MaxCooldownDays = 60;
    public const int DefaultRelationCap = 15;
    public const int MinRelationCap = 1;
    public const int MaxRelationCap = 30;
    public const bool DefaultAlignmentRefusalEnabled = true;
    /// <summary>A purse is worth one point per <see cref="GiftService.PurseGoldPerPoint"/> gold, so the range is one to ten points.</summary>
    public const int MinPurse = GiftService.PurseGoldPerPoint;
    public const int MaxPurse = GiftService.PurseGoldPerPoint * GiftService.PurseMaxPoints;
    public const int DefaultPurseSmall = 500;
    public const int DefaultPurseMedium = 2000;
    public const int DefaultPurseLarge = 5000;

    private TaomSettings? _settings;

    public GiftSettingsProvider()
    {
    }

    internal GiftSettingsProvider(TaomSettings settings) => _settings = settings;

    // Takes the settings reference once; a null (MCM not up yet) is not cached.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public GiftSettings Get() => From(Settings);

    internal static GiftSettings From(TaomSettings? s) => new(
        s?.GiftsEnabled ?? DefaultEnabled,
        SettingClamp.Clamp(s?.GiftCooldownDays, DefaultCooldownDays, MinCooldownDays, MaxCooldownDays),
        SettingClamp.Clamp(s?.GiftRelationCap, DefaultRelationCap, MinRelationCap, MaxRelationCap),
        s?.GiftAlignmentRefusal ?? DefaultAlignmentRefusalEnabled,
        SettingClamp.Clamp(s?.GiftPurseSmall, DefaultPurseSmall, MinPurse, MaxPurse),
        SettingClamp.Clamp(s?.GiftPurseMedium, DefaultPurseMedium, MinPurse, MaxPurse),
        SettingClamp.Clamp(s?.GiftPurseLarge, DefaultPurseLarge, MinPurse, MaxPurse));
}
