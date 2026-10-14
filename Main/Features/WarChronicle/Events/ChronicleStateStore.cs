using System;
using System.Collections.Generic;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// The chronicle's per-campaign state: one <see cref="ChronicleEventRecord"/> per event. The save carries the
/// rows as the <c>chronicle</c> array of the War Chronicle payload, read and written by
/// <c>WarChronicleSaveCodec</c>, which skips a bad row alone with a warning; the adoption rule then
/// re-creates it at session launch. A row for an id the configuration no longer lists is kept, so a save
/// from a later release survives a load in an earlier one.
///
/// <para>A process-lifetime singleton holding campaign state: <see cref="ResetForNewSession"/> runs from
/// <c>WarChronicleStateService.ResetForNewSession</c>, which the behavior's constructor calls.</para>
/// </summary>
public sealed class ChronicleStateStore
{
    private readonly Dictionary<string, ChronicleEventRecord> _records = new Dictionary<string, ChronicleEventRecord>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, ChronicleEventRecord> Records => _records;

    public ChronicleEventRecord? Get(string? id) =>
        id != null && _records.TryGetValue(id, out var record) ? record : null;

    /// <summary>Adds the record, or replaces the one with the same id.</summary>
    public void Set(ChronicleEventRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.Id))
            return;
        _records[record.Id] = record;
    }

    /// <summary>The rows for the save.</summary>
    public IReadOnlyList<ChronicleEventRecord> Snapshot() => new List<ChronicleEventRecord>(_records.Values);

    /// <summary>Replaces the store with the saved rows (already validated by the codec).</summary>
    public void Restore(IEnumerable<ChronicleEventRecord> records)
    {
        _records.Clear();
        foreach (var record in records)
            Set(record);
    }

    public void ResetForNewSession() => _records.Clear();
}
