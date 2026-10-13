using System;
using System.Collections.Generic;
using TAOM.Core.Validation;

namespace TAOM.Features.ArmyTargeting;

/// <summary>
/// The War Chronicle's army focus windows (#765, M4; docs/features/army-targeting.md "Army focus"). The
/// chronicle writes the whole set with <see cref="Replace"/> whenever a siege window opens or resolves;
/// <see cref="ArmyTargetingService.ApplyTargetScoreModifiers"/> reads it for every scored candidate.
///
/// <para><b>Hot path.</b> The set is an immutable array swapped in whole (copy-on-write), so a reader never
/// sees a half-written set and needs no lock. The read is a length check (the empty fast path, which is
/// the normal state outside a window) and a scan of a few entries: no allocation, no dictionary.</para>
///
/// <para><b>Validated on write.</b> An entry is kept only with a settlement id, at least one attacker or a
/// defender, and a finite multiplier above 1, clamped to <see cref="MaxMultiplier"/>. So a stored focus
/// can only raise a positive score; a zero (vanilla's distance veto) stays zero.</para>
///
/// <para><b>Not saved.</b> It is derived state: the chronicle pushes its Active windows again at session
/// launch, and clears the set when a window resolves and when the war ends. A process-lifetime singleton,
/// so the chronicle also clears it when each campaign's behavior is built.</para>
/// </summary>
public sealed class ArmyTargetFocusRegistry
{
    /// <summary>Highest multiplier a focus may carry; the MCM slider's maximum.</summary>
    public const float MaxMultiplier = 2.5f;

    private static readonly Entry[] Empty = new Entry[0];

    private volatile Entry[] _entries = Empty;

    public bool IsEmpty => _entries.Length == 0;

    public int Count => _entries.Length;

    /// <summary>Replaces every focus with the valid entries of <paramref name="focuses"/>; null clears.</summary>
    public void Replace(IEnumerable<ArmyTargetFocus?>? focuses)
    {
        if (focuses == null)
        {
            _entries = Empty;
            return;
        }

        var kept = new List<Entry>();
        foreach (var focus in focuses)
        {
            var entry = Entry.TryCreate(focus);
            if (entry != null)
                kept.Add(entry);
        }

        _entries = kept.Count == 0 ? Empty : kept.ToArray();
    }

    public void Clear() => _entries = Empty;

    /// <summary>
    /// The largest focus multiplier that applies to this army and candidate, or 1. Besieger armies of an
    /// attacker and Defender armies of the defender are focused; every other mission returns 1.
    /// </summary>
    public float GetMultiplier(ArmyTargetingMission mission, string? settlementId, string? factionId)
    {
        var entries = _entries;
        if (entries.Length == 0 || settlementId == null || factionId == null)
            return 1f;
        if (mission != ArmyTargetingMission.Besieger && mission != ArmyTargetingMission.Defender)
            return 1f;

        var result = 1f;
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (!string.Equals(entry.SettlementId, settlementId, StringComparison.Ordinal))
                continue;

            var applies = mission == ArmyTargetingMission.Besieger
                ? entry.HasAttacker(factionId)
                : string.Equals(entry.DefenderFactionId, factionId, StringComparison.Ordinal);
            if (applies && entry.Multiplier > result)
                result = entry.Multiplier;
        }

        return result;
    }

    /// <summary>A copy of the stored focuses, for the console.</summary>
    public IReadOnlyList<ArmyTargetFocus> Snapshot()
    {
        var entries = _entries;
        var result = new List<ArmyTargetFocus>(entries.Length);
        foreach (var entry in entries)
            result.Add(new ArmyTargetFocus(entry.SettlementId, entry.AttackerFactionIds, entry.DefenderFactionId, entry.Multiplier));
        return result;
    }

    private sealed class Entry
    {
        private Entry(string settlementId, string[] attackers, string? defender, float multiplier)
        {
            SettlementId = settlementId;
            AttackerFactionIds = attackers;
            DefenderFactionId = defender;
            Multiplier = multiplier;
        }

        public string SettlementId { get; }

        public string[] AttackerFactionIds { get; }

        public string? DefenderFactionId { get; }

        public float Multiplier { get; }

        public bool HasAttacker(string factionId)
        {
            var attackers = AttackerFactionIds;
            for (var i = 0; i < attackers.Length; i++)
            {
                if (string.Equals(attackers[i], factionId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static Entry? TryCreate(ArmyTargetFocus? focus)
        {
            if (focus == null || string.IsNullOrWhiteSpace(focus.SettlementId))
                return null;

            // Positive requirement: NaN fails `> 1f`, so a poisoned multiplier is never stored.
            if (!FiniteFloatValidator.IsFinite(focus.Multiplier) || !(focus.Multiplier > 1f))
                return null;

            var attackers = new List<string>();
            foreach (var id in focus.AttackerFactionIds)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    attackers.Add(id);
            }

            var defender = string.IsNullOrWhiteSpace(focus.DefenderFactionId) ? null : focus.DefenderFactionId;
            if (attackers.Count == 0 && defender == null)
                return null;

            return new Entry(focus.SettlementId!, attackers.ToArray(), defender, Math.Min(focus.Multiplier, MaxMultiplier));
        }
    }
}
