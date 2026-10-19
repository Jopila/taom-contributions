namespace TAOM.Features.BondsOfBattle;

/// <summary>The Bonds of Battle MCM values after clamping; the one shape every service reads.</summary>
public sealed record BondsOfBattleSettings(
    bool Enabled,
    int AllyBonus,
    bool EnemyPenaltyEnabled,
    int EnemyPenalty,
    bool BanditGratitudeEnabled,
    float BanditGratitudeRadius,
    bool TroopHandoverEnabled,
    int OverpayRelationCap);
