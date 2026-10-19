using System.Collections.Generic;

namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// In-memory <see cref="IEquipmentIssueLedger"/>. Not persisted — the orchestrator
/// wires this state into the core EnlistmentStore's save section in a later phase;
/// the interface boundary exists so the service never learns which.
/// </summary>
public sealed class InMemoryEquipmentIssueLedger : IEquipmentIssueLedger
{
    private readonly List<string> _issuedItemIds = new List<string>();

    public EnlistmentKitTier? HighestIssuedTier { get; private set; }

    public IReadOnlyList<string> IssuedItemIds => _issuedItemIds;

    public bool HasIssuedForTier(EnlistmentKitTier tier)
        => HighestIssuedTier.HasValue && tier <= HighestIssuedTier.Value;

    public void RecordIssue(EnlistmentKitTier tier, IReadOnlyList<string> itemIds)
    {
        if (!HighestIssuedTier.HasValue || tier > HighestIssuedTier.Value)
            HighestIssuedTier = tier;

        if (itemIds == null)
            return;
        foreach (var itemId in itemIds)
        {
            if (!string.IsNullOrEmpty(itemId))
                _issuedItemIds.Add(itemId);
        }
    }

    public void Reset()
    {
        HighestIssuedTier = null;
        _issuedItemIds.Clear();
    }
}
