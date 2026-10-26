using System;
using System.Collections.Generic;
using TAOM.Features.Execution;

namespace TAOM.Features.Provender.Models;

/// <summary>
/// One town the Provender-master could sell at, as the adapter reads it (docs/features/provender.md "Remote selling"). The
/// adapter reports what the campaign says and decides nothing: which towns are offered is the service's.
/// </summary>
public sealed record SaleTownFacts
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The engine's travel distance from the main party. An unreachable town comes back as a huge finite placeholder (the
    /// native path search's 1e30, or 1e8 or float.MaxValue), and the ranking rule also drops a distance of 0 or less.
    /// </summary>
    public float Distance { get; init; }

    public int Gold { get; init; }

    public bool IsUnderSiege { get; init; }

    public bool AtWarWithPlayer { get; init; }

    /// <summary>The town denies the player full access (vanilla's access model).</summary>
    public bool RefusesPlayer { get; init; }

    /// <summary>The town the main party stands in: the player can trade there directly, so it is not offered.</summary>
    public bool IsCurrent { get; init; }

    /// <summary>The town's map faction id and that faction's culture, for the alignment service.</summary>
    public SideIds Side { get; init; } = SideIds.None;
}

/// <summary>One town on offer: its id, its name and the Provender-master's cut there. The list order is the rank.</summary>
public sealed record SaleOffer(string TownId, string TownName, int FeePercent);

/// <summary>The towns on offer now, or why selling is closed.</summary>
public sealed record SaleQuote(ProvenderBlock Block, IReadOnlyList<SaleOffer> Offers)
{
    public static SaleQuote Closed(ProvenderBlock block) => new(block, Array.Empty<SaleOffer>());
}

/// <summary>What a closed sale screen did: the units the town took and the gold the player gained.</summary>
public readonly record struct SaleOutcome(int Units, int Gold);
