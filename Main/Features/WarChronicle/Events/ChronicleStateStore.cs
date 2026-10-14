using System;
using System.Collections.Generic;
using TAOM.Core.Validation;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// The chronicle's per-campaign state: one <see cref="ChronicleEventRecord"/> per event, and the day the
/// chronicle first saw each anchoring phase (IsengardWar, FullWar) reached. The save carries the rows as the
/// <c>chronicle</c> array and the days as the <c>phases</c> object of the War Chronicle payload, read and
/// written by <c>WarChronicleSaveCodec</c>, which skips a bad row alone with a warning; the adoption rule
/// then re-creates a lost event row at session launch, and a lost phase day is recorded again on first
/// sight. A row for an id the configuration no longer lists is kept, so a save from a later release survives
/// a load in an earlier one.
///
/// <para>A process-lifetime singleton holding campaign state: <see cref="ResetForNewSession"/> runs from
/// <c>WarChronicleStateService.ResetForNewSession</c>, which the behavior's constructor calls.</para>
/// </summary>
public sealed class ChronicleStateStore
{
    private readonly Dictionary<string, ChronicleEventRecord> _records = new Dictionary<string, ChronicleEventRecord>(StringComparer.Ordinal);
    private readonly Dictionary<WarPhase, double> _phaseSeenDays = new Dictionary<WarPhase, double>();

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

    /// <summary>True for the phases an event can anchor on: IsengardWar (Phase1) and FullWar (Phase2).</summary>
    public static bool IsAnchorPhase(WarPhase phase) => phase == WarPhase.IsengardWar || phase == WarPhase.FullWar;

    /// <summary>The day the chronicle first saw the phase reached; null before, or for a phase nothing anchors on.</summary>
    public double? GetPhaseSeenDay(WarPhase phase) =>
        _phaseSeenDays.TryGetValue(phase, out var day) ? day : null;

    /// <summary>Records the day once: a later sighting keeps the first. Ignores other phases and a non-finite day.</summary>
    public void NotePhaseSeen(WarPhase phase, double day)
    {
        if (!IsAnchorPhase(phase) || !FiniteFloatValidator.IsFinite(day) || _phaseSeenDays.ContainsKey(phase))
            return;
        _phaseSeenDays[phase] = day;
    }

    /// <summary>The seen days for the save.</summary>
    public IReadOnlyDictionary<WarPhase, double> PhaseSeenSnapshot() => new Dictionary<WarPhase, double>(_phaseSeenDays);

    /// <summary>Replaces the seen days with the saved ones (already validated by the codec).</summary>
    public void RestorePhaseSeenDays(IEnumerable<KeyValuePair<WarPhase, double>> days)
    {
        _phaseSeenDays.Clear();
        foreach (var pair in days)
            NotePhaseSeen(pair.Key, pair.Value);
    }

    public void ResetForNewSession()
    {
        _records.Clear();
        _phaseSeenDays.Clear();
    }
}
