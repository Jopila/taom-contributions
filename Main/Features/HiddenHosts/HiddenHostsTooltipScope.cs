using System;

namespace TAOM.Features.HiddenHosts;

/// <summary>
/// The open tooltip frame (#846 phase 2). The number helpers (<c>GetPartySizeText</c>, the party row text) take no
/// parameter that names the subject, so the bracket around the call that builds or refreshes the tooltip publishes it
/// here and restores the previous frame when it closes. The slot is per thread (<c>[ThreadStatic]</c>, the pattern of
/// TownGoldFlowScope), so a frame is seen only by the thread that opened it and two threads opening and closing out of
/// order cannot bring back a frame the other already closed.
/// </summary>
internal static class HiddenHostsTooltipScope
{
    [ThreadStatic]
    private static TooltipFrame? _current;

    /// <summary>The frame the calling thread opened and has not closed, or null (also for a frame another thread opened).</summary>
    internal static TooltipFrame? Current => _current;

    /// <summary>Makes <paramref name="frame"/> current; the token remembers the frame it replaced.</summary>
    internal static ScopeToken Open(TooltipFrame frame)
    {
        var token = new ScopeToken(_current);
        _current = frame;
        return token;
    }

    /// <summary>
    /// Restores the frame that was current when <paramref name="token"/> was opened, not null: closing the same token twice
    /// (Harmony reruns every finalizer when a later one throws) writes the same value again. An unset token (a stripped
    /// prefix) does nothing.
    /// </summary>
    internal static void Close(in ScopeToken token)
    {
        if (token.IsSet) _current = token.Previous;
    }
}
