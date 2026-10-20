using System.Collections.Generic;
using TAOM.Features.TokensOfEsteem.Models;

namespace TAOM.Adapters;

/// <summary>
/// The conversation statics a gift list needs (ADR-007): the partner, the repeated objects and the row text. The list is the
/// first repeatable player line in TAOM, modelled on vanilla's fief grant (<c>CompanionRolesCampaignBehavior</c>).
/// </summary>
public interface IGiftConversationAdapter
{
    /// <summary>The id of the hero the player is talking to, or null when there is none.</summary>
    string? PartnerId { get; }

    /// <summary>Hands the engine the objects its repeatable line lists, in order.</summary>
    void SetOptions(IReadOnlyList<GiftOption> options);

    /// <summary>The option the engine is processing for the current row, or null.</summary>
    GiftOption? CurrentOption { get; }

    /// <summary>The option the player chose, or null.</summary>
    GiftOption? SelectedOption { get; }

    /// <summary>Writes the row text for <paramref name="option"/> into the line being rendered.</summary>
    void WriteRow(GiftOption option);
}
