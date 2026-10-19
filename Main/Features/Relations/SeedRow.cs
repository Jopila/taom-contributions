namespace TAOM.Features.Relations;

/// <summary>
/// One living lord as the seeding sees him, by id and raw numbers. <see cref="Relation"/> is the player's raw stored relation
/// with this lord (<c>CharacterRelationManager.GetHeroRelation</c>), not the effective one. <see cref="ClanId"/> is null for a
/// hero with no clan.
/// </summary>
public sealed record SeedRow(string HeroId, string? ClanId, bool IsClanLeader, bool IsPlayerClan, int Relation);

/// <summary>One raw relation the seeding will write.</summary>
public sealed record SeedChange(string HeroId, int Value);
