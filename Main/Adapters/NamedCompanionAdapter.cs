using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

public class NamedCompanionAdapter : INamedCompanionAdapter
{
    public bool HeroExists(string characterId)
    {
        return Hero.AllAliveHeroes.Any(h => h.StringId == characterId)
            || Hero.DeadOrDisabledHeroes.Any(h => h.StringId == characterId);
    }

    public bool IsHeroAlive(string characterId)
    {
        return Hero.AllAliveHeroes.Any(h => h.StringId == characterId);
    }

    public bool IsPlacedInSettlement(string characterId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
        return hero?.StayingInSettlement != null || hero?.CurrentSettlement != null;
    }

    public bool IsRecruitedOrInParty(string characterId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
        if (hero == null) return false;
        // #127 P2 — broadened to include captor parties. A companion in a captor party
        // (PartyBelongedToAsPrisoner != null) is semantically "in a party" — skip placement.
        // Clan covers a companion promoted to lord: CompanionOf is null then, but Hero.Clan is its own clan. An
        // unrecruited named companion has no clan (heroes.xml uses Faction.neutral, which Hero.Deserialize skips).
        return hero.CompanionOf != null
            || hero.Clan != null
            || hero.PartyBelongedTo != null
            || hero.PartyBelongedToAsPrisoner != null;
    }

    public bool IsHeroPrisoner(string characterId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
        if (hero == null) return false;
        // Hero.IsPrisoner covers both settlement-prison and mobile-captor-party prisoners.
        // Belt-and-braces with PartyBelongedToAsPrisoner per the #127 audit description.
        return hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != null;
    }

    public bool IsHeroFugitive(string characterId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
        if (hero == null) return false;
        return hero.HeroState == Hero.CharacterStates.Fugitive;
    }

    public bool EnsureHomeSettlement(string characterId, string settlementId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
        if (hero == null || hero.BornSettlement != null) return false;

        var settlement = Settlement.Find(settlementId);
        if (settlement == null) return false;

        // Hero.UpdateHomeSettlement falls all the way through to _bornSettlement for a companion
        // with no clan, no CompanionOf and no governor relatives, so this is the field that decides
        // whether MapFaction resolves. The setter clears the cached _homeSettlement for us. This is
        // what HeroCreator.InitializeHeroFromSettings does for every runtime-created wanderer —
        // XML heroes are the only ones that miss it.
        hero.BornSettlement = settlement;
        return true;
    }

    public void PlaceInSettlement(string characterId, string settlementId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
        var settlement = Settlement.Find(settlementId);
        if (hero != null && settlement != null)
        {
            hero.ChangeState(Hero.CharacterStates.Active);
            EnterSettlementAction.ApplyForCharacterOnly(hero, settlement);
        }
    }

    public void MarkAsMet(string characterId)
    {
        var hero = Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
        hero?.SetHasMet();
    }

    public int GetRelationWithPlayer(string characterId)
    {
        try
        {
            var hero = Find(characterId);
            var player = Hero.MainHero;
            return hero == null || player == null ? 0 : player.GetRelation(hero);
        }
        catch { return 0; }
    }

    public bool ChangeRelationWithPlayer(string characterId, int delta)
    {
        try
        {
            var hero = Find(characterId);
            if (hero == null || Hero.MainHero == null) return false;
            ChangeRelationAction.ApplyPlayerRelation(hero, delta, affectRelatives: false, showQuickNotification: true);
            return true;
        }
        catch { return false; }
    }

    public bool IsCompanionLimitReached()
    {
        try
        {
            // Vanilla's too_many_companions (LordConversationsCampaignBehavior), same comparison.
            var clan = Clan.PlayerClan;
            return clan != null && clan.Companions.Count >= clan.CompanionLimit;
        }
        catch { return false; }
    }

    public int GetVanillaHiringPrice(string characterId)
    {
        try
        {
            var hero = Find(characterId);
            if (hero == null) return 0;
            return Campaign.Current?.Models?.CompanionHiringPriceCalculationModel?.GetCompanionHiringPrice(hero) ?? 0;
        }
        catch { return 0; }
    }

    public bool HireIntoPlayerParty(string characterId, int gold)
    {
        var hero = Find(characterId);
        var player = Hero.MainHero;
        var clan = Clan.PlayerClan;
        var party = MobileParty.MainParty;
        if (hero == null || player == null || clan == null || party == null || gold < 0 || player.Gold < gold)
            return false;

        // Vanilla's success consequence (conversation_companion_hire_on_consequence), same calls, same order.
        if (gold > 0) GiveGoldAction.ApplyBetweenCharacters(player, hero, gold);
        AddCompanionAction.Apply(clan, hero);
        AddHeroToPartyAction.Apply(hero, party);
        return true;
    }

    public bool IsHeroActive(string characterId) => Find(characterId)?.IsActive ?? false;

    public string GetCurrentSettlementId(string characterId) => Find(characterId)?.CurrentSettlement?.StringId;

    public void ReturnHome(string characterId, string settlementId)
    {
        var hero = Find(characterId);
        var settlement = Settlement.Find(settlementId);
        if (hero == null || settlement == null) return;

        // LeaveSettlementAction.ApplyForCharacterOnly dereferences CurrentSettlement unguarded.
        if (hero.CurrentSettlement != null) LeaveSettlementAction.ApplyForCharacterOnly(hero);
        EnterSettlementAction.ApplyForCharacterOnly(hero, settlement);
    }

    public bool IsPlayerInSettlement(string settlementId)
    {
        try
        {
            // Settlement.CurrentSettlement is the static vanilla's conversation code reads for "the player is in X".
            var current = Settlement.CurrentSettlement;
            return current != null && current.StringId == settlementId;
        }
        catch { return false; }
    }

    public bool SettlementExists(string settlementId)
    {
        try { return Settlement.Find(settlementId) != null; }
        catch { return false; }
    }

    private static Hero Find(string characterId) => Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == characterId);
}
