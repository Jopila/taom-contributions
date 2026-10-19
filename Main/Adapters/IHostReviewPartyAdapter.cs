using System.Collections.Generic;
using TAOM.Features.HostReview.Domain;

namespace TAOM.Adapters;

/// <summary>Reads the main party for Host Review: battle size and roster.</summary>
public interface IHostReviewPartyAdapter
{
    /// <summary>The number of men the engine lets one side spawn in a field battle.</summary>
    int RealBattleSize { get; }

    /// <summary>One entry per roster line; empty when the party cannot be read.</summary>
    IReadOnlyList<HostRosterEntry> GetRoster();
}
