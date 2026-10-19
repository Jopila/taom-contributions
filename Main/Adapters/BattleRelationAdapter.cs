using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.BondsOfBattle.Models;

namespace TAOM.Adapters;

public sealed class BattleRelationAdapter : IBattleRelationAdapter
{
    private readonly IModLogger _logger;

    public BattleRelationAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public BattleSnapshot? Capture(MapEvent mapEvent, float notableRadius)
    {
        try
        {
            // Campaign.Current?.MainParty: the static MobileParty.MainParty throws with no campaign (adapters.md).
            var main = Campaign.Current?.MainParty;
            if (mapEvent == null || main == null)
                return null;

            var playerSide = mapEvent.PlayerSide;
            if (playerSide != BattleSideEnum.Attacker && playerSide != BattleSideEnum.Defender)
                return null;
            var enemySide = playerSide == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker;

            var participants = new List<BattleParticipant>();
            AddSide(mapEvent, BattleSideEnum.Attacker, playerSide, main, participants);
            AddSide(mapEvent, BattleSideEnum.Defender, playerSide, main, participants);

            // MapEventSide.TroopCasualties is a public field. MapEventSide.MapFaction is not read: it throws when the
            // side has no LeaderParty.
            var enemyCasualties = mapEvent.GetMapEventSide(enemySide)?.TroopCasualties ?? 0;
            var notables = notableRadius > 0f ? NotablesNear(mapEvent.Position, notableRadius) : new List<SettlementNotables>();
            return new BattleSnapshot(participants, mapEvent.WinningSide == playerSide, enemyCasualties, notables, mapEvent.IsHideoutBattle);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[BondsOfBattle] BattleRelationAdapter.Capture failed: {ex.Message}");
            return null;
        }
    }

    private static void AddSide(MapEvent mapEvent, BattleSideEnum side, BattleSideEnum playerSide, MobileParty main,
        List<BattleParticipant> into)
    {
        var parties = mapEvent.PartiesOnSide(side);
        if (parties == null)
            return;

        var playerClan = Clan.PlayerClan;
        foreach (var eventParty in parties)
        {
            // A party with no MobileParty is a settlement's own PartyBase: it leads no one, and counting it would
            // break the bandit-only test on a hideout assault.
            var mobile = eventParty?.Party?.MobileParty;
            if (mobile == null)
                continue;

            var leader = mobile.LeaderHero;
            into.Add(new BattleParticipant(
                leader?.StringId,
                leader?.Name?.ToString() ?? string.Empty,
                side == playerSide,
                playerClan != null && (leader?.Clan == playerClan || mobile.ActualClan == playerClan),
                main.Army != null && mobile.Army == main.Army,
                mobile.IsBandit,
                leader != null && leader.IsLord));
        }
    }

    // A positive requirement on the distance: a NaN position fails the <= test and the settlement is left out.
    private static List<SettlementNotables> NotablesNear(CampaignVec2 position, float radius)
    {
        var found = new List<SettlementNotables>();
        var all = Settlement.All;
        if (all == null)
            return found;

        for (var i = 0; i < all.Count; i++)
        {
            var settlement = all[i];
            if (settlement == null || !(position.Distance(settlement.GatePosition) <= radius))
                continue;

            var living = new List<NotableInfo>();
            foreach (var notable in settlement.Notables)
            {
                if (notable != null && notable.IsAlive)
                    living.Add(new NotableInfo(notable.StringId, notable.Name?.ToString() ?? notable.StringId));
            }

            if (living.Count > 0)
                found.Add(new SettlementNotables(settlement.StringId, settlement.Name?.ToString() ?? settlement.StringId, living));
        }

        return found;
    }
}
