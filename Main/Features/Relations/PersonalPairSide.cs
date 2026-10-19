namespace TAOM.Features.Relations;

/// <summary>Which side of a relation pair, if any, personal relations takes back from its clan leader.</summary>
public enum PersonalPairSide
{
    /// <summary>Keep the engine's pair: the feature is inactive or the player is not one side of it.</summary>
    None,

    /// <summary>The first effective hero is the player: the second goes back to the hero it was asked about.</summary>
    UnmapSecond,

    /// <summary>The second effective hero is the player: the first goes back to the hero it was asked about.</summary>
    UnmapFirst,
}
