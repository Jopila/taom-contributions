using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public sealed class HeroRelationAdapter : IHeroRelationAdapter
{
    private readonly IModLogger _logger;

    public HeroRelationAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public int ApplyPlayerRelation(string heroId, int delta)
    {
        if (delta == 0)
            return 0;
        try
        {
            var player = CharacterObject.PlayerCharacter?.HeroObject;
            var hero = FindHero(heroId);
            if (player == null || hero == null || !hero.IsAlive || hero == player)
                return 0;

            // The relation the player sees: Hero.GetRelation, an int, through the diplomacy model's effective pair. In v1.5.5 the
            // call's affectRelatives argument is not forwarded to the change, so the default is passed.
            var before = player.GetRelation(hero);
            ChangeRelationAction.ApplyPlayerRelation(hero, delta, showQuickNotification: false);
            return player.GetRelation(hero) - before;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Relations] ApplyPlayerRelation('{heroId}', {delta}) failed: {ex.Message}");
            return 0;
        }
    }

    private static Hero? FindHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        return Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
    }
}
