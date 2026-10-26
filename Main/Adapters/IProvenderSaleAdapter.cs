using System;
using System.Collections.Generic;
using TAOM.Features.Execution;
using TAOM.Features.Provender.Models;

namespace TAOM.Adapters;

/// <summary>
/// The campaign side of the Provender-master's remote sale (#909, docs/features/provender.md "Remote selling"): the towns'
/// facts, the player's side, and the sell-only trade screen. Facts and plain actions only; which towns are offered and at
/// what cut is the service's. Keeps no engine object between calls. Main thread only.
/// </summary>
public interface IProvenderSaleAdapter
{
    /// <summary>Every town, with its distance from the main party and the facts the eligibility rule reads.</summary>
    IReadOnlyList<SaleTownFacts> ReadTowns();

    /// <summary>The player's clan map faction and its culture, for the alignment service.</summary>
    SideIds PlayerSide { get; }

    /// <summary>
    /// Opens, once the conversation has ended, a trade screen titled with the town where the player can only sell (and buy back
    /// what was sold there) at the town's price less <paramref name="feePercent"/>. When it closes, the town takes the sold
    /// goods and pays for them, and <paramref name="onClosed"/> hears what was sold. False when the town cannot be found.
    /// </summary>
    bool OpenScreen(string townId, int feePercent, Action<SaleOutcome> onClosed);
}
