using TAOM.Core.Validation;

namespace TAOM.Features.BondsOfBattle;

/// <summary>
/// The "Bonds of Battle" MCM group, clamped. The bounds match the slider ranges on <see cref="TaomSettings"/>; they exist
/// for a hand-edited MCM json, which MCM deserializes verbatim. The defaults live here so the property initializers and the
/// fallback before MCM is up cannot drift apart. Each battle, hand-over and barter reads it once, none per frame, so the
/// settings reference is taken lazily and read through.
/// </summary>
public sealed class BondsOfBattleSettingsProvider : IBondsOfBattleSettingsProvider
{
    public const bool DefaultEnabled = true;
    public const int DefaultAllyBonus = 1;
    public const int MinAllyBonus = 0;
    public const int MaxAllyBonus = 5;
    public const bool DefaultEnemyPenaltyEnabled = false;
    public const int DefaultEnemyPenalty = 5;
    public const int MinEnemyPenalty = 0;
    public const int MaxEnemyPenalty = 10;
    public const bool DefaultBanditGratitudeEnabled = true;

    /// <summary>
    /// In map units. TAOM_Map's settlements sit a median 17 units from their nearest neighbour and a median of 3 lie
    /// within 30 (measured on the live settlements.xml, 2026-10-18), so 25 reaches the one to three settlements around a
    /// battle. It can reach another realm's villages: settlements of different kingdoms lie as close as 14 units apart.
    /// </summary>
    public const float DefaultBanditGratitudeRadius = 25f;
    public const float MinBanditGratitudeRadius = 5f;
    public const float MaxBanditGratitudeRadius = 60f;
    public const bool DefaultTroopHandoverEnabled = true;

    /// <summary>The loop buys at most 3 points in vanilla (the setting here), before the Tribute perk (x1.2, rounded up) and the Charm relation bonus the engine adds when it applies the change.</summary>
    public const int DefaultOverpayRelationCap = 15;
    public const int MinOverpayRelationCap = 3;
    public const int MaxOverpayRelationCap = 30;

    private TaomSettings? _settings;

    public BondsOfBattleSettingsProvider()
    {
    }

    internal BondsOfBattleSettingsProvider(TaomSettings settings) => _settings = settings;

    // Takes the settings reference once; a null (MCM not up yet) is not cached.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public BondsOfBattleSettings Get() => From(Settings);

    internal static BondsOfBattleSettings From(TaomSettings? s) => new(
        s?.BondsOfBattleEnabled ?? DefaultEnabled,
        SettingClamp.Clamp(s?.BondsAllyBonus, DefaultAllyBonus, MinAllyBonus, MaxAllyBonus),
        s?.BondsEnemyPenaltyEnabled ?? DefaultEnemyPenaltyEnabled,
        SettingClamp.Clamp(s?.BondsEnemyPenalty, DefaultEnemyPenalty, MinEnemyPenalty, MaxEnemyPenalty),
        s?.BondsBanditGratitudeEnabled ?? DefaultBanditGratitudeEnabled,
        SettingClamp.Clamp(s?.BondsBanditGratitudeRadius, DefaultBanditGratitudeRadius, MinBanditGratitudeRadius, MaxBanditGratitudeRadius),
        s?.BondsTroopHandoverEnabled ?? DefaultTroopHandoverEnabled,
        SettingClamp.Clamp(s?.BondsOverpayRelationCap, DefaultOverpayRelationCap, MinOverpayRelationCap, MaxOverpayRelationCap));
}
