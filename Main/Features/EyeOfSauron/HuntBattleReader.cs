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
        return new HuntBattleSummary
        {
            IsPlayerEvent = isPlayerEvent,
            PlayerWon = playerWon,
            LoserKingdomId = playerWon ? LoserKingdomId(mapEvent) : null,
            Kind = mapEvent.IsSiegeAssault ? DeedKind.Assault
                : mapEvent.IsSallyOut || mapEvent.IsSiegeOutside ? DeedKind.SallyOrOutside
                : DeedKind.Field,
            InvolvesHunter = hunterPartyId != null && Involves(mapEvent, hunterPartyId),
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

    private static bool Involves(MapEvent mapEvent, string partyId)
    {
        foreach (var party in mapEvent.InvolvedParties)
        {
            if (string.Equals(party?.MobileParty?.StringId, partyId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
