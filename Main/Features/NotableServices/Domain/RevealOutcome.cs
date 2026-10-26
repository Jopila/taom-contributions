namespace TAOM.Features.NotableServices.Domain;

/// <summary>How a notable answered the request to point out the holds near him.</summary>
public enum RevealOutcome
{
    /// <summary>The request was not allowed (a gate closed, the player is short of gold, an engine fault); nothing was charged.</summary>
    Unavailable,

    /// <summary>He named holds, the player paid, and the holds are marked on the map.</summary>
    Revealed,

    /// <summary>His settlement is at war with the player; nothing was charged.</summary>
    AtWar,

    /// <summary>He does not know the player well enough; nothing was charged.</summary>
    Cold,

    /// <summary>Every hold he could name is already marked or too far; nothing was charged.</summary>
    NothingNew,
}
