namespace TAOM.Features.Execution;

/// <summary>
/// Rules over <see cref="FactionSide"/> that need no service. Beside the enum because the same reading is shared by gifts, hires,
/// tournaments, battle thanks and Sworn Anew.
/// </summary>
public static class FactionSideRules
{
    /// <summary>
    /// True for Free against Evil, either way round. Neutral on either end is nobody's opponent, which is why this is not
    /// <c>IAlignmentService.AreEnemyAlignments</c>: that one calls Neutral the enemy of everyone.
    /// </summary>
    public static bool AreOpposed(FactionSide a, FactionSide b) => a != FactionSide.Neutral && b != FactionSide.Neutral && a != b;
}
