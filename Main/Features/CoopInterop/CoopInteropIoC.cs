using DryIoc;

namespace TAOM.Features.CoopInterop;

public static class CoopInteropIoC
{
    public static void RegisterCoopInteropFeature(IContainer container)
    {
        container.Register<ISaveDefinerCollisionDetector, SaveDefinerCollisionDetector>(Reuse.Singleton);
        container.Register<ICoopPresenceProvider, CoopPresenceProvider>(Reuse.Singleton);
        // Process-constant (it reads this assembly's own load path), so a singleton caches the one
        // answer for the session.
        container.Register<IDedicatedServerProvider, DedicatedServerProvider>(Reuse.Singleton);
        // Session/role probe. Singleton because the reflection BINDING is cached; the probe itself
        // still re-reads Coop's state on every call, which it must — see ICoopSessionProvider.
        container.Register<ICoopSessionProvider, CoopSessionProvider>(Reuse.Singleton);
        // Who is a human player. Singleton because only the reflection binding is cached; answers are live.
        container.Register<IPlayerControlQuery, CoopPlayerControlQuery>(Reuse.Singleton);
        // Who owns an agent, and who is the battle host, for TAOM's mission behaviors (#967, C7). Singleton: the
        // reflection binding and the per-mission registry link are cached; every answer is live.
        container.Register<Missions.ICoopMissionAuthority, Missions.CoopMissionAuthority>(Reuse.Singleton);
    }
}
