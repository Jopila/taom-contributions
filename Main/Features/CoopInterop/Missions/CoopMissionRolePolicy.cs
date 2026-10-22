using System;

namespace TAOM.Features.CoopInterop.Missions;

/// <summary>
/// The whole-behavior gate for TAOM mission behaviors in a co-op session. Pure: the caller reads the
/// session flag once per mission. Host status is unknown at add time (the battle host is elected after
/// Mission.AfterStart, where TAOM adds its behaviors), so this gate drops only the behaviors that can
/// never be right in a session; every other role is checked live inside the behavior.
/// </summary>
public static class CoopMissionRolePolicy
{
    public static bool ShouldAdd(CoopMissionRole role, bool sessionActive) =>
        !(role == CoopMissionRole.Unsafe && sessionActive);

    /// <summary>The same gate by behavior type, through <see cref="CoopMissionRoles"/>.</summary>
    public static bool ShouldAdd(Type behaviorType, bool sessionActive) =>
        ShouldAdd(CoopMissionRoles.Of(behaviorType), sessionActive);
}
