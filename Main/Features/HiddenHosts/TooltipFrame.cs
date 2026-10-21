namespace TAOM.Features.HiddenHosts;

/// <summary>What a tooltip shows information about.</summary>
internal enum TooltipSubjectKind
{
    None,
    Party,
    Army,
    Settlement,
    MapEvent,
    SiegeEvent,
}

/// <summary>
/// One open tooltip build or refresh (#846 phase 2): the subject's kind and the verdict for it. Plain data. It lives for
/// the one engine call that opened it and is never kept afterwards. It is thread-local through
/// <see cref="HiddenHostsTooltipScope"/>, so it carries no owner. The decisions the patches make about it (rename, trim,
/// wrap, reason row) are members here so they are plain code the tests can call without any tooltip row.
/// </summary>
internal sealed class TooltipFrame
{
    internal TooltipFrame(TooltipSubjectKind kind, TooltipVerdict verdict)
    {
        Kind = kind;
        Verdict = verdict;
    }

    internal TooltipSubjectKind Kind { get; }

    internal TooltipVerdict Verdict { get; }

    /// <summary>
    /// True while the troop-total row of a party or army tooltip evaluates its value: the one number that shows the remembered
    /// count instead of the live reading. Set and cleared by the wrapper around that row, on the frame's own thread.
    /// </summary>
    internal bool InTotal { get; set; }

    /// <summary>
    /// A remembered "~" count really reached this tooltip: the wrapped total printed it, or the defenders row took it. The
    /// reason row and the drawn flag follow this, never the verdict alone (a tooltip without the row, such as the extended
    /// view of a hideout, shows no "~" and must not claim one).
    /// </summary>
    internal bool ShowedMemory { get; set; }

    /// <summary>A party or an army: the subjects with a title row and a troops section.</summary>
    internal bool IsPartyOrArmy => Kind == TooltipSubjectKind.Party || Kind == TooltipSubjectKind.Army;

    /// <summary>A party or army at the horizon loses its name, unless the scout named it (#847).</summary>
    internal bool RenamesTitle => IsPartyOrArmy && Verdict.Mode == TooltipMode.Horizon && !Verdict.Named;

    /// <summary>
    /// A masked party or army has its troop-total row wrapped, whether or not a memory exists yet, so a memory that appears
    /// while the tooltip stays open shows at the next refresh like the nameplate does.
    /// </summary>
    internal bool WrapsTotalRow => IsPartyOrArmy && Verdict.Mode == TooltipMode.Masked;

    /// <summary>Masked or at the horizon: the rows below a troops title are removed.</summary>
    internal bool TrimsTroopRows => Verdict.Mode != TooltipMode.Vanilla;

    /// <summary>A settlement's defenders row is looked for whenever the verdict is not vanilla.</summary>
    internal bool RewritesDefenders => Kind == TooltipSubjectKind.Settlement && Verdict.Mode != TooltipMode.Vanilla;

    /// <summary>The "Counted ... ago" row: only when a remembered count was actually printed in a masked party, army or settlement tooltip.</summary>
    internal bool AddsReasonRow =>
        ShowedMemory && Verdict.Mode == TooltipMode.Masked && Verdict.MemoryText != null
        && (IsPartyOrArmy || Kind == TooltipSubjectKind.Settlement);
}
