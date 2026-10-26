using System.Collections.Generic;

namespace TAOM.Features.Encyclopedia;

/// <summary>
/// The encyclopedia hero filters (#907), as read from <c>ModuleData/encyclopedia/hero_filters.json</c>: which races
/// make up each race filter, and where each group's four life-stage boundaries sit.
/// </summary>
public class HeroFilterConfig
{
    /// <summary>The group whose brackets a hero of an unknown or ungrouped race uses. Empty when no group is valid.</summary>
    public string DefaultGroup { get; set; } = "";

    public List<HeroFilterGroup> Groups { get; set; } = new List<HeroFilterGroup>();
}

public class HeroFilterGroup
{
    /// <summary>One of <see cref="HeroFilterRules.KnownGroupIds"/>; the label text lives in C#.</summary>
    public string Id { get; set; } = "";

    /// <summary>Race names as the FaceGen race list spells them (the RaceAge config's keys).</summary>
    public List<string> Races { get; set; } = new List<string>();

    /// <summary>Four strictly increasing upper bounds: young, adult, mature and elder end at these ages.</summary>
    public List<float> AgeUpperBounds { get; set; } = new List<float>();
}
