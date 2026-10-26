using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.Localization;
using TAOM.Core.Domain;
using TAOM.Core.Logging;

namespace TAOM.Features.Encyclopedia.Adapters;

/// <summary>
/// Builds the Race and Age filter groups of the encyclopedia hero page (#907). Every predicate catches: the engine's
/// <c>EncyclopediaPage.IsFiltered</c> has no catch, so a throw would break the whole list. A predicate that cannot
/// read the hero logs once and lets the hero through.
/// </summary>
public class EncyclopediaHeroFilterAdapter : IEncyclopediaHeroFilterAdapter
{
    private readonly IHeroFilterConfigProvider _provider;
    private readonly IRaceManager _races;
    private readonly IModLogger _logger;
    private HeroFilterConfig? _config;
    private bool _logged;

    public EncyclopediaHeroFilterAdapter(IHeroFilterConfigProvider provider, IRaceManager races, IModLogger logger)
    {
        _provider = provider;
        _races = races;
        _logger = logger;
    }

    public IReadOnlyList<EncyclopediaFilterGroup> BuildGroups()
    {
        var config = _config ??= _provider.LoadConfig();
        if (config.Groups.Count == 0) return Array.Empty<EncyclopediaFilterGroup>();

        var raceItems = new List<EncyclopediaFilterItem>();
        foreach (var group in config.Groups)
        {
            var id = group.Id;
            raceItems.Add(new EncyclopediaFilterItem(RaceLabel(id),
                Safe(hero => HeroFilterRules.MatchesRace(config, _races, id, hero.CharacterObject.Race))));
        }

        var ageItems = new List<EncyclopediaFilterItem>();
        foreach (var (stage, label) in StageLabels())
        {
            var wanted = stage;
            ageItems.Add(new EncyclopediaFilterItem(label,
                Safe(hero => KnowsDetailsOf(hero)
                    && HeroFilterRules.MatchesStage(config, _races, wanted, hero.CharacterObject.Race, hero.Age))));
        }

        return new[]
        {
            new EncyclopediaFilterGroup(raceItems, new TextObject("{=taom_ency_filter_race}Race")),
            new EncyclopediaFilterGroup(ageItems, new TextObject("{=jaaQijQs}Age")),
        };
    }

    /// <summary>Vanilla hides an unmet hero's age on the page, so the Age filter must not reveal it. Race stays unaffected: the page shows the model either way.</summary>
    protected virtual bool KnowsDetailsOf(Hero hero) =>
        Campaign.Current.Models.InformationRestrictionModel.DoesPlayerKnowDetailsOf(hero);

    private Predicate<object> Safe(Func<Hero, bool> rule) => item =>
    {
        try
        {
            return rule((Hero)item);
        }
        catch (Exception ex)
        {
            if (!_logged)
            {
                _logged = true;
                _logger.LogError($"EncyclopediaHeroFilterAdapter: a hero filter predicate threw; the hero is let through. {ex.GetType().Name}: {ex.Message}");
            }
            return true;
        }
    };

    private static TextObject RaceLabel(string groupId) => groupId switch
    {
        "men" => new TextObject("{=taom_ency_race_men}Men"),
        "elves" => new TextObject("{=taom_ency_race_elves}Elves"),
        "dwarves" => new TextObject("{=taom_ency_race_dwarves}Dwarves"),
        "orcs" => new TextObject("{=taom_ency_race_orcs}Orcs"),
        "goblins" => new TextObject("{=taom_ency_race_goblins}Goblins"),
        "uruks" => new TextObject("{=taom_ency_race_uruks}Uruk-hai"),
        "trolls" => new TextObject("{=taom_ency_race_trolls}Trolls"),
        "maiar" => new TextObject("{=taom_ency_race_maiar}Maiar and Wraiths"),
        _ => new TextObject(groupId),
    };

    private static IEnumerable<(LifeStage Stage, TextObject Label)> StageLabels()
    {
        yield return (LifeStage.Young, new TextObject("{=taom_ency_age_young}Young"));
        yield return (LifeStage.Adult, new TextObject("{=taom_ency_age_adult}Adult"));
        yield return (LifeStage.Mature, new TextObject("{=taom_ency_age_mature}Mature"));
        yield return (LifeStage.Elder, new TextObject("{=taom_ency_age_elder}Elder"));
        yield return (LifeStage.Venerable, new TextObject("{=taom_ency_age_venerable}Venerable"));
    }
}
