namespace TAOM.Features.HiddenHosts;

/// <summary>
/// Harmony's <c>__state</c> for the tooltip bracket (#846 phase 2): the frame that was current before this one opened.
/// Default means the prefix did not run (a stripped prefix) or opened nothing, and closing it does nothing. Public because
/// it appears in public prefix and finalizer signatures.
/// </summary>
public readonly struct ScopeToken
{
    internal ScopeToken(TooltipFrame? previous)
    {
        IsSet = true;
        Previous = previous;
    }

    /// <summary>True when a frame was opened with this token.</summary>
    public bool IsSet { get; }

    internal TooltipFrame? Previous { get; }
}
