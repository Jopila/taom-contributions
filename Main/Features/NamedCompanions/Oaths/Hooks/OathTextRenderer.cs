using System;
using System.Collections.Generic;
using TAOM.Features.NamedCompanions.Domain;

namespace TAOM.Features.NamedCompanions.Oaths.Hooks;

/// <summary>Turns hire terms and missing parts into the text a conversation variable holds.</summary>
internal static class OathTextRenderer
{
    /// <summary>
    /// The price and every item the companion asks for, as in "3000{GOLD_ICON}, 2 x Mithril Ore". Null, never an
    /// empty string, when there is nothing to list: the caller then lets vanilla hiring answer.
    /// </summary>
    public static string Terms(HireTermsDefinition terms, int price, Func<string, string> itemName)
    {
        var parts = new List<string>();
        if (price > 0) parts.Add(Gold(price));
        if (terms?.Items != null)
            foreach (var item in terms.Items)
                parts.Add(Item(item.ItemId, item.Count, itemName));
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>What the player still lacks, in the same form.</summary>
    public static string Missing(IEnumerable<MissingTerm> missing, Func<string, string> itemName)
    {
        var parts = new List<string>();
        foreach (var term in missing)
            parts.Add(term.Kind == MissingTermKind.Gold ? Gold(term.Count) : Item(term.ItemId, term.Count, itemName));
        return string.Join(", ", parts);
    }

    private static string Gold(int amount) => amount + "{GOLD_ICON}";

    private static string Item(string itemId, int count, Func<string, string> itemName)
    {
        var name = itemName?.Invoke(itemId);
        return count + " x " + (string.IsNullOrEmpty(name) ? itemId : name);
    }
}
