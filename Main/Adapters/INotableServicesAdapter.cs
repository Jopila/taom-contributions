using System;
using System.Collections.Generic;
using TAOM.Features.NotableServices.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The notable in the conversation and the three services' engine work (docs/features/notable-services.md): his cheap
/// facts, the notables who have not met the player, the purse, the gift screen, the writes to a settlement and the notices.
/// Boundary only: ids and plain values, no Hero (ADR-007). Every rule belongs to <c>NotableServicesService</c>.
/// </summary>
public interface INotableServicesAdapter
{
    /// <summary>The id of the hero the player is talking to, or null when there is none.</summary>
    string? ConversationPartnerId { get; }

    /// <summary>The cheap facts about a living hero who is not a captive, or null for a missing, dead or captive hero. Whether he may offer the services is the service's call (IsNotable, IsPreacher, InPlayersSettlement).</summary>
    NotableSnapshot? Snapshot(string heroId);

    /// <summary>
    /// The ids of the settlement's living notables whom the player has not met, the speaker included when he is unmet. Reads
    /// only. Leaving the speaker out is the service's rule.
    /// </summary>
    IReadOnlyList<string> UnmetNotables(string settlementId);

    /// <summary>
    /// True while any party screen is on the game-state stack, under a conversation or not. The read cannot throw.
    /// </summary>
    bool PartyScreenIsOpen { get; }

    /// <summary>True when the player's party holds at least one regular troop or regular prisoner (heroes do not count).</summary>
    bool PlayerHasMenToGive();

    /// <summary>The player's gold.</summary>
    int PlayerGold { get; }

    /// <summary>
    /// Pays <paramref name="price"/> gold from the player to the notable. True when the purse lost exactly the price, also when
    /// a trade-event listener throws after the gold moved (logged). False, with the purse unchanged, for a short purse, an
    /// unknown or dead notable, a price of 0 or less, or a fault before the gold moved; a partial loss is given back.
    /// </summary>
    bool PayNotable(string heroId, int price);

    /// <summary>Marks each hero as met by the player.</summary>
    void MarkMet(IReadOnlyList<string> heroIds);

    /// <summary>Runs the action once, when the current conversation has ended (the engine's end-of-conversation one-shot).</summary>
    void RunAfterConversation(Action action);

    /// <summary>
    /// Opens the party screen to give men to a settlement: the player's troops and prisoners move to a dummy side, heroes
    /// never. <paramref name="canTransfer"/> is asked when the screen builds a row and once per stack in Transfer All,
    /// never on a click or a drag; <paramref name="canFinish"/> is the done condition, re-checked after every move, and the only
    /// check that holds a gift to the cap; <paramref name="onDone"/> runs on every commit of the screen with what was given
    /// (Done, and the forced commit when a companion is removed), never on Cancel. Troop upgrades are off and nothing is donated to a party. <paramref name="menLimit"/> is the most men the screen
    /// shows as each gift panel's room, which bounds a Transfer All on that panel only (the engine has no combined limit).
    /// </summary>
    void OpenGiftScreen(string settlementId, int menLimit, Func<GiftTransfer, bool> canTransfer, Func<GiftTally, bool> canFinish,
        Action<GiftTally> onDone);

    /// <summary>Adds to the settlement's militia.</summary>
    void AddMilitia(string settlementId, float amount);

    /// <summary>Adds to a village's hearth.</summary>
    void AddHearth(string settlementId, float amount);

    /// <summary>Adds to a town's prosperity.</summary>
    void AddProsperity(string settlementId, float amount);

    /// <summary>The player hero's id; null before a campaign. The skill experience goes through <c>IHeroSkillXpAdapter</c>.</summary>
    string? PlayerHeroId { get; }

    /// <summary>The line after a gift: how many men the settlement took. The adapter names the settlement from its id.</summary>
    void ShowGiftNotice(string settlementId, int men);

    /// <summary>
    /// The line after the relation with a hero changed by <paramref name="change"/> (the amount that landed): vanilla's "with
    /// notable" text, with the new relation. Nothing for a change of 0.
    /// </summary>
    void ShowRelationNotice(string heroId, int change);

    /// <summary>The line after Roguery experience was earned.</summary>
    void ShowRogueryNotice(int xp);

    /// <summary>The line after an introduction.</summary>
    void ShowIntroducedNotice(int count);
}
