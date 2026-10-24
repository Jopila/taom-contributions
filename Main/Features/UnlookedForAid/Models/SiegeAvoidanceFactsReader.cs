using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;

namespace TAOM.Features.UnlookedForAid.Models;

/// <summary>
/// Boundary reader (ADR-007): turns the two parties of one avoid question into <see cref="SiegeAvoidanceFacts"/>. Called on
/// the AI worker thread and on the main thread, so it reads party and camp fields only. The strengths and the player-led
/// check are taken only for a lord party facing a party tied to a siege; every other question reads two flags.
/// </summary>
internal static class SiegeAvoidanceFactsReader
{
    public static SiegeAvoidanceFacts Read(MobileParty party, MobileParty target)
    {
        var isLord = party.IsLordParty;
        var camp = target.BesiegerCamp;
        var settlementSiege = target.CurrentSettlement?.SiegeEvent;
        var siegeCamp = camp ?? settlementSiege?.BesiegerCamp;
        var playerLed = isLord && siegeCamp != null && siegeCamp.LeaderParty == MobileParty.MainParty;
        return new SiegeAvoidanceFacts(
            isLord,
            camp != null,
            settlementSiege != null,
            playerLed,
            isLord && camp != null ? OwnStrength(party) : float.NaN,
            isLord && camp != null ? CampStrength(camp!) : float.NaN);
    }

    // Vanilla's own-strength expression (DefaultMobilePartyAIModel.cs:208): the army's strength when the party leads
    // that army or is attached to it, else the party's own. The army is read once. The main thread can change it under
    // this read (the army disbands); a failed read becomes NaN and the service keeps vanilla's answer.
    private static float OwnStrength(MobileParty party)
    {
        try
        {
            var army = party.Army;
            return army != null && (party.AttachedTo != null || army.LeaderParty == party)
                ? army.EstimatedStrength
                : party.Party.EstimatedStrength;
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is NullReferenceException)
        {
            return float.NaN;
        }
    }

    // The sum vanilla's second pass uses (DefaultMobilePartyAIModel.cs:325-327): every party involved in the siege, with
    // EstimatedStrength on both sides (vanilla's own flee input at :220-222 is the terrain-adjusted GetCustomStrength, so
    // the two differ a little). The camp list can change under the enumerator while the main thread runs. A torn read
    // may throw, or may return a short or repeated sum without throwing; both are harmless, because the next check
    // recomputes. A throw becomes NaN and the service keeps vanilla's answer.
    private static float CampStrength(BesiegerCamp camp)
    {
        try
        {
            var sum = 0f;
            foreach (var besieger in camp.GetInvolvedPartiesForEventType())
                sum += besieger.EstimatedStrength;
            return sum;
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is NullReferenceException)
        {
            return float.NaN;
        }
    }
}
