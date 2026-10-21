namespace TAOM.Features.Relations;

/// <summary>
/// A hero who could be granted relation, as raw facts. <see cref="RelationOwnerId"/> is the hero whose relation with the
/// player a change to this one really moves (<c>GetHeroesForEffectiveRelation</c>: the clan leader unless personal relations
/// are on), and it is the de-duplication key. Whether a dead hero or the player earns anything is the grant's decision
/// (<see cref="RelationGrantServiceExtensions.GrantStrongestPerOwner"/>).
/// </summary>
public sealed record RelationTarget(string HeroId, string RelationOwnerId, bool IsAlive, bool IsPlayer);

/// <summary>
/// One request for relation: a signed change (positive earns, negative costs) with <see cref="Target"/>. A null target is
/// skipped, which lets a caller list an optional hero (no spouse, no clan leader) without a branch.
/// </summary>
public readonly record struct RelationAsk(RelationTarget? Target, int Delta);
