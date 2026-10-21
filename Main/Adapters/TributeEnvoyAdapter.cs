using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>
/// The tribute envoy's player reads and payment (#870 slice 2). Members verified against the v1.5.5 decompile:
/// <c>Hero.MainHero</c> (Hero.cs:958), <c>Hero.GetSkillValue</c> (Hero.cs:1439), <c>DefaultSkills.Charm</c>
/// (DefaultSkills.cs:69), <c>Clan.PlayerClan</c> (Clan.cs:284), <c>Clan.Kingdom</c> (Clan.cs:152), <c>Clan.Fiefs</c> (Clan.cs:181) and
/// <c>Town.Villages</c> (Town.cs:309, the bound villages of the fief's settlement). The gold goes through
/// <see cref="ICareerPurseAdapter"/>, the adapter that already reads and debits the main hero's purse
/// (<c>GiveGoldAction.ApplyBetweenCharacters</c>), so there is one debit path. Stateless: every call reads the live campaign.
/// </summary>
public sealed class TributeEnvoyAdapter : ITributeEnvoyAdapter
{
    private readonly ICareerPurseAdapter _purse;

    public TributeEnvoyAdapter(ICareerPurseAdapter purse) => _purse = purse;

    public EnvoyPlayerReading ReadPlayer()
    {
        // Hero.MainHero and Clan.PlayerClan both dereference the current campaign.
        if (Campaign.Current == null)
            return EnvoyPlayerReading.Missing;
        var hero = Hero.MainHero;
        var clan = Clan.PlayerClan;
        if (hero == null || clan == null)
            return EnvoyPlayerReading.Missing;
        return new EnvoyPlayerReading(true, clan.StringId, clan.Kingdom?.StringId, _purse.Gold, hero.GetSkillValue(DefaultSkills.Charm), CountVillages(clan));
    }

    /// <summary>
    /// The villages bound to the clan's fiefs, summed fief by fief (not <c>Clan.Villages</c>): the same owner, the bound
    /// fief's clan, that <see cref="RaidHostAdapter"/> reads when it picks the villages a host raids.
    /// </summary>
    private static int CountVillages(Clan clan)
    {
        var fiefs = clan.Fiefs;
        int villages = 0;
        for (int i = 0; fiefs != null && i < fiefs.Count; i++)
            villages += fiefs[i]?.Villages?.Count ?? 0;
        return villages;
    }

    public bool TryCharge(int amount) => _purse.ChargeGold(amount);
}
