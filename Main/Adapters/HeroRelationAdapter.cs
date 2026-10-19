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

    public bool ApplyPlayerRelation(string heroId, int delta)
    {
        if (delta == 0)
            return false;
        try
        {
            var hero = FindHero(heroId);
            if (hero == null || !hero.IsAlive || hero == CharacterObject.PlayerCharacter?.HeroObject)
                return false;

            // In v1.5.5 the call's affectRelatives argument is not forwarded to the change, so the default is passed.
            ChangeRelationAction.ApplyPlayerRelation(hero, delta, showQuickNotification: false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Relations] ApplyPlayerRelation('{heroId}', {delta}) failed: {ex.Message}");
            return false;
        }
    }

    private static Hero? FindHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        return Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
    }
}
