using System;
using System.Collections.Generic;

namespace TAOM.Features.HiddenHosts;

/// <summary>
/// The counts the player remembers (#846 phase 3, docs/features/hidden-hosts.md "Memory"): one <see cref="MemoryRow"/> per
/// party and per settlement, keyed by StringId. Plain data, whole campaign hours, no engine type, main thread only. The
/// service owns one and decides what a reading means; this class only applies the record, visibility and fade rules, so
/// each can be checked at its exact hour.
/// </summary>
internal sealed class HiddenHostsMemory
{
    /// <summary>Rows kept for parties; destroyed parties are removed, the rest age out.</summary>
    internal const int MaxParties = 4096;

    internal const int MaxSettlements = 2048;

    private readonly Dictionary<string, MemoryRow> _parties = new();
    private readonly Dictionary<string, MemoryRow> _settlements = new();
    private readonly List<string> _scratch = new();

    /// <summary>Inserts refused because the table was full of fresh rows. The behavior logs it once per session.</summary>
    internal int SkippedInserts { get; private set; }

    internal int Count(MemoryKind kind) => Table(kind).Count;

    internal bool TryGet(MemoryKind kind, string id, out MemoryRow row) => Table(kind).TryGetValue(id, out row!);

    /// <summary>
    /// Applies one reading of <paramref name="count"/> with its <paramref name="known"/> leading digits. An existing row is
    /// overwritten when the reading is at least as good (known digits no fewer, or the count read in full) or when it
    /// contradicts the memory (<see cref="HiddenHostsMath.Agrees"/>: another digit count or other leading digits), so the
    /// live view wins over a stale count even when no digit is readable; a worse reading that agrees leaves the row as it
    /// was. A reading with no digit never makes a new row. The clock starts fresh while the subject is
    /// <paramref name="visible"/>, else at the stamp plus the hold. Returns the row, or null when there is none: no digit and
    /// no row, or the table is full of fresh rows and the insert was skipped.
    /// </summary>
    internal MemoryRow? Record(MemoryKind kind, string id, int count, int known, int nowHour, bool visible, int scout)
    {
        var table = Table(kind);
        int digits = HiddenHostsMath.DigitCount(count);
        known = Math.Min(Math.Max(known, 0), digits);

        if (table.TryGetValue(id, out var row))
        {
            if (known >= row.Known || known >= digits || !HiddenHostsMath.Agrees(count, known, row.Count))
                Overwrite(row, count, known, nowHour, visible, scout);
            return row;
        }

        if (known < 1) return null;
        if (table.Count >= Cap(kind) && !EvictStalest(table))
        {
            SkippedInserts++;
            return null;
        }

        row = new MemoryRow();
        Overwrite(row, count, known, nowHour, visible, scout);
        table[id] = row;
        return row;
    }

    /// <summary>Stores a decoded row as it is. A duplicate id keeps the last; false when the table is full and the id is new.</summary>
    internal bool Put(MemoryKind kind, string id, MemoryRow row)
    {
        var table = Table(kind);
        if (table.Count >= Cap(kind) && !table.ContainsKey(id)) return false;
        table[id] = row;
        return true;
    }

    internal bool Remove(MemoryKind kind, string id) => Table(kind).Remove(id);

    /// <summary>
    /// A party came into or left sight. In sight the memory is fresh; out of sight the clock runs from the stamp, not from
    /// now, so information ages from when it was counted (the hold may already be spent).
    /// </summary>
    internal void SetVisible(string partyId, bool visible, int scout)
    {
        if (!_parties.TryGetValue(partyId, out var row)) return;
        row.FadeHour = visible ? -1 : row.StampHour + HiddenHostsMath.HoldHours(scout);
    }

    /// <summary>
    /// The hourly tick. Settlement rows first poll the sight (<paramref name="settlementInSight"/>: null for a settlement
    /// that no longer exists, true when inspected, false otherwise). Then every row whose clock is due loses one digit and
    /// schedules the next loss one fade step ahead, so a long gap costs one digit per tick, never several. A row with
    /// nothing left and no Scouting latch for today is dropped.
    /// </summary>
    internal void FadeHourly(int nowHour, int scout, Func<string, bool?> settlementInSight)
    {
        if (nowHour < 0) return;

        _scratch.Clear();
        foreach (var pair in _settlements)
        {
            bool? inSight = settlementInSight(pair.Key);
            if (inSight == null) _scratch.Add(pair.Key);
            else if (inSight.Value) pair.Value.FadeHour = -1;
            else if (pair.Value.FadeHour < 0) pair.Value.FadeHour = pair.Value.StampHour + HiddenHostsMath.HoldHours(scout);
        }
        RemoveScratched(_settlements);

        int today = nowHour / 24;
        FadeTable(_parties, nowHour, today, scout);
        FadeTable(_settlements, nowHour, today, scout);
    }

    /// <summary>Drops every row with nothing left to remember and no latch for <paramref name="today"/>; nothing else is touched.</summary>
    internal void Prune(int today)
    {
        PruneTable(_parties, today);
        PruneTable(_settlements, today);
    }

    internal IEnumerable<KeyValuePair<string, MemoryRow>> Rows(MemoryKind kind) => Table(kind);

    internal void Clear()
    {
        _parties.Clear();
        _settlements.Clear();
        _scratch.Clear();
        SkippedInserts = 0;
    }

    private Dictionary<string, MemoryRow> Table(MemoryKind kind) => kind == MemoryKind.Party ? _parties : _settlements;

    private static int Cap(MemoryKind kind) => kind == MemoryKind.Party ? MaxParties : MaxSettlements;

    private static void Overwrite(MemoryRow row, int count, int known, int nowHour, bool visible, int scout)
    {
        row.Count = Math.Max(0, count);
        row.Known = known;
        row.StampHour = nowHour;
        row.FadeHour = visible ? -1 : nowHour + HiddenHostsMath.HoldHours(scout);
    }

    /// <summary>Removes the stale row (clock running) with the oldest stamp; false when every row is fresh. One scan, only at the cap.</summary>
    private static bool EvictStalest(Dictionary<string, MemoryRow> table)
    {
        string? oldest = null;
        int oldestStamp = int.MaxValue;
        foreach (var pair in table)
        {
            if (pair.Value.FadeHour < 0 || pair.Value.StampHour >= oldestStamp) continue;
            oldest = pair.Key;
            oldestStamp = pair.Value.StampHour;
        }
        return oldest != null && table.Remove(oldest);
    }

    private void FadeTable(Dictionary<string, MemoryRow> table, int nowHour, int today, int scout)
    {
        _scratch.Clear();
        foreach (var pair in table)
        {
            var row = pair.Value;
            if (row.FadeHour >= 0 && nowHour >= row.FadeHour && row.Known > 0)
            {
                row.Known--;
                row.FadeHour = nowHour + HiddenHostsMath.FadeStepHours(scout);
            }
            if (row.Known == 0 && row.XpDay != today) _scratch.Add(pair.Key);
        }
        RemoveScratched(table);
    }

    private void PruneTable(Dictionary<string, MemoryRow> table, int today)
    {
        _scratch.Clear();
        foreach (var pair in table)
            if (pair.Value.Known == 0 && pair.Value.XpDay != today) _scratch.Add(pair.Key);
        RemoveScratched(table);
    }

    private void RemoveScratched(Dictionary<string, MemoryRow> table)
    {
        for (int i = 0; i < _scratch.Count; i++) table.Remove(_scratch[i]);
        _scratch.Clear();
    }
}
