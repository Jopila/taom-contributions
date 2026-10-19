using System.Collections.Generic;
using TAOM.Features.Relations;

namespace TAOM.Adapters;

/// <summary>The player's raw stored relations with living lords, and a raw write (ADR-007).</summary>
public interface IPersonalRelationSeedAdapter
{
    /// <summary>
    /// One row per living lord other than the player, clanless ones included. Null when the campaign cannot be read (no
    /// campaign, an engine fault, logged), so the caller tries again later.
    /// </summary>
    IReadOnlyList<SeedRow>? Snapshot();

    /// <summary>
    /// Writes the player's raw relation with the hero (<c>Hero.SetPersonalRelation</c>, which clamps and writes the raw pair): no event, no
    /// notification, no diplomacy-model scaling. False for an unknown or dead hero or an engine fault (logged).
    /// </summary>
    bool SetRelation(string heroId, int value);
}
