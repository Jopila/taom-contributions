using System.Collections.Generic;

namespace TAOM.Features.SettlementServices;

/// <summary>
/// Why the arena question is hidden, greyed or unanswered. <see cref="TidingsService"/> checks in this order: Disabled, NotSinglePlayer,
/// Disguised, NotEnoughGold. NoTournaments and Problem are only ever the answer to asking.
/// </summary>
public enum TidingsBlock
{
    None,
    Disabled,
    NotSinglePlayer,
    Disguised,
    NotEnoughGold,
    NoTournaments,
    Problem,
}

/// <summary>One town holding a tournament, as plain values read from the engine.</summary>
/// <param name="TownId">The town's string id.</param>
/// <param name="TownName">The name the game shows now.</param>
/// <param name="RealmName">The name of the town's map faction (its political owner).</param>
/// <param name="Distance">The land travel distance from the town the player stands in; not trusted until the service has checked it.</param>
/// <param name="IsAtWarWithPlayer">True when the town's map faction is at war with the player's.</param>
/// <param name="IsCurrentTown">True for the town the player stands in.</param>
public sealed record TournamentSiteSnapshot(string TownId, string TownName, string RealmName, float Distance, bool IsAtWarWithPlayer,
    bool IsCurrentTown);

/// <summary>One line of the answer: a town and the realm that holds it.</summary>
public readonly record struct TidingsRow(string Town, string Realm);

/// <summary>What asking did. <see cref="Rows"/> is empty unless the block is None.</summary>
public sealed record TidingsResult(TidingsBlock Block, IReadOnlyList<TidingsRow> Rows);
