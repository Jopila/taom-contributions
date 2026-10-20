using TAOM.Features.TokensOfEsteem.Models;

namespace TAOM.Adapters;

/// <summary>
/// The heroes of a gift, as plain facts (ADR-007): who the player is, who would receive, the gold between them and the
/// relation notice. Every fact is raw; <c>GiftService</c> decides what each one means.
/// </summary>
public interface IGiftHeroAdapter
{
    /// <summary>The player's facts, or null outside a campaign.</summary>
    GiftPlayerFacts? ReadPlayer();

    /// <summary>The recipient's facts, or null for an unknown or dead hero, or the player.</summary>
    GiftRecipientFacts? ReadRecipient(string heroId);

    /// <summary>
    /// Moves <paramref name="amount"/> gold from the player to the hero and returns true; false, with nothing moved, when the
    /// hero is unknown, the amount is not positive, or the player holds less.
    /// </summary>
    bool PayGoldToHero(string heroId, int amount);

    /// <summary>
    /// Shows vanilla's "your relation with this hero increased (or decreased)" line for <paramref name="change"/>. Callers pass
    /// the change that landed, never 0.
    /// </summary>
    void ShowRelationChange(string heroId, int change);
}
