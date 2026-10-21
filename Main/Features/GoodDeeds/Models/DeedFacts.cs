using TAOM.Features.Execution;

namespace TAOM.Features.GoodDeeds.Models;

/// <summary>
/// A hero who could be granted relation, as raw facts. <see cref="RelationOwnerId"/> is the hero whose relation with the
/// player a change to this one really moves (<c>GetHeroesForEffectiveRelation</c>: the clan leader unless personal relations
/// are on), and it is the de-duplication key. Whether a dead hero or the player earns anything is the service's decision.
/// </summary>
public sealed record DeedHero(string HeroId, string RelationOwnerId, bool IsAlive, bool IsPlayer);

/// <summary>
/// A prisoner the player just released, as raw facts. <see cref="Spouse"/> is <c>Hero.Spouse</c> and
/// <see cref="ClanLeader"/> the leader of the lord's clan; each is null when there is none (no spouse, no clan).
/// <see cref="Player"/> and <see cref="LordSide"/> are the sides of the player's map faction and the lord's.
/// </summary>
public sealed record ReleaseFacts(
    SideIds Player,
    SideIds LordSide,
    bool IsLord,
    DeedHero Lord,
    DeedHero? Spouse,
    DeedHero? ClanLeader);

/// <summary>
/// What the party named by a <c>HeroPrisonerReleased</c> event says about who released the prisoner, as three raw facts read by
/// the adapter (the service decides which of them pays). <see cref="IsMainParty"/>: the party is the player's own, a release in
/// conversation. <see cref="IsPlayerPartyScreen"/>: the event carries no party and the player's own party screen is open with
/// the main party as its right owner, which is how Done (or Talk then "Apply Changes") on that screen arrives. <see cref="IsPlayerClanSettlement"/>:
/// the party is a settlement whose owner clan is the player's, the "let go" talk in the player's own dungeon. A player-clan
/// mobile party other than the main party is none of the three, deliberately: the engine releases a removed party's hero
/// prisoners by choice. The default value is all false.
/// </summary>
public readonly record struct ReleaseSource(bool IsMainParty, bool IsPlayerPartyScreen, bool IsPlayerClanSettlement)
{
    public static readonly ReleaseSource None = default;
}

/// <summary>A captive hero offered to a settlement: the captive's side and the settlement owner's side.</summary>
public sealed record DonationFacts(SideIds Captive, SideIds Owner);

/// <summary>A notable of the tournament town with the side of his own map faction.</summary>
public sealed record DeedNotable(DeedHero Hero, SideIds Side);

/// <summary>
/// The town of a finished tournament. <see cref="OwnerLeader"/> is the leader of the owning clan (null when there is none),
/// <see cref="PlayerClanOwnsTown"/> is true when the player's clan owns it, and <see cref="OwnerSide"/> is the side of the
/// owner's map faction. <see cref="Notables"/> are the notables living in the town.
/// </summary>
public sealed record TournamentFacts(
    string TownName,
    SideIds Player,
    bool PlayerClanOwnsTown,
    DeedHero? OwnerLeader,
    SideIds OwnerSide,
    System.Collections.Generic.IReadOnlyList<DeedNotable> Notables);
