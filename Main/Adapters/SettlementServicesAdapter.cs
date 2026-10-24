using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.SettlementServices;

namespace TAOM.Adapters;

/// <summary>
/// The engine side of Watch-Silver and Tidings of the Lists (#859, docs/features/settlement-services.md). It reads the facts
/// the services decide on and performs the two writes (a fief's loyalty and the player's gold); it applies no rule. Nothing it
/// writes is a TAOM record: a fief's loyalty and the player's gold are saved by the engine.
/// </summary>
public sealed class SettlementServicesAdapter : ISettlementServicesAdapter
{
    public GovernedFiefSnapshot? ReadGovernedFief()
    {
        if (Campaign.Current == null)
            return null;
        var hero = Hero.OneToOneConversationHero;
        var town = hero?.GovernorOf;
        var settlement = town?.Settlement;
        if (hero == null || town == null || settlement == null)
            return null;

        return new GovernedFiefSnapshot(
            settlement.StringId,
            town.OwnerClan == Clan.PlayerClan,
            hero.CurrentSettlement == settlement,
            Settlement.CurrentSettlement == settlement,
            town.Loyalty,
            settlement.Militia);
    }

    public float SetLoyalty(string settlementId, float value)
    {
        var town = Settlement.Find(settlementId)?.Town
            ?? throw new InvalidOperationException($"Settlement {settlementId} has no town component, so it has no loyalty to set.");
        town.Loyalty = value;
        return town.Loyalty;
    }

    public IReadOnlyList<TournamentSiteSnapshot> ReadTournamentSites()
    {
        var here = Settlement.CurrentSettlement;
        var manager = Campaign.Current?.TournamentManager;
        var sites = new List<TournamentSiteSnapshot>();
        if (here == null || manager == null)
            return sites;

        var playerFaction = Hero.MainHero?.MapFaction;
        var model = Campaign.Current!.Models.MapDistanceModel;
        // Tournaments are created for towns only (TournamentCampaignBehavior), so Town.AllTowns is the whole population here.
        // The distance is the cached land path and is measured only for a town that holds a tournament.
        foreach (var town in Town.AllTowns)
        {
            var settlement = town.Settlement;
            if (settlement == null || manager.GetTournamentGame(town) == null)
                continue;

            var isCurrent = settlement == here;
            var distance = isCurrent ? 0f : model.GetDistance(here, settlement, false, false, MobileParty.NavigationType.Default);
            var faction = town.MapFaction;
            var atWar = faction != null && playerFaction != null && faction.IsAtWarWith(playerFaction);
            sites.Add(new TournamentSiteSnapshot(settlement.StringId, settlement.Name.ToString(), faction?.Name?.ToString() ?? "",
                distance, atWar, isCurrent));
        }

        return sites;
    }

    public float TournamentDistanceCutoff => Campaign.MapDiagonal * 2f;

    public int PlayerGold => Hero.MainHero?.Gold ?? 0;

    public bool IsPlayerDisguised => Campaign.Current?.IsMainHeroDisguised == true;

    public int ChargePlayer(int amount)
    {
        var hero = Hero.MainHero;
        if (hero == null || amount <= 0 || hero.Gold < amount)
            return 0;
        var before = hero.Gold;
        GiveGoldAction.ApplyBetweenCharacters(hero, null, amount);
        return before - hero.Gold;
    }

    public void Refund(int amount)
    {
        var hero = Hero.MainHero;
        if (hero == null || amount <= 0)
            return;
        GiveGoldAction.ApplyBetweenCharacters(null, hero, amount, disableNotification: true);
    }
}
