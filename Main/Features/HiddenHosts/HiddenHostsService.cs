using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Validation;

namespace TAOM.Features.HiddenHosts;

/// <summary>
/// Decides what another party's nameplate shows (#846): the troop count blurred by its distance from the player's main
/// party and the horizon. The policy lives here: the snapshot carries raw facts and this composes them into who is
/// exempt and who counts as the same realm. Plain data in, plain data out; the hooks read the snapshot through
/// <see cref="IPartyIntelAdapter"/>. Main thread only (nameplate binding and the encyclopedia hotkey). A process
/// singleton, so <see cref="ResetForNewSession"/> runs for every campaign start.
/// </summary>
internal sealed partial class HiddenHostsService
{
    /// <summary>Bookkeeping is dropped past this many parties (destroyed ones are never removed); the next frames rebuild it.</summary>
    internal const int MaxTrackedParties = 4096;

    private sealed class PartyState
    {
        internal int Known;
        internal int TextCount;
        internal int TextKnown;
        internal string? Text;
        internal int MemoryTextCount;
        internal int MemoryTextKnown;
        internal string? MemoryText;
    }

    private readonly IHiddenHostsSettings _settings;
    private readonly Dictionary<string, PartyState> _parties = new();
    private readonly Dictionary<string, PartyState> _settlements = new();
    private readonly HiddenHostsMemory _memory = new();
    private int _pendingScoutXp;

    public HiddenHostsService(IHiddenHostsSettings settings) => _settings = settings;

    internal bool Enabled => _settings.Enabled;

    /// <summary>
    /// Everything the service holds about one campaign: the latch, the text caches, the remembered counts, the Scouting XP
    /// not yet taken and the "a tilde was drawn" flag. Runs for every campaign start, and again from <see cref="LoadFrom"/>.
    /// </summary>
    internal void ResetForNewSession()
    {
        _parties.Clear();
        _settlements.Clear();
        _memory.Clear();
        _pendingScoutXp = 0;
        MemoryDrawn = false;
    }

    internal NameplateIntel Evaluate(in PartyIntelSnapshot s)
    {
        if (!TryDistanceRatio(in s, out float ratio)) return NameplateIntel.Unmasked;

        var state = StateFor(s.StringId!);
        if (HiddenHostsMath.IsHorizon(ratio, s.SpottingRatio))
        {
            // A party that left the horizon starts from nothing again.
            state.Known = 0;
            return new NameplateIntel(HiddenHostsMath.HorizonText, isHorizon: true);
        }

        int count = s.HealthyCount;
        bool sameRealm = s.SameFaction || s.ArmyLeaderSameFaction;
        float multiplier = HiddenHostsMath.ScoutMultiplier(s.ScoutingSkill, sameRealm);
        int known = HiddenHostsMath.KnownDigits(count, ratio, multiplier, state.Known);
        state.Known = known;

        // The reading is recorded (and may pay Scouting XP) even when it shows the whole number.
        var row = ReadPartyMemory(in s, count, known);
        if (row != null && row.Known > known)
        {
            MemoryDrawn = true;
            return new NameplateIntel(MemoryText(state, row), isHorizon: false);
        }
        if (known >= HiddenHostsMath.DigitCount(count)) return NameplateIntel.Unmasked;

        return new NameplateIntel(MaskText(state, count, known), isHorizon: false);
    }

    /// <summary>True when the party is past nine tenths of its own spotting reach. Reads the latch of no party.</summary>
    internal bool IsHorizon(in PartyIntelSnapshot s) =>
        TryDistanceRatio(in s, out float ratio) && HiddenHostsMath.IsHorizon(ratio, s.SpottingRatio);

    /// <summary>The gates every answer about a party shares; false means the plate or row is left to vanilla.</summary>
    private bool TryDistanceRatio(in PartyIntelSnapshot s, out float ratio)
    {
        ratio = 0f;
        if (!_settings.Enabled || IsPartyExempt(in s)) return false;
        return TryRatio(s.DistanceToMain, s.MainSeeingRange, out ratio);
    }

    /// <summary>
    /// The player's own side and what the engine hides: no data, an engine-hidden party, the main party, the player's clan or
    /// army, true sight. Never masked and never remembered. The one predicate behind both <see cref="IsPartyExempt"/> and the
    /// exact stamps.
    /// </summary>
    private static bool IsOwnSideOrHidden(in PartyIntelSnapshot s) =>
        !s.HasData || s.IsInfoHidden || s.StringId == null || s.IsMainParty || s.InPlayerClan || s.InPlayerArmy || s.TrueSight;

    /// <summary>Who is never masked: the player's own side and hidden parties, and a party in plain sight (already exact by distance).</summary>
    private static bool IsPartyExempt(in PartyIntelSnapshot s) => IsOwnSideOrHidden(in s) || s.InPlainSight;

    /// <summary>A settlement of the player's own side: owned by the player's clan, a clan hero inside, or true sight.</summary>
    private static bool IsSettlementOwnSide(in SettlementIntelSnapshot s) =>
        s.OwnedByPlayerClan || s.PlayerClanHeroInside || s.TrueSight;

    /// <summary>Distance over seeing range, as a positive requirement so NaN fails: false when either input or the quotient is unusable.</summary>
    private static bool TryRatio(float distance, float seeingRange, out float ratio)
    {
        ratio = 0f;
        if (!(FiniteFloatValidator.IsFinite(seeingRange) && seeingRange > 0f)) return false;
        if (!FiniteFloatValidator.IsFiniteAtLeast(distance, 0f)) return false;

        ratio = distance / seeingRange;
        return FiniteFloatValidator.IsFinite(ratio);
    }

    private PartyState StateFor(string id) => StateFor(_parties, id);

    private static PartyState StateFor(Dictionary<string, PartyState> table, string id)
    {
        if (table.TryGetValue(id, out var state)) return state;
        if (table.Count >= MaxTrackedParties) table.Clear();
        state = new PartyState();
        table[id] = state;
        return state;
    }

    /// <summary>The "~15?" text of a remembered count, reused while the row's count and known digits are unchanged.</summary>
    private static string MemoryText(PartyState state, MemoryRow row)
    {
        if (state.MemoryText == null || state.MemoryTextCount != row.Count || state.MemoryTextKnown != row.Known)
        {
            state.MemoryText = HiddenHostsMath.FormatMemory(row.Count, row.Known);
            state.MemoryTextCount = row.Count;
            state.MemoryTextKnown = row.Known;
        }
        return state.MemoryText!;
    }

    /// <summary>The formatted mask, reused while the count and the known digits are unchanged, so a steady plate allocates nothing.</summary>
    private static string MaskText(PartyState state, int count, int known)
    {
        if (state.Text == null || state.TextCount != count || state.TextKnown != known)
        {
            state.Text = HiddenHostsMath.FormatMask(count, known);
            state.TextCount = count;
            state.TextKnown = known;
        }
        return state.Text!;
    }
}
