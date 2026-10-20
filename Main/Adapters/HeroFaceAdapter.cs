using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>
/// <see cref="IHeroFaceAdapter"/> over the campaign's heroes. <c>Hero.StaticBodyProperties</c>, <c>Weight</c>
/// and <c>Build</c> have public setters. The adapter is stateless: each snapshot carries its engine hero.
/// </summary>
public sealed class HeroFaceAdapter : IHeroFaceAdapter
{
    public IReadOnlyList<HeroFaceSnapshot> GetSnapshots()
    {
        var result = new List<HeroFaceSnapshot>();
        // Hero.MainHero is a computed getter that throws while the player character is not set yet.
        var player = CharacterObject.PlayerCharacter?.HeroObject;
        Append(Hero.AllAliveHeroes, player, result);
        Append(Hero.DeadOrDisabledHeroes, player, result);
        return result;
    }

    public bool TryRollInRange(HeroFaceSnapshot snapshot, int seed)
    {
        if (snapshot.Handle is not Hero hero) return false;
        var character = hero.CharacterObject;
        var rolled = BodyProperties.GetRandomBodyProperties(
            character.Race, hero.IsFemale,
            character.GetBodyPropertiesMin(returnBaseValue: true),
            character.GetBodyPropertiesMax(returnBaseValue: true),
            0, seed,
            character.BodyPropertyRange.HairTags,
            character.BodyPropertyRange.BeardTags,
            character.BodyPropertyRange.TattooTags);
        // HeroCreator rolls weight and build between the range's minimum and maximum the same way (engine random, not the seed).
        var dynamic = CharacterHelper.GetDynamicBodyPropertiesBetweenMinMaxRange(character);
        hero.StaticBodyProperties = rolled.StaticProperties;
        hero.Weight = dynamic.Weight;
        hero.Build = dynamic.Build;
        return true;
    }

    public bool TryRollAroundOwnFace(HeroFaceSnapshot snapshot, int seed, float variation, float rollAge)
    {
        if (snapshot.Handle is not Hero hero) return false;
        var character = hero.CharacterObject;
        var own = new BodyProperties(new DynamicBodyProperties(rollAge, hero.Weight, hero.Build), hero.StaticBodyProperties);
        var rolled = BodyProperties.GetRandomBodyProperties(
            character.Race, hero.IsFemale, own, own, 0, seed,
            character.BodyPropertyRange.HairTags,
            character.BodyPropertyRange.BeardTags,
            character.BodyPropertyRange.TattooTags,
            variation);
        hero.StaticBodyProperties = rolled.StaticProperties;
        return true;
    }

    private static void Append(IEnumerable<Hero> heroes, Hero? player, List<HeroFaceSnapshot> into)
    {
        foreach (var hero in heroes)
        {
            var character = hero?.CharacterObject;
            if (hero == null || character == null || string.IsNullOrEmpty(hero.StringId)) continue;

            var min = character.GetBodyPropertiesMin(returnBaseValue: true).StaticProperties;
            var max = character.GetBodyPropertiesMax(returnBaseValue: true).StaticProperties;
            into.Add(new HeroFaceSnapshot(
                hero.StringId,
                character.Race.ToString(System.Globalization.CultureInfo.InvariantCulture),
                hero.IsFemale,
                ReferenceEquals(hero, player),
                character.IsOriginalCharacter,
                hero.Clan?.Leader == hero,
                hero.StaticBodyProperties.ToString(),
                hero.StaticBodyProperties == min,
                min != max)
            {
                Handle = hero,
            });
        }
    }
}
