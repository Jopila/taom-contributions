namespace TAOM.Features.SpecialResources;

/// <summary>
/// Which hero pays the one-time recruit_cost when a recruit event arrives. Pure so the co-op cases are
/// a table test. Under co-op only the server raises <c>OnUnitRecruited</c> for another player's
/// recruitment, and it carries no party, so Patch113 supplies the scoped party and the caller resolves
/// that party's leader.
/// </summary>
public static class RecruitChargePolicy
{
    /// <param name="sessionActive">A co-op session is live: an unscoped recruit then charges nobody, because no human plays on the authority process (<see href="https://github.com/haterade22/TAOM/issues/967#issuecomment-6328464305">#967 design comment</see>).</param>
    /// <param name="hasScope">Patch113 named a recruiting party for this event.</param>
    /// <param name="scopedLeader">That party's leader, or null when the party or its leader is gone.</param>
    /// <returns>The payer, or null to charge nobody.</returns>
    public static T? ChoosePayer<T>(
        bool isAuthority, bool isDedicated, bool sessionActive, bool hasScope, T? scopedLeader, T? mainHero) where T : class
    {
        // A client's own balance is not the server's: the authority charges, once.
        if (!isAuthority) return null;
        // A scoped recruit belongs to the scoped party's leader. A leader who is gone means nobody pays,
        // never the host.
        if (hasScope) return scopedLeader;
        // A dedicated server's MainHero is a placeholder nobody controls.
        if (isDedicated) return null;
        // Under a live session an unscoped recruit is nobody's: the authority's own MainHero earns and pays nothing.
        if (sessionActive) return null;
        return mainHero;
    }
}
