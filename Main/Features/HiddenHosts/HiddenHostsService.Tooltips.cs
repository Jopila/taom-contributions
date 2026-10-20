using System;
using TAOM.Adapters;
using TAOM.Core.Validation;

namespace TAOM.Features.HiddenHosts;

/// <summary>
/// The tooltip half of the service (#846 phase 2): the verdict a hovered party, army, settlement, battle or siege gets, and
/// the number texts that verdict produces for the engine's own count rows. A party's verdict shares the nameplate's latch, so
/// a digit the plate holds is held in the tooltip too; an army leader's party frame is judged on its own roster with no latch. Hidden Hosts never shows more than vanilla: a count vanilla itself
/// hid is left alone by the hooks, and any doubt gives the vanilla verdict.
/// </summary>
internal sealed partial class HiddenHostsService
{
    /// <summary>
    /// The verdict for a hovered party. A party that leads an army prints its own roster in vanilla's party tooltip, so it is
    /// judged on that count with no latch and no memory of its own: the latch and the remembered count belong to the army
    /// total that the nameplate and the army tooltip show. This verdict neither reads nor writes them, except that it resets
    /// <c>Known</c> at the horizon, as the nameplate does.
    /// </summary>
    internal TooltipVerdict EvaluateTooltipParty(in PartyIntelSnapshot s) => EvaluateTooltipSubject(in s, asArmy: false);

    /// <summary>The verdict for a hovered army, judged on its leader, whose healthy count includes the attached parties.</summary>
    internal TooltipVerdict EvaluateTooltipArmy(in PartyIntelSnapshot s) => EvaluateTooltipSubject(in s, asArmy: true);

    private TooltipVerdict EvaluateTooltipSubject(in PartyIntelSnapshot s, bool asArmy)
    {
        if (!TryDistanceRatio(in s, out float ratio)) return TooltipVerdict.Vanilla;

        bool ownRoster = !asArmy && s.LeadsArmy;
        var state = StateFor(s.StringId!);
        int count = ownRoster ? s.OwnHealthyCount : s.HealthyCount;
        float multiplier = HiddenHostsMath.ScoutMultiplier(s.ScoutingSkill, s.SameFaction || s.ArmyLeaderSameFaction);

        // Only a party drawn on the map has a horizon; a tracker or menu row for one out of sight keeps its name and its digits.
        if (s.IsVisible && HiddenHostsMath.IsHorizon(ratio, s.SpottingRatio))
        {
            state.Known = 0;
            return new TooltipVerdict(TooltipMode.Horizon, ratio, multiplier, 0, count);
        }

        int known = HiddenHostsMath.KnownDigits(count, ratio, multiplier, ownRoster ? 0 : state.Known);
        MemoryRow? row = null;
        if (!ownRoster)
        {
            state.Known = known;
            row = ReadPartyMemory(in s, count, known);
        }
        if (known >= HiddenHostsMath.DigitCount(count)) return TooltipVerdict.Vanilla;

        return MaskedVerdict(ratio, multiplier, known, count, row, s.NowHour);
    }

    /// <summary>The verdict for a hovered settlement; its subject count is the defenders vanilla prints. A settlement has no horizon.</summary>
    internal TooltipVerdict EvaluateTooltipSettlement(in SettlementIntelSnapshot s)
    {
        if (!_settings.Enabled || !s.HasData || s.StringId == null) return TooltipVerdict.Vanilla;
        if (s.IsPlayerSettlement || IsSettlementOwnSide(in s)) return TooltipVerdict.Vanilla;
        if (!TryRatio(s.DistanceToMain, s.MainSeeingRange, out float ratio)) return TooltipVerdict.Vanilla;

        var state = StateFor(_settlements, s.StringId);
        int count = s.DefenderCount;
        float multiplier = HiddenHostsMath.ScoutMultiplier(s.ScoutingSkill, s.SameFaction);
        int known = HiddenHostsMath.KnownDigits(count, ratio, multiplier, state.Known);
        state.Known = known;

        // A settlement under the cursor is in sight; its defenders are recorded like a party's count, with no XP.
        var row = RecordReading(MemoryKind.Settlement, s.StringId, count, known, s.NowHour, visible: true, s.ScoutingSkill);
        return known >= HiddenHostsMath.DigitCount(count)
            ? TooltipVerdict.Vanilla
            : MaskedVerdict(ratio, multiplier, known, count, row, s.NowHour);
    }

    /// <summary>The verdict for a hovered battle or siege; its subject count is every involved healthy troop. No latch, no horizon.</summary>
    internal TooltipVerdict EvaluateTooltipEvent(in EventIntelSnapshot s)
    {
        if (!_settings.Enabled || !s.HasData || s.PlayerInvolved || s.TrueSight) return TooltipVerdict.Vanilla;
        if (!TryRatio(s.DistanceToMain, s.MainSeeingRange, out float ratio)) return TooltipVerdict.Vanilla;

        int count = s.TotalHealthy;
        float multiplier = HiddenHostsMath.ScoutMultiplier(s.ScoutingSkill, s.SameFaction);
        int known = HiddenHostsMath.KnownDigits(count, ratio, multiplier, 0);
        return known >= HiddenHostsMath.DigitCount(count)
            ? TooltipVerdict.Vanilla
            : new TooltipVerdict(TooltipMode.Masked, ratio, multiplier, known, count);
    }

    /// <summary>A masked verdict, carrying the remembered text when the row holds more digits than this reading shows.</summary>
    private static TooltipVerdict MaskedVerdict(float ratio, float multiplier, int known, int count, MemoryRow? row, int nowHour)
    {
        if (row == null || row.Known <= known) return new TooltipVerdict(TooltipMode.Masked, ratio, multiplier, known, count);
        return new TooltipVerdict(TooltipMode.Masked, ratio, multiplier, known, count,
            HiddenHostsMath.FormatMemory(row.Count, row.Known), Math.Max(0, nowHour - row.StampHour));
    }

    /// <summary>
    /// The text for one healthy count the engine is about to print inside the frame <paramref name="v"/>: "..." at the
    /// horizon, the known leading digits then "?" while masked, null to keep vanilla's text (an exempt subject, or a number
    /// whose every digit is within reach). For the subject's own total this equals the nameplate.
    /// </summary>
    internal string? MaskTooltipCount(in TooltipVerdict v, int healthy)
    {
        if (v.Mode == TooltipMode.Horizon) return HiddenHostsMath.HorizonText;
        if (v.Mode != TooltipMode.Masked) return null;
        if (!(FiniteFloatValidator.IsFinite(v.Ratio) && FiniteFloatValidator.IsFinite(v.Multiplier) && v.Multiplier > 0f)) return null;

        int known = HiddenHostsMath.KnownDigits(healthy, v.Ratio, v.Multiplier, v.LatchedKnown);
        return known >= HiddenHostsMath.DigitCount(healthy) ? null : HiddenHostsMath.FormatMask(healthy, known);
    }

    /// <summary>
    /// The text for one party row of a settlement tooltip: the settlement's distance (the tooltip is about the settlement),
    /// the number vanilla printed on the row, the party's own Scouting and realm, and the party's own exemptions. Vanilla
    /// prints the army total only when <paramref name="includeAttachedParties"/> is true, otherwise the party's own roster,
    /// and the mask follows that number so its magnitude is never wrong. Null keeps vanilla's row.
    /// </summary>
    internal string? MaskSettlementPartyRow(in TooltipVerdict settlement, in PartyIntelSnapshot party, bool includeAttachedParties)
    {
        if (settlement.Mode == TooltipMode.Vanilla || !_settings.Enabled || IsPartyExempt(in party)) return null;

        float multiplier = HiddenHostsMath.ScoutMultiplier(party.ScoutingSkill, party.SameFaction || party.ArmyLeaderSameFaction);
        int printed = includeAttachedParties ? party.HealthyCount : party.OwnHealthyCount;
        var row = new TooltipVerdict(settlement.Mode, settlement.Ratio, multiplier, 0, printed);
        return MaskTooltipCount(in row, printed);
    }

    /// <summary>The value of a settlement tooltip's defenders row: the known leading digits of the defenders, or null to keep vanilla's.</summary>
    internal string? DefendersText(in TooltipVerdict v)
    {
        if (v.Mode == TooltipMode.Horizon) return HiddenHostsMath.HorizonText;
        if (v.Mode != TooltipMode.Masked) return null;
        if (v.MemoryText == null) return HiddenHostsMath.FormatMask(v.SubjectCount, v.LatchedKnown);

        MemoryDrawn = true;
        return v.MemoryText;
    }

    /// <summary>
    /// The text for the subject's own troop total: the remembered count when the verdict carries one, else what
    /// <see cref="MaskTooltipCount"/> gives. Only the total uses the memory; every other number stays a live reading.
    /// </summary>
    internal string? MaskTooltipTotal(in TooltipVerdict v, int healthy)
    {
        if (v.Mode == TooltipMode.Masked && v.MemoryText != null)
        {
            MemoryDrawn = true;
            return v.MemoryText;
        }
        return MaskTooltipCount(in v, healthy);
    }
}
