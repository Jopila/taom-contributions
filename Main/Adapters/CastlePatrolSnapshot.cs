namespace TAOM.Adapters;

/// <summary>
/// One settlement as the castle patrol decision sees it (#888), with no TaleWorlds type in it (ADR-007). The adapter fills
/// it; <c>default</c> reads as "not a castle", so a null settlement or a town answers no.
/// </summary>
/// <param name="IsCastle">The settlement is a castle.</param>
/// <param name="OwnerIsRebel">The owner clan is a rebel clan, which vanilla never gives a patrol.</param>
/// <param name="GuardHouseLevel">The level of the castle Guard House, 0 when it is not built.</param>
/// <param name="HasWeakTemplate">
/// The owner culture's weak (level 1) patrol template exists. Like the other two template flags, false when the castle has no
/// owner clan, because the template comes from the owner clan's culture.
/// </param>
/// <param name="HasModerateTemplate">The owner culture's moderate (level 2) patrol template exists.</param>
/// <param name="HasStrongTemplate">The owner culture's strong (level 3) patrol template exists.</param>
public readonly record struct CastlePatrolSnapshot(
    bool IsCastle,
    bool OwnerIsRebel,
    int GuardHouseLevel,
    bool HasWeakTemplate,
    bool HasModerateTemplate,
    bool HasStrongTemplate)
{
    /// <summary>The Guard House level value <see cref="ICastlePatrolAdapter.CastlePatrolGuardHouseLevel"/> gives a party that is not a castle patrol.</summary>
    public const int NotACastlePatrol = -1;

    /// <summary>Whether the owner culture has the patrol template for a strength of 1 (weak), 2 (moderate) or 3 (strong).</summary>
    public bool HasTemplateFor(int strength) =>
        strength == 1 ? HasWeakTemplate : strength == 2 ? HasModerateTemplate : strength == 3 && HasStrongTemplate;
}
