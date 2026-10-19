namespace TAOM.Features.Relations;

/// <summary>The Personal Relations disband values after clamping. The leader range is ordered: min is never above max.</summary>
public sealed record PersonalRelationSettings(
    bool DisbandCostEnabled,
    int LeaderMin,
    int LeaderMax);
