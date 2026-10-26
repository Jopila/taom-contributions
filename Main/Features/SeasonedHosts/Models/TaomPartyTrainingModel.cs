using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TAOM.Adapters;

namespace TAOM.Features.SeasonedHosts.Models;

/// <summary>
/// Seasoned Hosts (#890, docs/features/seasoned-hosts.md): an AI lord party's troops train faster. Both XP members call base
/// first, then add one factor from the service. The daily member covers each troop of the party
/// (<c>MobilePartyTrainingBehavior.OnDailyTickParty</c>); the battle member covers the XP a troop earned in a battle
/// (<c>MapEventParty.CommitXpGain</c>). Registry: docs/reference/gamemodel-registry.md.
/// </summary>
public sealed class TaomPartyTrainingModel : DefaultPartyTrainingModel
{
    private readonly SeasonedHostsService _service;
    private readonly ISeasonedHostsAdapter _adapter;

    public TaomPartyTrainingModel(SeasonedHostsService service, ISeasonedHostsAdapter adapter)
    {
        _service = service;
        _adapter = adapter;
    }

    public override ExplainedNumber GetEffectiveDailyExperience(MobileParty mobileParty, TroopRosterElement troop) =>
        WithBonus(base.GetEffectiveDailyExperience(mobileParty, troop), mobileParty);

    public override ExplainedNumber CalculateXpGainFromBattles(FlattenedTroopRosterElement troopRosterElement, PartyBase party) =>
        WithBonus(base.CalculateXpGainFromBattles(troopRosterElement, party), party.MobileParty);

    private ExplainedNumber WithBonus(ExplainedNumber xp, MobileParty party)
    {
        xp.AddFactor(_service.TrainingFactor(_adapter.Read(party)));
        return xp;
    }
}
