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
internal sealed class HiddenHostsService
{
    /// <summary>Bookkeeping is dropped past this many parties (destroyed ones are never removed); the next frames rebuild it.</summary>
    internal const int MaxTrackedParties = 4096;

    private sealed class PartyState
    {
        internal int Known;
        internal int TextCount;
        internal int TextKnown;
        internal string? Text;
    }

    private readonly IHiddenHostsSettings _settings;
    private readonly Dictionary<string, PartyState> _parties = new();

    public HiddenHostsService(IHiddenHostsSettings settings) => _settings = settings;

    internal bool Enabled => _settings.Enabled;

    internal void ResetForNewSession() => _parties.Clear();

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
        if (known >= HiddenHostsMath.DigitCount(count)) return NameplateIntel.Unmasked;

        return new NameplateIntel(MaskText(state, count, known), isHorizon: false);
    }

    /// <summary>True when the party is past nine tenths of its own spotting reach. Reads the latch of no party.</summary>
    internal bool IsHorizon(in PartyIntelSnapshot s) =>
        TryDistanceRatio(in s, out float ratio) && HiddenHostsMath.IsHorizon(ratio, s.SpottingRatio);

    /// <summary>The gates every answer shares; false means the plate is left to vanilla.</summary>
    private bool TryDistanceRatio(in PartyIntelSnapshot s, out float ratio)
    {
        ratio = 0f;
        if (!_settings.Enabled) return false;
        if (!s.HasData || s.IsInfoHidden || s.StringId == null) return false;
        if (s.IsMainParty || s.InPlayerClan || s.InPlayerArmy || s.TrueSight) return false;
        if (!(FiniteFloatValidator.IsFinite(s.MainSeeingRange) && s.MainSeeingRange > 0f)) return false;
        if (!FiniteFloatValidator.IsFiniteAtLeast(s.DistanceToMain, 0f)) return false;

        ratio = s.DistanceToMain / s.MainSeeingRange;
        return FiniteFloatValidator.IsFinite(ratio);
    }

    private PartyState StateFor(string id)
    {
        if (_parties.TryGetValue(id, out var state)) return state;
        if (_parties.Count >= MaxTrackedParties) _parties.Clear();
        state = new PartyState();
        _parties[id] = state;
        return state;
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
