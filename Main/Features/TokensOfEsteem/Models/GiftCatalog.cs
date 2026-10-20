using System;
using System.Collections.Generic;

namespace TAOM.Features.TokensOfEsteem.Models;

/// <summary>
/// A taste: heroes of any listed race or any listed culture value the goods and add <see cref="Bonus"/>. Race names and culture
/// ids are lowercase data ids, matched without regard to case.
/// </summary>
public sealed record TasteGroup(IReadOnlyList<string> Races, IReadOnlyList<string> Cultures, int Bonus);

/// <summary>One item category the catalog lists: what a lord values it at (1 to 10) and who prizes it more.</summary>
public sealed record GiftCatalogEntry(string CategoryId, int LordBase, IReadOnlyList<TasteGroup> Tastes)
{
    /// <summary>
    /// The taste bonus for this recipient: the highest bonus among the groups that list the hero's race or culture, 0 when none
    /// does. Groups do not stack. A blank race or culture matches nothing.
    /// </summary>
    public int TasteBonus(string raceName, string cultureId)
    {
        var best = 0;
        foreach (var group in Tastes)
        {
            if (group.Bonus > best && (Lists(group.Races, raceName) || Lists(group.Cultures, cultureId)))
                best = group.Bonus;
        }
        return best;
    }

    private static bool Lists(IReadOnlyList<string> ids, string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;
        foreach (var candidate in ids)
        {
            if (string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

/// <summary>
/// The validated gift catalog, keyed by item category id (<c>ItemObject.ItemCategory.StringId</c>), so a renamed item keeps its
/// place. Built by <c>GiftCatalogProvider</c>, which drops every id the game does not know.
/// </summary>
public sealed class GiftCatalog
{
    public static readonly GiftCatalog Empty = new(Array.Empty<GiftCatalogEntry>());

    private readonly Dictionary<string, GiftCatalogEntry> _entries = new(StringComparer.Ordinal);

    public GiftCatalog(IEnumerable<GiftCatalogEntry> entries)
    {
        foreach (var entry in entries)
            _entries[entry.CategoryId] = entry;
    }

    public int Count => _entries.Count;

    public IReadOnlyCollection<string> CategoryIds => _entries.Keys;

    public bool TryGet(string categoryId, out GiftCatalogEntry entry)
    {
        entry = null!;
        return !string.IsNullOrEmpty(categoryId) && _entries.TryGetValue(categoryId, out entry!);
    }
}
