using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Domain;
using TAOM.Core.Validation;

namespace TAOM.Features.Encyclopedia;

public enum LifeStage
{
    Young,
    Adult,
    Mature,
    Elder,
    Venerable,
}

/// <summary>Pure rules behind the encyclopedia hero filters (#907): the life stage of an age, and a race's group.</summary>
public static class HeroFilterRules
{
    /// <summary>Where the Young stage starts; younger heroes are children and fall in no stage.</summary>
    public const float FirstStageAge = 18f;

    /// <summary>The groups the mod has a label for, in the order the Race filter lists them.</summary>
    public static readonly IReadOnlyList<string> KnownGroupIds =
        new[] { "men", "elves", "dwarves", "orcs", "goblins", "uruks", "trolls", "maiar" };

    public static bool IsKnownGroup(string? id) => id != null && KnownGroupIds.Contains(id);

    /// <summary>[18, b0) young, [b0, b1) adult, [b1, b2) mature, [b2, b3) elder, b3 and over venerable. Null below 18.</summary>
    public static LifeStage? StageFor(IReadOnlyList<float> bounds, float age)
    {
        if (bounds.Count != 4 || !FiniteFloatValidator.IsFinite(age) || age < FirstStageAge) return null;
        if (age < bounds[0]) return LifeStage.Young;
        if (age < bounds[1]) return LifeStage.Adult;
        if (age < bounds[2]) return LifeStage.Mature;
        if (age < bounds[3]) return LifeStage.Elder;
        return LifeStage.Venerable;
    }

    public static HeroFilterGroup? FindGroupForRace(HeroFilterConfig config, string? raceName) =>
        string.IsNullOrWhiteSpace(raceName)
            ? null
            : config.Groups.FirstOrDefault(g => g.Races.Any(r => string.Equals(r, raceName, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// The group of a race id, or null for an invalid id or a race in no group. The id is validated BEFORE the name
    /// lookup: <c>GetRaceNameFromId</c> answers "human" for an unknown id, which would file it under Men.
    /// </summary>
    public static string? GroupIdForRaceId(HeroFilterConfig config, IRaceManager races, int raceId)
    {
        if (!races.IsValidRaceId(raceId)) return null;
        return FindGroupForRace(config, races.GetRaceNameFromId(raceId))?.Id;
    }

    public static bool MatchesRace(HeroFilterConfig config, IRaceManager races, string groupId, int raceId) =>
        GroupIdForRaceId(config, races, raceId) == groupId;

    /// <summary>The hero's own group brackets, else the default group's.</summary>
    public static bool MatchesStage(HeroFilterConfig config, IRaceManager races, LifeStage stage, int raceId, float age)
    {
        var groupId = GroupIdForRaceId(config, races, raceId) ?? config.DefaultGroup;
        var group = config.Groups.FirstOrDefault(g => g.Id == groupId);
        return group != null && StageFor(group.AgeUpperBounds, age) == stage;
    }
}
