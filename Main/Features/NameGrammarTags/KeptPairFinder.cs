using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.NameGrammarTags;

/// <summary>Derives a language's kept pairs from its two row sets (an id both vanilla and TAOM carry with different text), because the shipped module has no copy of the kept-rows list.</summary>
public static class KeptPairFinder
{
    public static IReadOnlyList<KeptPair> Find(
        IReadOnlyDictionary<string, string> taomRows, IReadOnlyDictionary<string, string> vanillaRows)
    {
        var pairs = new List<KeptPair>();
        foreach (var row in taomRows)
        {
            if (!vanillaRows.TryGetValue(row.Key, out var vanilla))
                continue;
            if (string.IsNullOrWhiteSpace(row.Value) || string.IsNullOrWhiteSpace(vanilla))
                continue;
            if (string.Equals(row.Value, vanilla, StringComparison.Ordinal))
                continue;
            pairs.Add(new KeptPair(row.Key, vanilla, row.Value));
        }
        return pairs.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
    }
}
