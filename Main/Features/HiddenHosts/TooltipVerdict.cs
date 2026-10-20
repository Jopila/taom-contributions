namespace TAOM.Features.HiddenHosts;

/// <summary>How a tooltip's numbers are shown (#846 phase 2).</summary>
internal enum TooltipMode
{
    /// <summary>Vanilla's text: an exempt subject, a subject whose total is fully read, or no data.</summary>
    Vanilla,

    /// <summary>Digits past the reach are replaced by "?".</summary>
    Masked,

    /// <summary>The subject is at the edge of sight: numbers read "...", and a party or army has no name.</summary>
    Horizon,
}

/// <summary>
/// What the service decided for one tooltip subject at one moment: plain numbers, nothing from the engine. A verdict is
/// computed when a tooltip is built or refreshed and read by the number helpers for the length of that one call.
/// </summary>
internal readonly struct TooltipVerdict
{
    internal TooltipVerdict(TooltipMode mode, float ratio, float multiplier, int latchedKnown, int subjectCount,
        string? memoryText = null, int memoryHours = 0)
    {
        Mode = mode;
        Ratio = ratio;
        Multiplier = multiplier;
        LatchedKnown = latchedKnown;
        SubjectCount = subjectCount;
        MemoryText = memoryText;
        MemoryHours = memoryHours;
    }

    /// <summary>The verdict that leaves every number to vanilla.</summary>
    internal static TooltipVerdict Vanilla => default;

    internal TooltipMode Mode { get; }

    /// <summary>The subject's distance over the main party's seeing range.</summary>
    internal float Ratio { get; }

    /// <summary>The Scouting and same-realm widening of every digit threshold.</summary>
    internal float Multiplier { get; }

    /// <summary>The leading digits of the subject's own total that are held known (the hysteresis memory).</summary>
    internal int LatchedKnown { get; }

    /// <summary>The subject's own total, which the digit thresholds were chosen for.</summary>
    internal int SubjectCount { get; }

    /// <summary>
    /// The remembered count ("~15?") when the player's memory of the subject beats what the distance lets them read now,
    /// else null. Only a masked party, army or settlement carries one; the horizon never does.
    /// </summary>
    internal string? MemoryText { get; }

    /// <summary>Whole hours since the remembered count was taken (for the "Counted ... ago" row); 0 without a memory.</summary>
    internal int MemoryHours { get; }
}
