using System;
using System.Collections.Generic;
using TAOM.Features.LordCourtesies.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The lord in the conversation and the two courtesies' engine work (docs/features/lord-courtesies.md): his cheap
/// facts, his troops, the donate party screen, the engine's worth of a gift and the one-line holdings notice.
/// Boundary only: ids and plain values, no Hero.
/// </summary>
public interface ILordCourtesyAdapter
{
    /// <summary>The id of the hero the player is talking to, or null when there is none.</summary>
    string? ConversationPartnerId { get; }

    /// <summary>The cheap facts about a living, free lord, or null for a missing, dead or captive hero.</summary>
    LordCourtesySnapshot? Snapshot(string heroId);

    /// <summary>The members of the lord's own party: one entry per roster element, not folded; heroes flagged.</summary>
    IReadOnlyList<TroopCount> MemberTroops(string heroId);

    /// <summary>His display name, for the hand-over notice.</summary>
    string LordName(string heroId);

    /// <summary>
    /// The hero a relation change with this lord really lands on (<c>DiplomacyModel.GetHeroesForEffectiveRelation</c>:
    /// the lord himself with Personal Relations on, his clan leader off), or null when unknown.
    /// </summary>
    string? RelationOwnerId(string heroId);

    /// <summary>
    /// What vanilla's donate screen would report for these troops: the sum of count times
    /// <c>PrisonerDonationModel.CalculateInfluenceGainAfterTroopDonation</c>, truncated to an int as the screen does.
    /// </summary>
    int GiftInfluence(IReadOnlyList<TroopCount> donated);

    /// <summary>Runs the action once, when the current conversation has ended (the engine's end-of-conversation one-shot).</summary>
    void RunAfterConversation(Action action);

    /// <summary>
    /// Opens the donate-style party screen with the lord's party on the left: regular troops move from the player
    /// to the lord only, no influence is paid. <paramref name="onDone"/> runs on every commit of the screen (the
    /// engine's done step, which Done and map-bar navigation reach; talking from the screen is refused), never on Cancel.
    /// </summary>
    void OpenGiftScreen(string heroId, Action onDone);

    /// <summary>The one-line count of holdings marked on the map.</summary>
    void ShowHoldingsMarked(int count);
}
