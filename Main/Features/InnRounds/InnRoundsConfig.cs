using System.Collections.Generic;

namespace TAOM.Features.InnRounds;

/// <summary>
/// JSON DTO for <c>inn_rounds/inn_rounds.json</c>, validated by <see cref="InnRoundsConfigProvider"/>
/// (docs/features/inn-rounds.md). Every number is a float so the shared finite-number check applies; the counts and prices are
/// rounded where they are used.
/// </summary>
public class InnRoundsConfig
{
    /// <summary>The most rounds (party and house together) the player can stand in one campaign day. Range [1, 20].</summary>
    public float MaxRoundsPerDay { get; set; } = 3f;

    /// <summary>The ceiling on the price of one head's drink, whatever the town's market asks. Range [1, 1000].</summary>
    public float MaxPricePerHead { get; set; } = 35f;

    /// <summary>The price of one head's drink when the town's market cannot be asked. Range [1, 1000].</summary>
    public float FallbackPricePerHead { get; set; } = 27f;

    /// <summary>The lowest morale a party round gives, before the MCM scale. Range [0, 100], at most <see cref="MoraleMax"/>.</summary>
    public float MoraleMin { get; set; } = 20f;

    /// <summary>The highest morale a party round gives, before the MCM scale. Range [0, 100].</summary>
    public float MoraleMax { get; set; } = 40f;

    /// <summary>The lowest renown a house round gives, before the MCM scale. Range [0, 50], at most <see cref="RenownMax"/>.</summary>
    public float RenownMin { get; set; } = 1f;

    /// <summary>The highest renown a house round gives, before the MCM scale. Range [0, 50].</summary>
    public float RenownMax { get; set; } = 3f;

    /// <summary>The lowest Charm experience a round gives. Range [0, 1000], at most <see cref="CharmXpMax"/>.</summary>
    public float CharmXpMin { get; set; } = 20f;

    /// <summary>The highest Charm experience a round gives. Range [0, 1000].</summary>
    public float CharmXpMax { get; set; } = 60f;

    /// <summary>
    /// A culture id to the drink its taverns pour, <c>beer</c> or <c>wine</c> (the only drink goods in the game). A culture
    /// that is not listed pours beer. Keys are lower-cased on load.
    /// </summary>
    public Dictionary<string, string> CultureDrinks { get; set; } = new();
}
