using System.Collections.Generic;
using TAOM.Features.TokensOfEsteem.Models;

namespace TAOM.Adapters;

/// <summary>
/// Goods and registries for a gift (ADR-007). Held goods are every stack in a catalog category, each flagged
/// <see cref="HeldGood.IsModified"/> when it carries an item modifier. <see cref="IPartyItemRosterAdapter.RemoveItem"/>
/// removes from the unmodified stack and nothing else, so <c>GiftService</c> lists only the unmodified ones and a "Sharp" or
/// "Battered" copy is never given by mistake. (Listing modified stacks is a follow-up.)
/// </summary>
public interface IGiftItemAdapter
{
    /// <summary>True once the campaign's item categories and cultures are loaded, so the catalog can be validated against them.</summary>
    bool IsRegistryReady { get; }

    /// <summary>True when an item category with this id exists.</summary>
    bool IsKnownCategory(string categoryId);

    /// <summary>True when a culture with this id exists.</summary>
    bool IsKnownCulture(string cultureId);

    /// <summary>The stacks in the main party's inventory whose item category is one of <paramref name="categoryIds"/>, modified ones flagged.</summary>
    IReadOnlyList<HeldGood> ReadHeldGoods(IReadOnlyCollection<string> categoryIds);

    /// <summary>
    /// Commits the party screen still open under the conversation, if any. A conversation started from the party screen
    /// leaves the screen's pre-conversation snapshot alive, and a later troop change followed by Cancel or Reset refills the
    /// item roster from that snapshot, which would hand a given item back (lessons/adapters-taleworlds-api.md, 2026-10-19).
    /// </summary>
    void CommitOpenPartyScreen();
}
