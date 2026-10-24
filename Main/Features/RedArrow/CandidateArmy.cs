namespace TAOM.Features.RedArrow;

/// <summary>Which army a candidate party belongs to, as vanilla's checks 4 and 5 compare it with the player's own.</summary>
public enum CandidateArmy
{
    /// <summary>The party is in no army.</summary>
    None = 0,

    /// <summary>The party is in the same army as the player's party (vanilla: "Already in army").</summary>
    PlayersArmy,

    /// <summary>The party is in an army that is not the player's party's (vanilla: "Already in another army").</summary>
    OtherArmy,
}
