namespace TAOM.Features.TokensOfEsteem;

/// <summary>
/// The "Tokens of Esteem" MCM group after clamping. <see cref="RelationCap"/> is the most relation one gift asks for (the
/// player's Charm can add to what lands). The three purses are gold amounts, offered to companions only.
/// </summary>
public sealed record GiftSettings(
    bool Enabled,
    int CooldownDays,
    int RelationCap,
    bool AlignmentRefusalEnabled,
    int PurseSmall,
    int PurseMedium,
    int PurseLarge);
