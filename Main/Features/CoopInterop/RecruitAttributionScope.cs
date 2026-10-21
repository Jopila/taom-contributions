namespace TAOM.Features.CoopInterop;

/// <summary>
/// The id of the party whose recruitment the co-op server is applying right now, or null. Set by
/// Patch113_CoopRecruitAttribution around Coop's server-side <c>HandleOnRecruitmentDone</c>, so the
/// party-less <c>CampaignEvents.OnUnitRecruitedEvent</c> raised inside it can be attributed to the
/// recruiting player's party. Thread-static: the scope only has to span one synchronous call, and a
/// handler on another thread must never see it.
/// </summary>
internal static class RecruitAttributionScope
{
    [System.ThreadStatic]
    private static string? _current;

    internal static string? Current
    {
        get => _current;
        set => _current = value;
    }
}
