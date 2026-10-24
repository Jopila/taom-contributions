using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.BondsOfBattle.Models;
using TAOM.Features.Enlistment;
using TAOM.Features.Execution;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Relations;

namespace TAOM.Features.BondsOfBattle;

/// <summary>
/// The relation a finished player battle earns: a bonus with each allied lord, an optional penalty with each enemy lord,
/// and, after the player clears bandits in the field, gratitude from one notable in each settlement near the battle.
/// Pure over the snapshot and the settings the caller already read; <see cref="Apply"/> hands the result to
/// <see cref="IRelationGrantService"/> and tells the player.
/// </summary>
public sealed class BattleRelationService
{
    internal const int MinBanditGratitude = 1;
    internal const int MaxBanditGratitude = 10;
    internal const int CasualtiesPerGratitudePoint = 10;

    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IRandomSource _random;
    private readonly IRelationGrantService _grants;
    private readonly IBondsNoticeAdapter _notice;
    private readonly IAlignmentService _alignment;

    public BattleRelationService(IEnlistmentStateQuery enlistment, IRandomSource random, IRelationGrantService grants,
        IBondsNoticeAdapter notice, IAlignmentService alignment)
    {
        _enlistment = enlistment;
        _random = random;
        _grants = grants;
        _notice = notice;
        _alignment = alignment;
    }

    public void Apply(BattleSnapshot snapshot, BondsOfBattleSettings settings)
    {
        foreach (var grant in ComputeGrants(snapshot, settings))
        {
            if (_grants.Grant(grant.HeroId, grant.Delta, "battle: " + grant.Kind) == 0)
                continue;

            switch (grant.Kind)
            {
                case BattleGrantKind.FoughtTogether:
                    _notice.ShowFoughtTogether(grant.HeroName);
                    break;
                case BattleGrantKind.FoughtAgainst:
                    _notice.ShowFoughtAgainst(grant.HeroName);
                    break;
                case BattleGrantKind.BanditGratitude:
                    _notice.ShowBanditGratitude(grant.HeroName, grant.SettlementName);
                    break;
                default:
                    break;
            }
        }
    }

    public IReadOnlyList<BattleRelationGrant> ComputeGrants(BattleSnapshot snapshot, BondsOfBattleSettings settings)
    {
        var grants = new List<BattleRelationGrant>();
        if (!settings.Enabled || _enlistment.IsEnlisted)
            return grants;

        // Counted once per effective recipient: the hero the engine lands the change on (his clan leader under the vanilla
        // pair rule, himself under personal relations), so one clan is paid once when the relation is shared.
        var granted = new HashSet<string>(StringComparer.Ordinal);

        // On a win vanilla's own thank-you conversation already pays the side leader the player came to help
        // (PlayerEncounter.DoPlayerVictory), so he is skipped here, and his recipient is reserved first so that a kinsman
        // listed ahead of him does not pay his clan through the back door. On a loss there is no thank-you, so he earns it.
        foreach (var party in snapshot.Participants)
        {
            if (IsVanillaThanked(snapshot, party))
                granted.Add(KeyOf(party));
        }

        foreach (var party in snapshot.Participants)
        {
            if (!IsBondableLord(snapshot, party))
                continue;

            var delta = party.IsPlayerSide ? settings.AllyBonus : settings.EnemyPenaltyEnabled ? -settings.EnemyPenalty : 0;
            if (delta == 0 || !granted.Add(KeyOf(party)))
                continue;

            grants.Add(new BattleRelationGrant(party.LeaderHeroId!, delta,
                party.IsPlayerSide ? BattleGrantKind.FoughtTogether : BattleGrantKind.FoughtAgainst, party.LeaderName, string.Empty));
        }

        // A hideout assault is thanked by vanilla (HideoutCampaignBehavior.SetCleanHideoutRelations: +2 to one notable per
        // nearby village), so only a field battle with bandits earns gratitude here.
        if (settings.BanditGratitudeEnabled && snapshot.PlayerSideWon && !snapshot.IsHideoutBattle && OnlyBanditsOpposed(snapshot))
            AddBanditGratitude(snapshot, granted, grants);
        return grants;
    }

    // The recipient key: the effective recipient when the adapter read one, else the lord himself.
    private static string KeyOf(BattleParticipant party) =>
        string.IsNullOrEmpty(party.RelationHeroId) ? party.LeaderHeroId! : party.RelationHeroId!;

    // A party of the player's clan (the main party included), of the army the player leads, or nobody's earns nothing.
    private static bool IsBondableLord(BattleSnapshot snapshot, BattleParticipant party) =>
        !string.IsNullOrEmpty(party.LeaderHeroId) && party.IsLord && !party.IsBandit
        && !party.IsPlayerClanParty && !(party.IsInPlayersArmy && snapshot.PlayerLeadsArmy);

    /// <summary>
    /// Vanilla's own thank-you predicate (<c>PlayerEncounter.DoPlayerVictory</c>, v1.5.5 :1392): on a win, the leader party of the
    /// player's side, who is not in the player's army, and whom <c>BattleRewardModel.GetPlayerGainedRelationAmount</c> pays
    /// more than 0. Reserving a recipient needs only this predicate: a thanked leader Bonds would not pay (no lord, say) still keeps his clan out of
    /// the back door. The player's own party, when it leads the side, reserves only its own key (the player), which no paid party shares.
    /// </summary>
    private static bool IsVanillaThanked(BattleSnapshot snapshot, BattleParticipant party) =>
        snapshot.PlayerSideWon && party.IsPlayerSide && party.IsSideLeader && !party.IsInPlayersArmy
        && snapshot.VanillaThanksAmount > 0;

    private static bool OnlyBanditsOpposed(BattleSnapshot snapshot)
    {
        var enemies = snapshot.Participants.Where(p => !p.IsPlayerSide).ToList();
        return enemies.Count > 0 && enemies.All(p => p.IsBandit);
    }

    // One random notable per settlement, each paid the same: one point for every ten bandits killed, wounded or
    // routed (the side's troop casualties), from 1 to 10.
    private void AddBanditGratitude(BattleSnapshot snapshot, HashSet<string> granted, List<BattleRelationGrant> grants)
    {
        var delta = Math.Min(MaxBanditGratitude, Math.Max(MinBanditGratitude, snapshot.EnemyCasualties / CasualtiesPerGratitudePoint));
        var playerSide = _alignment.ResolveSide(snapshot.Player);
        foreach (var town in snapshot.NearbyNotables)
        {
            // A village in the other side's land (Free against Evil) does not thank; Neutral on either side keeps it. The
            // same reading the tournament filter uses (TournamentAlignmentFilterService): AreEnemyAlignments is not used
            // because it counts Neutral as everyone's enemy.
            var townSide = _alignment.ResolveSide(town.Owner);
            if (town.Notables.Count == 0 || FactionSideRules.AreOpposed(playerSide, townSide))
                continue;

            var pick = Math.Max(0, Math.Min(town.Notables.Count - 1, _random.Next(0, town.Notables.Count)));
            var notable = town.Notables[pick];
            if (granted.Add(notable.HeroId)) // a notable's recipient is himself
                grants.Add(new BattleRelationGrant(notable.HeroId, delta, BattleGrantKind.BanditGratitude, notable.Name, town.SettlementName));
        }
    }
}
