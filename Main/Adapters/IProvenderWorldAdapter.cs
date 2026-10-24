using System.Collections.Generic;
using TAOM.Features.Provender.Models;

namespace TAOM.Adapters;

/// <summary>
/// The campaign facts the Provender-master rules read and the writes they make (ADR-007, docs/features/provender.md). Facts and
/// plain writes only: who may give an order, what is bought and what is slaughtered are the service's. Implementations keep no
/// engine object between calls. Main thread only.
/// </summary>
public interface IProvenderWorldAdapter
{
    /// <summary>The item and culture registries exist (a campaign is up).</summary>
    bool IsRegistryReady { get; }

    bool IsKnownItem(string itemId);

    bool IsKnownCulture(string cultureId);

    /// <summary>The display name of the item, or the id when the item is unknown.</summary>
    string ItemName(string itemId);

    /// <summary>The id of the hero the player is talking to, or null when there is none.</summary>
    string? ConversationPartnerId { get; }

    /// <summary>The id of the hero in the main party's Quartermaster role, or null: one field read, for the talk check.</summary>
    string? QuartermasterId { get; }

    /// <summary>A snapshot of the main party, or <see cref="ProvenderPartyFacts.None"/> when there is no campaign.</summary>
    ProvenderPartyFacts ReadParty();

    /// <summary>A snapshot of one settlement, or <see cref="ProvenderSettlementFacts.None"/> when the id names none.</summary>
    ProvenderSettlementFacts ReadSettlement(string settlementId);

    /// <summary>
    /// Buys up to <paramref name="count"/> units from the settlement's market for the player in one purchase (one gold notice),
    /// capped by the stock and by what the player's gold covers at the current price. Returns what landed and what was paid.
    /// </summary>
    PurchaseResult Buy(string settlementId, string itemId, int count);

    /// <summary>
    /// The price of each of the next <paramref name="count"/> units in buying order, each unit's rise included (a town's price
    /// climbs as its stock falls; a village's does not move). Shorter when the market holds fewer; empty when it cannot sell.
    /// </summary>
    IReadOnlyList<int> UnitPrices(string settlementId, string itemId, int count);

    /// <summary>Slaughters up to <paramref name="heads"/> of the livestock item; returns the heads removed.</summary>
    int Slaughter(string itemId, int heads);

    /// <summary>Takes up to <paramref name="gold"/> from the player as the host's pay; returns what was taken.</summary>
    int PayTroops(int gold);

    /// <summary>Adds morale to the main party, and marks it disorganized when <paramref name="disorganize"/> is set.</summary>
    void AddMorale(int morale, bool disorganize);

    /// <summary>Commits the open party screen, so a write made in a conversation opened from it survives Escape.</summary>
    void CommitPartyScreen();

    /// <summary>Shows the player one line saying what the Provender-master did.</summary>
    void Announce(ProvenderAnnouncement kind, int count, int gold);
}
