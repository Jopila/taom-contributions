using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TAOM.Features.SpecialResources;

namespace TAOM.Features.EyeOfSauron;

/// <summary>
/// Reduces a finished <see cref="MapEvent"/> to the plain <see cref="HuntBattleSummary"/> the service reads. Runs at
/// <c>MapEventEnded</c> (MapEvent.cs:1737), after the defeated parties were processed and before the sides are cleared,
/// so <c>IsPlayerMapEvent</c>, <c>PlayerSide</c> and the involved parties are still valid there. Boundary code only.
/// </summary>
internal static class HuntBattleReader
{
    internal static HuntBattleSummary Summarize(MapEvent mapEvent, string? hunterPartyId)
    {
        bool isPlayerEvent = mapEvent.IsPlayerMapEvent;
        bool playerWon = isPlayerEvent && SpecialResourceEarnPolicy.IsPlayerVictory(mapEvent.PlayerSide, mapEvent.WinningSide);
        var hunterSide = hunterPartyId == null ? BattleSideEnum.None : SideOf(mapEvent, hunterPartyId);
        var defeated = mapEvent.DefeatedSide;
        return new HuntBattleSummary
        {
            IsPlayerEvent = isPlayerEvent,
            PlayerWon = playerWon,
            LoserKingdomId = playerWon ? LoserKingdomId(mapEvent) : null,
            Kind = mapEvent.IsSiegeAssault ? DeedKind.Assault
                : mapEvent.IsSallyOut || mapEvent.IsSiegeOutside ? DeedKind.SallyOrOutside
                : DeedKind.Field,
            InvolvesHunter = hunterSide != BattleSideEnum.None,
            // None when nobody won: a player who got away (PlayerEncounter.LeaveBattle finalizes with no victory state).
            HasWinner = mapEvent.WinningSide != BattleSideEnum.None,
            HunterLost = hunterSide != BattleSideEnum.None && hunterSide == defeated,
        };
    }

    // MapEventSide.MapFaction falls back to LeaderParty.MapFaction unguarded (MapEventSide.cs:106).
    private static string? LoserKingdomId(MapEvent mapEvent)
    {
        var defeated = mapEvent.DefeatedSide;
        if (defeated == BattleSideEnum.None) return null;
        var side = mapEvent.GetMapEventSide(defeated);
        if (side?.LeaderParty == null) return null;
        return (side.MapFaction as Kingdom)?.StringId;
    }

    /// <summary>The side the party fought on, or None when it took no part. The sides are cleared only after this event.</summary>
    private static BattleSideEnum SideOf(MapEvent mapEvent, string partyId)
    {
        if (OnSide(mapEvent.AttackerSide, partyId)) return BattleSideEnum.Attacker;
        if (OnSide(mapEvent.DefenderSide, partyId)) return BattleSideEnum.Defender;
        return BattleSideEnum.None;
    }

    private static bool OnSide(MapEventSide? side, string partyId)
    {
        if (side == null) return false;
        foreach (var party in side.Parties)
        {
            if (string.Equals(party?.Party?.MobileParty?.StringId, partyId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
