using TAOM.Features.Execution;

namespace TAOM.Features.Relations;

/// <summary>
/// The multiplier for each way two sides can stand to each other, one set per deed (Good Deeds, Long Memory). Neutral when either
/// side is Neutral (a side with no alignment is nobody's ally or enemy), Same when they match, Opposite otherwise.
/// </summary>
public sealed record SideMultipliers(float Same, float Neutral, float Opposite)
{
    public float For(FactionSide first, FactionSide second) =>
        first == FactionSide.Neutral || second == FactionSide.Neutral ? Neutral : first == second ? Same : Opposite;
}
