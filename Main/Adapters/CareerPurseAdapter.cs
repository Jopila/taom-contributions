using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace TAOM.Adapters;

public class CareerPurseAdapter : ICareerPurseAdapter
{
    public int Gold => Hero.MainHero?.Gold ?? 0;

    public bool ChargeGold(int amount)
    {
        var hero = Hero.MainHero;
        if (hero == null || amount < 0 || hero.Gold < amount)
            return false;
        if (amount > 0)
            GiveGoldAction.ApplyBetweenCharacters(hero, null, amount);
        return true;
    }

    public bool GrantGold(int amount)
    {
        var hero = Hero.MainHero;
        if (hero == null || amount <= 0)
            return false;
        // A null giver is valid in GiveGoldAction.ApplyInternal: only a non-null hero or party is debited.
        GiveGoldAction.ApplyBetweenCharacters(null, hero, amount);
        return true;
    }
}
