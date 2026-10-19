using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IHardQuestionsActionAdapter"/>. The engine ignores
/// <c>affectRelatives</c> (v1.5.5). A relation change lands on the pair from
/// <c>DiplomacyModel.GetHeroesForEffectiveRelation</c>, the hero's clan leader unless he is clanless or a player
/// companion. Per-clan quick notifications are off because one deed touches many clans; ShowRelationsHurt shows one summary instead.
/// </summary>
public sealed class HardQuestionsActionAdapter : IHardQuestionsActionAdapter
{
    private readonly IModLogger _logger;

    public HardQuestionsActionAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public bool ChangeRelation(string heroId, int delta)
    {
        var hero = Find(heroId);
        if (hero == null || delta == 0)
            return false;
        ChangeRelationAction.ApplyPlayerRelation(hero, delta, affectRelatives: false, showQuickNotification: false);
        return true;
    }

    public bool PayGold(string heroId, int amount)
    {
        var hero = Find(heroId);
        var player = Hero.MainHero;
        if (hero == null || player == null || amount <= 0 || player.Gold < amount)
            return false;
        GiveGoldAction.ApplyBetweenCharacters(player, hero, amount);
        return true;
    }

    /// <summary>
    /// Sets the hit points to 1 directly: <c>Hero.MakeWounded</c> would also clear a death mark the hero may
    /// carry, and the setter raises the wound event and updates the prison roster.
    /// </summary>
    public bool Wound(string heroId)
    {
        var hero = Find(heroId);
        if (hero == null || hero.IsDead)
            return false;
        hero.HitPoints = 1;
        return true;
    }

    public bool KillByWounds(string heroId)
    {
        var hero = Find(heroId);
        if (hero == null || hero.IsDead)
            return false;
        KillCharacterAction.ApplyByWounds(hero);
        if (!hero.IsDead)
            return false;
        // KillCharacterAction removes the location character only while the hero still has a current
        // settlement, which an ended captivity no longer gives him: the dead lord would spawn again in the dungeon
        // on the next visit. An agent already standing there for this visit is not removed.
        LocationComplex.Current?.RemoveCharacterIfExists(hero);
        return true;
    }

    public void LoseRenown(int amount)
    {
        var clan = Clan.PlayerClan;
        if (clan == null || amount <= 0)
            return;
        var floor = Campaign.Current.Models.ClanTierModel.GetRequiredRenownForTier(clan.Tier);
        if (clan.Renown <= floor)
            return;
        clan.Renown = Math.Max(floor, clan.Renown - amount);
    }

    public void ShowReproach(string companionId)
    {
        var hero = Find(companionId);
        if (hero == null)
            return;
        var text = new TextObject("{=taom_hq_reproach}{COMPANION} will not meet your eye after what was done to that lord.")
            .SetTextVariable("COMPANION", hero.Name);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Red));
    }

    public void ShowRelationsHurt(int count)
    {
        try
        {
            var text = new TextObject("{=taom_hq_relations_hurt}Your deed has hurt your relations with {COUNT} {?IS_PLURAL}clans{?}clan{\\?}.");
            text.SetTextVariable("IS_PLURAL", count > 1 ? 1 : 0);
            text.SetTextVariable("COUNT", count);
            MBInformationManager.AddQuickInformation(text);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[HardQuestions] Showing the relations summary failed: {ex.Message}");
        }
    }

    public void CommitOpenPartyScreen()
    {
        try
        {
            GameStateManager.Current?.LastOrDefault<PartyState>()?.PartyScreenLogic?.DoneLogic(true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[HardQuestions] Committing the open party screen failed: {ex.Message}");
        }
    }

    private static Hero? Find(string heroId) =>
        string.IsNullOrEmpty(heroId) ? null : Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
}
