using System.Collections.Generic;

namespace TAOM.Features.BondsOfBattle.Models;

/// <summary>
/// One party in a battle the player fought, read at the boundary so no engine type reaches the service.
/// <see cref="LeaderHeroId"/> is null for a leaderless party. <see cref="IsPlayerClanParty"/> is true for the main
/// party and every other party of the player's clan, so it also covers the player hero.
/// </summary>
public sealed record BattleParticipant(
    string? LeaderHeroId,
    string LeaderName,
    bool IsPlayerSide,
    bool IsPlayerClanParty,
    bool IsInPlayerArmy,
    bool IsBandit,
    bool IsLord);

/// <summary>A living notable, by id and display name.</summary>
public sealed record NotableInfo(string HeroId, string Name);

/// <summary>The living notables of one settlement that lies near the battle.</summary>
public sealed record SettlementNotables(string SettlementId, string SettlementName, IReadOnlyList<NotableInfo> Notables);

/// <summary>
/// A finished player battle as the service sees it. <see cref="EnemyCasualties"/> is the troop casualties of the side
/// opposite the player's. <see cref="NearbyNotables"/> is empty unless the adapter was asked for a radius.
/// <see cref="IsHideoutBattle"/> marks a hideout assault, which vanilla already thanks.
/// </summary>
public sealed record BattleSnapshot(
    IReadOnlyList<BattleParticipant> Participants,
    bool PlayerSideWon,
    int EnemyCasualties,
    IReadOnlyList<SettlementNotables> NearbyNotables,
    bool IsHideoutBattle);

/// <summary>Why a battle changes a relation; picks the line the player reads.</summary>
public enum BattleGrantKind
{
    FoughtTogether,
    FoughtAgainst,
    BanditGratitude,
}

/// <summary>One relation change the battle earns. <see cref="SettlementName"/> is empty unless the notable's town matters.</summary>
public sealed record BattleRelationGrant(string HeroId, int Delta, BattleGrantKind Kind, string HeroName, string SettlementName);
