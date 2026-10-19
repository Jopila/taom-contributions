using TAOM.Core.Validation;
using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Features.CultureDoctrine.Doctrines;

/// <summary>
/// When a defending army stands against horse: it has foot to stand with, the enemy's melee
/// cavalry is at least <see cref="HorseStandTunables.EnemyCavalryShare"/> of its army, and the
/// enemy has more men than our whole side (allies included) times <see cref="HorseStandTunables.OutnumberedRatio"/>. Pure:
/// a snapshot and the tunables in, a verdict out. The cavalry ratio is the engine's own
/// (`TeamQuerySystem.EnemyCavalryRatio`, melee cavalry only, cached for 15 s), so a verdict
/// can lag a charge by that long; every comparison is a positive requirement, so a NaN or
/// infinite ratio fails the gate.
/// </summary>
public static class HorseStand
{
    /// <summary>The factor on a standing wall's weight while the gate passes. Measured at the A/B's
    /// stand army (200 men against 300, 60% melee cavalry; `ShippedDoctrineOrderingTests`): the
    /// wall is 1.08, Dale's disciplined line 1.35 and vanilla's choke hold 0.88 to 1.05, and the
    /// wall must beat the best of them by the 1.5x that `TeamAIComponent.MakeDecision` grants the
    /// current tactic (`TeamAIComponent.cs:301`), so at least 1.5 * 1.35 / 1.08 = 1.875. A plain
    /// 2 leaves a 6% margin on the disciplined line and 20% or more on the rest.</summary>
    public const float StandEdge = 2f;

    /// <summary>The factor a stand-capable wall multiplies its weight by: <see cref="StandEdge"/>
    /// while <see cref="Applies"/>, else 1, so the wall yields the moment the gate fails.</summary>
    public static float WeightFactor(in TeamQuerySnapshot s, in HorseStandTunables tunables) =>
        Applies(in s, in tunables) ? StandEdge : 1f;

    public static bool Applies(in TeamQuerySnapshot s, in HorseStandTunables tunables)
    {
        if (!s.IsDefender || !s.HasInfantry)
            return false;
        if (!FiniteFloatValidator.IsFinite(s.EnemyCavalryRatio) || !(s.EnemyCavalryRatio >= tunables.EnemyCavalryShare))
            return false;
        return s.EnemyUnitCount > s.AllyUnitCount * tunables.OutnumberedRatio;
    }

    /// <summary>The stand an AI team is armed with: the culture's policy when the MCM switch is on
    /// and the team defends and is not the player's, else none. The player's own team never stands.</summary>
    public static HorseStandPolicy? PolicyFor(bool enabled, HorseStandPolicy? culture, bool isDefender, bool isPlayerTeam) =>
        enabled && isDefender && !isPlayerTeam ? culture : null;
}
