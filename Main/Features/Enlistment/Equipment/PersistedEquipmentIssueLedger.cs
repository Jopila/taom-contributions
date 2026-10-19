using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Enlistment.Content;

namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// The shipping ledger: issuance state lives in the content record, so it round-trips
/// through <c>_taom_enlistment_content</c> SyncData. The in-memory implementation survived
/// only until the content store existed — without persistence a full game restart reset
/// the ledger and re-allowed one free kit draw per tier.
/// </summary>
public class PersistedEquipmentIssueLedger : IEquipmentIssueLedger
{
    private readonly IEnlistmentContentStore _contentStore;

    public PersistedEquipmentIssueLedger(IEnlistmentContentStore contentStore)
    {
        _contentStore = contentStore;
    }

    public EnlistmentKitTier? HighestIssuedTier
    {
        get
        {
            var raw = _contentStore.Record.HighestIssuedKitTier;
            if (!raw.HasValue)
                return null;
            // Defensive: a hand-edited save could carry a tier ordinal we don't define.
            return System.Enum.IsDefined(typeof(EnlistmentKitTier), raw.Value)
                ? (EnlistmentKitTier?)raw.Value
                : null;
        }
    }

    public IReadOnlyList<string> IssuedItemIds => _contentStore.Record.IssuedEquipmentItemIds;

    public bool HasIssuedForTier(EnlistmentKitTier tier)
    {
        var highest = HighestIssuedTier;
        // Monotonic: covering a tier covers everything below it, so a demotion never re-issues.
        return highest.HasValue && (int)highest.Value >= (int)tier;
    }

    public void RecordIssue(EnlistmentKitTier tier, IReadOnlyList<string> itemIds)
    {
        var record = _contentStore.Record;
        var highest = HighestIssuedTier;
        if (!highest.HasValue || (int)tier > (int)highest.Value)
            record.HighestIssuedKitTier = (int)tier;

        if (itemIds != null && itemIds.Count > 0)
            record.IssuedEquipmentItemIds.AddRange(itemIds.Where(id => !string.IsNullOrEmpty(id)));
    }

    public void Reset()
    {
        var record = _contentStore.Record;
        record.HighestIssuedKitTier = null;
        record.IssuedEquipmentItemIds.Clear();
    }
}
