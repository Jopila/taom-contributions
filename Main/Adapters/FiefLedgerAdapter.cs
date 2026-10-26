using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TAOM.Features.ReevesLedger;

namespace TAOM.Adapters;

public sealed class FiefLedgerAdapter : IFiefLedgerAdapter
{
    public ProfitInputs ReadProfit(Settlement settlement)
    {
        var town = settlement.Town;
        var clan = town?.OwnerClan;
        if (town == null || clan == null)
            return new ProfitInputs();

        // The model slots, not DefaultClanFinanceModel: TaomClanFinanceModel adds the tariff feats to the tariff call. The same
        // three calls and the same (int) truncation as DefaultClanFinanceModel.AddSettlementIncome, so the figures match the clan
        // finance tooltip line for line. applyWithdrawals stays false everywhere: true takes a fifth of the accumulated trade tax out of
        // the town on every call.
        var inputs = new ProfitInputs
        {
            SettlementTax = (int)Campaign.Current.Models.SettlementTaxModel.CalculateTownTax(town).ResultNumber,
            Tariffs = (int)Campaign.Current.Models.ClanFinanceModel.CalculateTownIncomeFromTariffs(clan, town, applyWithdrawals: false).ResultNumber,
            Projects = Campaign.Current.Models.ClanFinanceModel.CalculateTownIncomeFromProjects(town),
        };

        var villages = new List<VillageIncome>(town.Villages.Count);
        foreach (var village in town.Villages)
            villages.Add(new VillageIncome(village.Name.ToString(), Campaign.Current.Models.ClanFinanceModel.CalculateVillageIncome(clan, village, applyWithdrawals: false)));
        inputs.Villages = villages;

        // The line is the garrison's daily wage while its party is active. The clan expense pass charges a top-up to a 5,000 purse
        // floor (AddExpensesFromPartiesAndGarrisons), which equals this wage once the purse has settled.
        var garrison = town.GarrisonParty;
        if (garrison != null && garrison.IsActive)
        {
            inputs.HasGarrison = true;
            inputs.GarrisonWage = garrison.TotalWage;
        }

        return inputs;
    }

    public IReadOnlyList<StashEntry> ReadStash(Settlement settlement)
    {
        var stash = settlement.Stash;
        var entries = new List<StashEntry>(stash.Count);
        for (var i = 0; i < stash.Count; i++)
        {
            var element = stash[i];
            if (element.IsEmpty)
                continue;
            entries.Add(new StashEntry(element.EquipmentElement.GetModifiedItemName().ToString(), element.Amount));
        }

        return entries;
    }

    public IReadOnlyList<PrisonerEntry> ReadPrisoners(Settlement settlement)
    {
        var roster = settlement.Party.PrisonRoster;
        var entries = new List<PrisonerEntry>(roster.Count);
        for (var i = 0; i < roster.Count; i++)
        {
            var character = roster.GetCharacterAtIndex(i);
            entries.Add(new PrisonerEntry(character.Name.ToString(), roster.GetElementNumber(i), character.IsHero));
        }

        return entries;
    }

    public int ReadPatrolMen(Settlement settlement) =>
        settlement.PatrolParty?.MobileParty?.MemberRoster.TotalManCount ?? 0;

    public bool ReadPatrolAlive(Settlement settlement) => settlement.PatrolParty != null;

    // The model slot, not the Guard House: a castle's answer comes from TaomSettlementPatrolModel (Keep Wardens, #888).
    public bool PatrolModelAllows(Settlement settlement) =>
        Campaign.Current.Models.SettlementPatrolModel.CanSettlementHavePatrolParties(settlement, false);

    public string PatrolTooltip(Settlement settlement)
    {
        var town = settlement.Town;
        if (town == null)
            return "";
        // GetTownPatrolTooltip asks the behaviour for a status when there is no patrol party, and dereferences it unchecked.
        if (settlement.PatrolParty == null && Campaign.Current.GetCampaignBehavior<IPatrolPartiesCampaignBehavior>() == null)
            return "";
        return CampaignUIHelper.GetTownPatrolTooltip(town);
    }
}
