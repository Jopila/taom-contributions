using System;
using System.Threading;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CoopInterop.Missions;

/// <summary>
/// The call-site face of <see cref="ICoopMissionAuthority"/> for mission code that is built with
/// <c>new</c> or runs in a tree task, where constructor injection is not available (#967, C7). The
/// authority is resolved lazily and once from the container; with no container (a unit test, the first
/// frame of startup) every answer is "owned" and "host", exactly as in singleplayer.
///
/// A check costs one session read in singleplayer and no reflection. In a session it is the authority's
/// compiled registry probe. Answers are never cached here.
/// </summary>
public static class CoopMissionOwnership
{
    private static ICoopMissionAuthority? _authority;
    private static Func<ICoopMissionAuthority?> _resolver = FromContainer;

    // Internal for TAOM.Tests: replaces the container lookup; null restores it. Drops the cached authority.
    internal static void UseResolver(Func<ICoopMissionAuthority?>? resolver)
    {
        Volatile.Write(ref _resolver, resolver ?? FromContainer);
        Volatile.Write(ref _authority, null);
    }

    // IoC.Resolve dereferences the container, which does not exist before IoC.Configure.
    private static ICoopMissionAuthority? FromContainer() => IoC.Resolve<ICoopMissionAuthority>();

    /// <summary>True when this peer owns <paramref name="agent"/>, or there is no co-op session.</summary>
    public static bool IsOwnedHere(Agent? agent)
    {
        var authority = Authority();
        return authority == null || authority.IsLocallyControlled(agent);
    }

    /// <summary>True when this peer is the battle host of <paramref name="mission"/>, or there is no session.</summary>
    public static bool IsBattleHost(Mission? mission)
    {
        var authority = Authority();
        return authority == null || authority.IsBattleHost(mission);
    }

    private static ICoopMissionAuthority? Authority()
    {
        var cached = Volatile.Read(ref _authority);
        if (cached != null) return cached;

        ICoopMissionAuthority? resolved;
        try
        {
            resolved = Volatile.Read(ref _resolver).Invoke();
        }
        catch (Exception)
        {
            // No container yet, or a disposed one: answer as singleplayer does, and ask again next time.
            return null;
        }

        if (resolved != null) Volatile.Write(ref _authority, resolved);
        return resolved;
    }
}
