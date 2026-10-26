using System.Collections.Generic;

namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Issuance state: the highest tier already issued (once-per-tier is MONOTONIC —
/// covering a tier covers every tier below it, so a demotion never re-issues)
/// plus one entry per physical item instance issued (duplicates preserved so the
/// discharge can reclaim each one). <c>PersistedEquipmentIssueLedger</c> is the
/// production implementation.
/// </summary>
public interface IEquipmentIssueLedger
{
    EnlistmentKitTier? HighestIssuedTier { get; }

    /// <summary>One entry per issued item INSTANCE — the same id twice means two items.</summary>
    IReadOnlyList<string> IssuedItemIds { get; }

    bool HasIssuedForTier(EnlistmentKitTier tier);

    void RecordIssue(EnlistmentKitTier tier, IReadOnlyList<string> itemIds);

    void Reset();
}
