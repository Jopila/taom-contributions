using System;
using System.Collections.Generic;

namespace TAOM.Features.Provender.Models;

/// <summary>
/// One ration the host can be given for its spirits: the item, how many troops one item serves and the morale it adds. The
/// races and cultures name who prefers it; <see cref="IsDefault"/> marks the one everybody else is given.
/// </summary>
public sealed record RationEntry(
    string Id,
    string ItemId,
    int TroopsPerItem,
    int Morale,
    bool IsDefault,
    IReadOnlyList<string> Races,
    IReadOnlyList<string> Cultures)
{
    public bool PrefersRace(string? raceName) => Lists(Races, raceName);

    public bool PrefersCulture(string? cultureId) => Lists(Cultures, cultureId);

    private static bool Lists(IReadOnlyList<string> ids, string? id)
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

/// <summary>The validated rations, in file order. Built by <c>RationCatalogProvider</c>, which drops every id the game does not know.</summary>
public sealed class RationCatalog
{
    public static readonly RationCatalog Empty = new(Array.Empty<RationEntry>());

    public RationCatalog(IReadOnlyList<RationEntry> entries) => Entries = entries;

    public IReadOnlyList<RationEntry> Entries { get; }

    public int Count => Entries.Count;
}
