using System;

namespace TAOM.Features.HiddenHosts;

/// <summary>
/// The memory half of the service (#846 phase 3, docs/features/hidden-hosts.md "Memory"): recording what the player has
/// counted, the exact stamps of hosts in plain sight, the fade, the Scouting XP waiting to be taken, and the save text.
/// Plain ids, ints and whole campaign hours; the behavior brings the engine's events and clock in through
/// <see cref="TAOM.Adapters.IHiddenHostsCampaignAdapter"/>. Main thread only.
/// </summary>
internal sealed partial class HiddenHostsService
{
    /// <summary>A "~" count has been drawn since the session began: the one-time memory hint waits for it.</summary>
    internal bool MemoryDrawn { get; private set; }

    /// <summary>Inserts the memory refused because every row was fresh; logged once per session by the behavior.</summary>
    internal int MemorySkippedInserts => _memory.SkippedInserts;

    /// <summary>The Scouting XP earned since the last call, which it zeroes. The hooks only add; the campaign tick grants.</summary>
    internal int TakePendingScoutXp()
    {
        int xp = _pendingScoutXp;
        _pendingScoutXp = 0;
        return xp;
    }

    /// <summary>
    /// An exact count of a host in plain sight (an encounter, a conversation, a settlement the player is in, the player's own
    /// siege on either side): every digit is remembered and no XP is paid. The snapshot is judged by the same predicate as the
    /// mask (<see cref="IsOwnSideOrHidden"/>): the player's own side and hidden parties are never remembered, and only a
    /// party whose snapshot says it is in plain sight is stamped. A party inside a settlement is not visible, so its fade
    /// clock restarts at every stamp.
    /// </summary>
    internal void Stamp(in TAOM.Adapters.PartyIntelSnapshot s)
    {
        if (!_settings.Enabled || s.NowHour < 0 || IsOwnSideOrHidden(in s) || !s.InPlainSight) return;
        _memory.Record(MemoryKind.Party, s.StringId!, s.HealthyCount, HiddenHostsMath.DigitCount(s.HealthyCount), s.NowHour,
            s.IsVisible, s.ScoutingSkill);
    }

    /// <summary>
    /// The same for the defenders of the settlement the player is inside (a snapshot with <c>IsPlayerSettlement</c>), unless
    /// it is the player's own side: a fief the player's clan owns, one a clan hero is in, or true sight.
    /// </summary>
    internal void StampSettlement(in TAOM.Adapters.SettlementIntelSnapshot s)
    {
        if (!s.IsPlayerSettlement) return;
        WriteSettlement(in s, visible: true);
    }

    /// <summary>
    /// The defenders a scout counted in a settlement the player is not in (#847): the same exact stamp, with the same gates
    /// (the toggle, a usable clock, the player's own side), but for any settlement and with the clock running from the stamp.
    /// A settlement the player is not inside is not in sight, so the row starts at stamp plus the hold; the hourly poll puts it
    /// back on the sight clock if it is inspected.
    /// </summary>
    internal void RecordScoutedSettlement(in TAOM.Adapters.SettlementIntelSnapshot s) => WriteSettlement(in s, visible: false);

    private void WriteSettlement(in TAOM.Adapters.SettlementIntelSnapshot s, bool visible)
    {
        if (!_settings.Enabled || !s.HasData || s.StringId == null || s.NowHour < 0 || IsSettlementOwnSide(in s)) return;
        _memory.Record(MemoryKind.Settlement, s.StringId, s.DefenderCount, HiddenHostsMath.DigitCount(s.DefenderCount), s.NowHour,
            visible, s.ScoutingSkill);
    }

    /// <summary>
    /// What a scout's report says about one host's count (#847): the digits the distance lets the player read now, plus
    /// <paramref name="stages"/> more (one, or two for a skilled scout), never past the host's digit count, and never fewer than
    /// the player already remembers of a host of that size (a remembered count that disagrees with the live one is another
    /// size of host and counts for nothing). The raised figure is remembered like any counted one, but pays no Scouting XP (the
    /// ride pays its own). The latch is read, not written: the plate keeps showing the live reading and the memory shows the
    /// rest. With the fog off, no clock, or a host of the player's own side or one the engine hides, the report gives the whole
    /// number and writes nothing. Returns the text to print: the number with its unknown digits as "?".
    /// </summary>
    internal string RaiseByScout(in TAOM.Adapters.PartyIntelSnapshot s, int stages)
    {
        int count = Math.Max(0, s.HealthyCount);
        int digits = HiddenHostsMath.DigitCount(count);
        if (!_settings.Enabled || s.NowHour < 0 || IsOwnSideOrHidden(in s)) return HiddenHostsMath.FormatMask(count, digits);

        int basis = LiveKnownDigits(in s, count);
        if (_memory.TryGet(MemoryKind.Party, s.StringId!, out var row) && HiddenHostsMath.Agrees(count, row.Known, row.Count))
            basis = Math.Max(basis, row.Known);
        int raised = Math.Min(digits, basis + Math.Max(0, stages));
        _memory.Record(MemoryKind.Party, s.StringId!, count, raised, s.NowHour, s.IsVisible, s.ScoutingSkill);
        return HiddenHostsMath.FormatMask(count, raised);
    }

    /// <summary>The digits <see cref="Evaluate"/> would show now: none at the horizon or without a usable distance, else the held latch widened by the reach.</summary>
    private int LiveKnownDigits(in TAOM.Adapters.PartyIntelSnapshot s, int count)
    {
        if (!TryRatio(s.DistanceToMain, s.MainSeeingRange, out float ratio) || HiddenHostsMath.IsHorizon(ratio, s.SpottingRatio)) return 0;
        int latch = _parties.TryGetValue(s.StringId!, out var state) ? state.Known : 0;
        float multiplier = HiddenHostsMath.ScoutMultiplier(FogSkill(s.ScoutingSkill), s.SameFaction || s.ArmyLeaderSameFaction);
        return HiddenHostsMath.KnownDigits(count, ratio, multiplier, latch);
    }

    /// <summary>
    /// A party came into or left sight. Not gated by the toggle: it only ages state that already exists, and a
    /// toggle-gated transition would leave a row fresh forever (harmony-patches.md "Latches and Toggle Gates").
    /// </summary>
    internal void OnVisibilityChanged(string id, bool visible, int scout) => _memory.SetVisible(id, visible, scout);

    /// <summary>The hourly tick: fades the memory by the clock. <paramref name="settlementInSight"/> answers true, false or null (gone) for a settlement id.</summary>
    internal void FadeHourly(int nowHour, int scout, Func<string, bool?> settlementInSight) =>
        _memory.FadeHourly(nowHour, scout, settlementInSight);

    /// <summary>A destroyed party loses its row.</summary>
    internal void Forget(string partyId) => _memory.Remove(MemoryKind.Party, partyId);

    /// <summary>The save text. Rows with nothing left to remember and no Scouting latch for today are dropped first.</summary>
    internal string EncodeMemory(int nowHour)
    {
        if (nowHour >= 0) _memory.Prune(nowHour / 24);
        return HiddenHostsMemoryCodec.Encode(_memory);
    }

    /// <summary>
    /// Replaces everything with the saved memory. It clears first, so a load whose ids repeat the last session's starts
    /// without its latch, text caches, pending XP or drawn flag, even when no reset ran before it.
    /// </summary>
    internal MemoryDecodeReport LoadFrom(string? payload)
    {
        ResetForNewSession();
        return HiddenHostsMemoryCodec.Decode(payload, _memory);
    }

    /// <summary>
    /// Applies one live reading of a party to the memory and pays the Scouting XP it earned. A reading with no digit
    /// makes no new row, but it still replaces a row it contradicts and finds the row the display may fall back to. Returns
    /// null without a usable clock.
    /// </summary>
    private MemoryRow? ReadPartyMemory(in TAOM.Adapters.PartyIntelSnapshot s, int count, int known)
    {
        var row = RecordReading(MemoryKind.Party, s.StringId!, count, known, s.NowHour, s.IsVisible, s.ScoutingSkill);
        if (row != null && known >= 1 && s.AtWar) PayScoutXp(row, known, s.NowHour);
        return row;
    }

    private MemoryRow? RecordReading(MemoryKind kind, string id, int count, int known, int nowHour, bool visible, int scout)
    {
        return nowHour < 0 ? null : _memory.Record(kind, id, count, known, nowHour, visible, scout);
    }

    /// <summary>
    /// Stages 1 to 3 are the first, second and third digit read (more digits stay at stage 3). Each stage pays once per
    /// party per campaign day, the first time a reading reaches it; a new day starts the party over.
    /// </summary>
    private void PayScoutXp(MemoryRow row, int known, int nowHour)
    {
        int stage = Math.Min(known, 3);
        int today = nowHour / 24;
        if (row.XpDay != today)
        {
            row.XpDay = today;
            row.XpStage = 0;
        }
        for (int k = row.XpStage + 1; k <= stage; k++)
            _pendingScoutXp += HiddenHostsMath.XpForStage(k);
        if (stage > row.XpStage) row.XpStage = stage;
    }
}
