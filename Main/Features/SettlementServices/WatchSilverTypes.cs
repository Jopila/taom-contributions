using System.Collections.Generic;

namespace TAOM.Features.SettlementServices;

/// <summary>
/// Why Watch-Silver is not on offer, or not payable, right now. <see cref="WatchSilverService"/> returns the first that applies in
/// this order: Disabled, NotSinglePlayer, NotEligible, Problem, AtCeiling. NotEnoughGold is only ever the answer to a payment.
/// </summary>
public enum WatchSilverBlock
{
    None,
    Disabled,
    NotSinglePlayer,
    NotEligible,
    Problem,
    AtCeiling,
    NotEnoughGold,
}

/// <summary>
/// The conversation hero's governed fief as plain values: whose fief it is, whether the governor stands in it and whether the
/// player does, and the two numbers the price reads. A castle is a <c>Town</c> to the engine and is priced like a town.
/// </summary>
/// <param name="Id">The settlement's string id.</param>
/// <param name="OwnedByPlayerClan">True when the fief's owner clan is the player's clan.</param>
/// <param name="GovernorInFief">True when the governor currently stands in the fief he governs.</param>
/// <param name="PlayerInFief">True when the player currently stands in that fief.</param>
/// <param name="Loyalty">The fief's loyalty, as the engine holds it (it may be a non-finite value after a bad write elsewhere).</param>
/// <param name="Militia">The fief's militia, as the engine holds it (it may be zero after a siege, or odd).</param>
public sealed record GovernedFiefSnapshot(string Id, bool OwnedByPlayerClan, bool GovernorInFief, bool PlayerInFief, float Loyalty,
    float Militia);

/// <summary>One offer: the loyalty it adds and its price in denars.</summary>
public readonly record struct WatchSilverTier(int Points, int Price);

/// <summary>What the governor can offer now: the gate, the loyalty to name and the offers (empty unless the gate is None).</summary>
public sealed record WatchSilverQuote(WatchSilverBlock Block, float Loyalty, IReadOnlyList<WatchSilverTier> Tiers)
{
    /// <summary>The offer line is shown while the gate is open or only the ceiling stops it (then it is greyed with its reason).</summary>
    public bool IsOffered => Block == WatchSilverBlock.None || Block == WatchSilverBlock.AtCeiling;
}

/// <summary>
/// What a payment did. On success <see cref="Loyalty"/> is the value read back from the fief after the write; the governor's reply
/// states only that loyalty, never the quote.
/// </summary>
public sealed record WatchSilverResult(WatchSilverBlock Block, float Loyalty)
{
    public bool Success => Block == WatchSilverBlock.None;
}
