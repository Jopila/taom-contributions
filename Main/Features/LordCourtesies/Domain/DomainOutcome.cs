namespace TAOM.Features.LordCourtesies.Domain;

/// <summary>How a lord answered the question about his clan's holdings.</summary>
public enum DomainOutcome
{
    /// <summary>The question was not allowed (a rule failed, or the lord is gone); nothing happened.</summary>
    Unavailable,

    /// <summary>He named his holdings and they were marked on the map.</summary>
    Revealed,

    /// <summary>He is willing but his clan holds nothing; the player lost relation for the empty question.</summary>
    Landless,

    /// <summary>He does not know the player well enough; the player lost relation for asking.</summary>
    Refused,
}
