using TaleWorlds.CampaignSystem;
using TAOM.Features.Relations;

namespace TAOM.Adapters;

/// <summary>
/// Builds a <see cref="RelationTarget"/> from an engine hero, for the adapters whose facts list heroes the player could earn or
/// lose relation with (Good Deeds, Long Memory).
/// </summary>
internal static class RelationTargets
{
    /// <summary>
    /// The hero and the hero a change to that hero really moves. The player is never asked: the diplomacy model's pair rule is not
    /// meant for the player against the player's own hero.
    /// </summary>
    public static RelationTarget Of(Hero player, Hero hero)
    {
        if (hero == player)
            return new RelationTarget(hero.StringId, hero.StringId, hero.IsAlive, true);

        Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(player, hero, out _, out var owner);
        return new RelationTarget(hero.StringId, (owner ?? hero).StringId, hero.IsAlive, false);
    }
}
