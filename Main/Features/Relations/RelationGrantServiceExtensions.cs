using System;
using System.Collections.Generic;

namespace TAOM.Features.Relations;

/// <summary>
/// Shared rules for a caller that lists several heroes and wants one grant per relation owner. An extension, not a member of
/// <see cref="IRelationGrantService"/>, so every existing fake of the seam keeps working.
/// </summary>
public static class RelationGrantServiceExtensions
{
    /// <summary>
    /// Grants the strongest ask once per relation owner and returns how many landed (a non-zero change). The engine lands a change
    /// on the owner (the clan leader unless personal relations are on), so two asks to kin would otherwise stack on one person.
    /// A null target, a dead hero, the player and a zero delta are skipped. Asks are grouped by <see cref="RelationTarget.RelationOwnerId"/>
    /// (the hero's own id when it is blank, compared Ordinal), in the order each owner first appears; per owner the largest
    /// <c>|delta|</c> wins and the first listed wins a tie. <paramref name="reasonForLog"/> goes to the TAOM log only.
    /// </summary>
    public static int GrantStrongestPerOwner(this IRelationGrantService grants, IEnumerable<RelationAsk> asks, string reasonForLog)
    {
        // Dictionary plus an order list keeps the owners in the order each first appears; a tie keeps the earlier ask because only a
        // strictly larger magnitude replaces it. Math.Abs on a long, so int.MinValue does not throw.
        var strongest = new Dictionary<string, RelationAsk>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var ask in asks)
        {
            var target = ask.Target;
            if (target == null || !target.IsAlive || target.IsPlayer || ask.Delta == 0)
                continue;
            var owner = string.IsNullOrEmpty(target.RelationOwnerId) ? target.HeroId : target.RelationOwnerId;
            if (!strongest.TryGetValue(owner, out var current))
            {
                strongest[owner] = ask;
                order.Add(owner);
            }
            else if (Math.Abs((long)ask.Delta) > Math.Abs((long)current.Delta))
            {
                strongest[owner] = ask;
            }
        }

        var landed = 0;
        foreach (var owner in order)
        {
            var ask = strongest[owner];
            if (grants.Grant(ask.Target!.HeroId, ask.Delta, reasonForLog) != 0)
                landed++;
        }
        return landed;
    }
}
