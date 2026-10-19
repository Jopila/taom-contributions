using System.Collections.Generic;

namespace TAOM.Features.BondsOfBattle.Models;

/// <summary>
/// One party in a battle the player fought, read at the boundary so no engine type reaches the service.
/// <see cref="LeaderHeroId"/> is null for a leaderless party. <see cref="IsPlayerClanParty"/> is true for the main
/// party and every other party of the player's clan, so it also covers the player hero.
/// <see cref="IsInPlayersArmy"/> is true for a party in the army the player is in (his own or an AI lord's he joined);
/// whether the player leads it is <see cref="BattleSnapshot.PlayerLeadsArmy"/>, and the service decides what the two facts
/// mean. <see cref="IsSideLeader"/> marks the leader party of this party's side. <see cref="RelationHeroId"/> is the hero the engine would land a relation change on for this
/// leader (his clan leader under the vanilla pair rule, himself under personal relations), the key a bonus is counted
/// once on.
/// </summary>
public sealed record BattleParticipant(
    string? LeaderHeroId,
    string LeaderName,
    bool IsPlayerSide,
    bool IsPlayerClanParty,
    bool IsInPlayersArmy,
    bool IsBandit,
    bool IsLord,
    string? RelationHeroId,
    bool IsSideLeader);

/// <summary>A living notable, by id and display name.</summary>
public sealed record NotableInfo(string HeroId, string Name);

/// <summary>
/// The living notables of one settlement that lies near the battle, with the settlement owner's faction and culture ids
/// (the pair <c>IAlignmentService.ResolveSide</c> reads), so a village in the other side's land can be left out.
/// </summary>
public sealed record SettlementNotables(string SettlementId, string SettlementName, IReadOnlyList<NotableInfo> Notables,
    string? OwnerFactionId, string? OwnerCultureId);

/// <summary>
/// A finished player battle as the service sees it. <see cref="EnemyCasualties"/> is the troop casualties of the side
/// opposite the player's. <see cref="NearbyNotables"/> is empty unless the adapter was asked for a radius.
/// <see cref="IsHideoutBattle"/> marks a hideout assault, which vanilla already thanks. The player's faction and culture ids
/// are the other half of the alignment test on a notable's settlement. <see cref="PlayerLeadsArmy"/> is whether the player
/// leads the army he is in. <see cref="VanillaThanksAmount"/> is vanilla's value for the player side's leader
/// (<c>BattleRewardModel.GetPlayerGainedRelationAmount</c>), computed on every capture; the service uses it only on a win, and
/// it is 0 when there is no player encounter or no leader hero.
/// </summary>
public sealed record BattleSnapshot(
    IReadOnlyList<BattleParticipant> Participants,
    bool PlayerSideWon,
    int EnemyCasualties,
    IReadOnlyList<SettlementNotables> NearbyNotables,
    bool IsHideoutBattle,
    string? PlayerFactionId,
    string? PlayerCultureId,
    bool PlayerLeadsArmy,
    int VanillaThanksAmount);

/// <summary>Why a battle changes a relation; picks the line the player reads.</summary>
public enum BattleGrantKind
{
    FoughtTogether,
    FoughtAgainst,
    BanditGratitude,
}

/// <summary>One relation change the battle earns. <see cref="SettlementName"/> is empty unless the notable's town matters.</summary>
public sealed record BattleRelationGrant(string HeroId, int Delta, BattleGrantKind Kind, string HeroName, string SettlementName);
