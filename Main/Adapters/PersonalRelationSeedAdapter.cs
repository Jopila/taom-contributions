using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.Relations;

namespace TAOM.Adapters;

public sealed class PersonalRelationSeedAdapter : IPersonalRelationSeedAdapter
{
    private readonly IModLogger _logger;

    public PersonalRelationSeedAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<SeedRow>? Snapshot()
    {
        try
        {
            // The campaign statics throw with no campaign (adapters.md), so the player is read the safe way.
            var player = CharacterObject.PlayerCharacter?.HeroObject;
            var alive = Campaign.Current?.AliveHeroes;
            if (player == null || alive == null)
                return null;

            var playerClan = Clan.PlayerClan;
            var rows = new List<SeedRow>();
            foreach (var hero in alive)
            {
                if (hero == null || hero == player || !hero.IsAlive || !hero.IsLord)
                    continue;

                var clan = hero.Clan;
                rows.Add(new SeedRow(hero.StringId, clan?.StringId, clan != null && clan.Leader == hero,
                    clan != null && clan == playerClan, CharacterRelationManager.GetHeroRelation(player, hero)));
            }

            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[PersonalRelations] seed snapshot failed: {ex.Message}");
            return null;
        }
    }

    public bool SetRelation(string heroId, int value)
    {
        try
        {
            var player = CharacterObject.PlayerCharacter?.HeroObject;
            var hero = string.IsNullOrEmpty(heroId) ? null : Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
            if (player == null || hero == null || hero == player || !hero.IsAlive)
                return false;

            // Hero.SetPersonalRelation clamps to the diplomacy model's limits and writes the raw pair: no event, no notice.
            player.SetPersonalRelation(hero, value);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[PersonalRelations] SetRelation('{heroId}', {value}) failed: {ex.Message}");
            return false;
        }
    }
}
