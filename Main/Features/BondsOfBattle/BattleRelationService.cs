using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.BondsOfBattle.Models;
using TAOM.Features.Enlistment;
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

    public BattleRelationService(IEnlistmentStateQuery enlistment, IRandomSource random, IRelationGrantService grants,
        IBondsNoticeAdapter notice)
    {
        _enlistment = enlistment;
        _random = random;
        _grants = grants;
        _notice = notice;
    }

    public void Apply(BattleSnapshot snapshot, BondsOfBattleSettings settings)
    {
        foreach (var grant in ComputeGrants(snapshot, settings))
        {
            if (!_grants.Grant(grant.HeroId, grant.Delta, "battle: " + grant.Kind))
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

        var granted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var party in snapshot.Participants)
        {
            if (!IsBondableLord(party))
                continue;

            var delta = party.IsPlayerSide ? settings.AllyBonus : settings.EnemyPenaltyEnabled ? -settings.EnemyPenalty : 0;
            if (delta == 0 || !granted.Add(party.LeaderHeroId!))
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

    // A party of the player's clan (the main party included), of the player's army, or nobody's earns nothing.
    private static bool IsBondableLord(BattleParticipant party) =>
        !string.IsNullOrEmpty(party.LeaderHeroId) && party.IsLord && !party.IsBandit
        && !party.IsPlayerClanParty && !party.IsInPlayerArmy;

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
        foreach (var town in snapshot.NearbyNotables)
        {
            if (town.Notables.Count == 0)
                continue;

            var pick = Math.Max(0, Math.Min(town.Notables.Count - 1, _random.Next(0, town.Notables.Count)));
            var notable = town.Notables[pick];
            if (granted.Add(notable.HeroId))
                grants.Add(new BattleRelationGrant(notable.HeroId, delta, BattleGrantKind.BanditGratitude, notable.Name, town.SettlementName));
        }
    }
}
