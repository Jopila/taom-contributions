namespace TAOM.Features.InnRounds.Domain;

/// <summary>The two rounds a tavern keeper can be asked to pour: for the player's own party, or for everyone drinking in the tavern.</summary>
public enum RoundKind
{
    /// <summary>One drink for every man in the player's party; the party's morale rises.</summary>
    Party,

    /// <summary>One drink for everyone in the tavern; the player's clan gains renown.</summary>
    House,
}
