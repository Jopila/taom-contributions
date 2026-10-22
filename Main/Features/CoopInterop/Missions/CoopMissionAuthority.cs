using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;

namespace TAOM.Features.CoopInterop.Missions;

/// <summary>
/// Binds <see cref="ICoopMissionAuthority"/> to BannerlordCoop BY REFLECTION (no compile-time reference,
/// the same rule as <see cref="CoopSessionProvider"/>). Members bound, all verified against the
/// bannerlord-1.5.x source of BannerlordCoop (2026-10-21):
///   GameInterface.ContainerProvider.TryResolve&lt;T&gt;(out T) -> bool            (public static, generic)
///   Missions.INetworkAgentRegistry.TryGetAgentInfo(Agent, out CoopAgentInfo) -> bool
///   Missions.INetworkAgentRegistry.IsLocallyControlled(Agent) -> bool
///   Missions.CoopMissionController                                              (abstract MissionBehavior)
///   Missions.Battles.CoopBattleController.Session -> IBattleSession             (public instance property)
///   Missions.Battles.IBattleSession.IsLocalHost -> bool                         (re-read from the host registry per call)
///
/// The registry answers per agent: unregistered, registered to another peer, or registered here. An
/// unregistered agent follows co-op's own fallback in BattleBlowInterceptPatch: a human is owned when its
/// engine controller is not None; a mount follows its rider (a masterless one is owned).
///
/// Hot-path rules: every call is compiled delegates, no reflection Invoke. The registry is resolved once
/// per mission and the per-mission bindings are replaced when another mission asks; an ANSWER is never
/// cached, since authority moves on a host handoff. Callable off the main thread.
/// </summary>
public sealed class CoopMissionAuthority : ICoopMissionAuthority
{
    private const string ContainerProviderTypeName = "GameInterface.ContainerProvider";
    private const string RegistryTypeName = "Missions.INetworkAgentRegistry";
    private const string MissionControllerTypeName = "Missions.CoopMissionController";
    private const string BattleControllerTypeName = "Missions.Battles.CoopBattleController";

    private const int Unregistered = 0;
    private const int RegisteredElsewhere = 1;
    private const int RegisteredHere = 2;

    private readonly ICoopSessionProvider _session;
    private readonly IModLogger _logger;
    private readonly Func<string, Type?> _findType;
    private readonly Lazy<Binding> _binding;
    private readonly object _linkLock = new();
    private volatile MissionLink? _link;
    private int _warned;

    public CoopMissionAuthority(ICoopSessionProvider session, IModLogger logger)
        : this(session, logger, ReflectionUtils.FindTypeAcrossLoadedAssemblies)
    {
    }

    // Internal for TAOM.Tests (InternalsVisibleTo): the type lookup is the only seam.
    internal CoopMissionAuthority(ICoopSessionProvider session, IModLogger logger, Func<string, Type?> findType)
    {
        _session = session;
        _logger = logger;
        _findType = findType;
        // Bind once, success or failure, like CoopPlayerControlQuery: the types cannot appear later.
        _binding = new Lazy<Binding>(Bind);
    }

    public bool IsLocallyControlled(Agent? agent)
    {
        // No session: owned, with no reflection and no engine read.
        if (!_session.IsSessionActive) return true;
        if (agent == null) return false;

        try
        {
            var mission = agent.Mission ?? Mission.Current;
            if (mission == null) return new EngineAgentFacts(agent).HasLiveController;

            var link = LinkFor(mission, () => mission.MissionBehaviors);
            return Decide(link, new EngineAgentFacts(agent));
        }
        catch (Exception ex)
        {
            // Never let another mod's internals throw into a mission tick. Fail open: TAOM acts as it
            // does in singleplayer.
            LogOnce($"ownership probe threw, treating the agent as owned: {ex.Message}");
            return true;
        }
    }

    public bool IsBattleHost(Mission? mission)
    {
        if (!_session.IsSessionActive) return true;
        if (mission == null) return true;

        try
        {
            var link = LinkFor(mission, () => mission.MissionBehaviors);
            return DecideHost(link);
        }
        catch (Exception ex)
        {
            LogOnce($"host probe threw, treating this peer as host: {ex.Message}");
            return true;
        }
    }

    // Internal for TAOM.Tests: the whole ownership decision below the engine reads.
    internal bool Decide<T>(MissionLink link, T agent) where T : IAgentFacts
    {
        // A mission co-op does not manage (a plain tournament, a custom battle) has no puppets.
        if (!link.Managed) return true;

        var binding = _binding.Value;
        var probe = binding.Probe;
        var registry = link.Registry;
        // Registry unreadable (bind failure, no container): the engine controller is the best signal left.
        if (probe == null || registry == null) return agent.HasLiveController;

        var answer = Probe(probe, registry, agent.Key);
        if (answer == RegisteredHere) return true;
        if (answer == RegisteredElsewhere) return false;
        if (answer < 0) return agent.HasLiveController;

        // Unregistered: co-op's own fallback.
        if (agent.IsHuman) return agent.HasLiveController;
        if (!agent.TryGetRider(out var riderKey, out var riderHasController)) return true;

        var riderAnswer = Probe(probe, registry, riderKey);
        if (riderAnswer == RegisteredHere) return true;
        if (riderAnswer == RegisteredElsewhere) return false;
        return riderHasController;
    }

    // Internal for TAOM.Tests.
    internal bool DecideHost(MissionLink link)
    {
        // Not a co-op battle (a tournament, a tavern, a mission co-op does not manage): nothing to elect.
        var controller = link.BattleController;
        if (controller == null) return true;

        var isHost = _binding.Value.SessionIsLocalHost;
        if (isHost == null) return true;

        try { return isHost(controller); }
        catch (Exception ex)
        {
            LogOnce($"host probe threw, treating this peer as host: {ex.Message}");
            return true;
        }
    }

    // Internal for TAOM.Tests: the mission is an opaque key, its behaviors are read once per mission.
    internal MissionLink LinkFor(object mission, Func<IEnumerable<object>> behaviors)
    {
        var current = _link;
        if (current != null && ReferenceEquals(current.Mission, mission)) return current;

        lock (_linkLock)
        {
            current = _link;
            if (current != null && ReferenceEquals(current.Mission, mission)) return current;

            var built = BuildLink(mission, behaviors());
            _link = built;
            return built;
        }
    }

    private MissionLink BuildLink(object mission, IEnumerable<object> behaviors)
    {
        var binding = _binding.Value;
        var managed = binding.MissionControllerType == null;
        object? battleController = null;

        foreach (var behavior in behaviors)
        {
            if (behavior == null) continue;
            if (binding.MissionControllerType?.IsInstanceOfType(behavior) == true) managed = true;
            if (binding.BattleControllerType?.IsInstanceOfType(behavior) == true) battleController = behavior;
        }

        object? registry = null;
        if (managed && binding.ResolveRegistry != null)
        {
            try { registry = binding.ResolveRegistry(); }
            catch (Exception ex) { LogOnce($"registry resolve threw, using engine controllers: {ex.Message}"); }
        }

        return new MissionLink(mission, managed, registry, battleController);
    }

    private int Probe(Func<object, object, int> probe, object registry, object agent)
    {
        try { return probe(registry, agent); }
        catch (Exception ex)
        {
            LogOnce($"registry probe threw, using engine controllers: {ex.Message}");
            return -1;
        }
    }

    private Binding Bind()
    {
        var binding = new Binding();
        try
        {
            binding.MissionControllerType = _findType(MissionControllerTypeName);
            binding.BattleControllerType = _findType(BattleControllerTypeName);
            if (binding.MissionControllerType == null)
                LogOnce($"'{MissionControllerTypeName}' not found: a co-op session is live but its missions are " +
                        "unreadable, so ownership falls back to engine controllers. If this is BannerlordCoop, " +
                        "its API may have changed.");

            var provider = _findType(ContainerProviderTypeName);
            var registryType = _findType(RegistryTypeName);
            if (provider == null || registryType == null)
            {
                LogOnce($"'{ContainerProviderTypeName}' or '{RegistryTypeName}' not found: ownership falls back to " +
                        "engine controllers. If this is BannerlordCoop, its API may have changed.");
            }
            else
            {
                binding.ResolveRegistry = CompileResolve(provider, registryType);
                binding.Probe = CompileProbe(registryType);
            }

            if (binding.BattleControllerType != null)
                binding.SessionIsLocalHost = CompileSessionIsLocalHost(binding.BattleControllerType);
        }
        catch (Exception ex)
        {
            binding.ResolveRegistry = null;
            binding.Probe = null;
            LogOnce($"binding failed, ownership falls back to engine controllers: {ex.Message}");
        }
        return binding;
    }

    // () => ContainerProvider.TryResolve<INetworkAgentRegistry>(out var r) ? (object)r : null
    private static Func<object?>? CompileResolve(Type provider, Type registryType)
    {
        var method = provider.GetMethod("TryResolve", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?.MakeGenericMethod(registryType);
        if (method == null) return null;

        var instance = Expression.Variable(registryType);
        var body = Expression.Block(new[] { instance },
            Expression.Condition(
                Expression.Call(method, instance),
                Expression.Convert(instance, typeof(object)),
                Expression.Constant(null, typeof(object))));
        return Expression.Lambda<Func<object?>>(body).Compile();
    }

    // (registry, agent) => TryGetAgentInfo(agent, out _) ? (IsLocallyControlled(agent) ? 2 : 1) : 0
    private static Func<object, object, int>? CompileProbe(Type registryType)
    {
        var tryGet = FindTryGetAgentInfo(registryType);
        if (tryGet == null) return null;
        var agentType = tryGet.GetParameters()[0].ParameterType;
        var infoType = tryGet.GetParameters()[1].ParameterType.GetElementType()!;
        var isLocal = registryType.GetMethod("IsLocallyControlled", new[] { agentType });
        if (isLocal == null) return null;

        var registryArg = Expression.Parameter(typeof(object), "registry");
        var agentArg = Expression.Parameter(typeof(object), "agent");
        var registry = Expression.Convert(registryArg, registryType);
        var agent = Expression.Convert(agentArg, agentType);
        var info = Expression.Variable(infoType);

        var body = Expression.Block(new[] { info },
            Expression.Condition(
                Expression.Call(registry, tryGet, agent, info),
                Expression.Condition(
                    Expression.Call(registry, isLocal, agent),
                    Expression.Constant(RegisteredHere),
                    Expression.Constant(RegisteredElsewhere)),
                Expression.Constant(Unregistered)));
        return Expression.Lambda<Func<object, object, int>>(body, registryArg, agentArg).Compile();
    }

    private static System.Reflection.MethodInfo? FindTryGetAgentInfo(Type registryType)
    {
        foreach (var method in registryType.GetMethods())
        {
            if (method.Name != "TryGetAgentInfo" || method.ReturnType != typeof(bool)) continue;
            var parameters = method.GetParameters();
            if (parameters.Length == 2 && parameters[0].ParameterType == typeof(Agent) && parameters[1].IsOut)
                return method;
        }
        return null;
    }

    // controller => controller.Session != null && controller.Session.IsLocalHost
    private static Func<object, bool>? CompileSessionIsLocalHost(Type controllerType)
    {
        var sessionProperty = controllerType.GetProperty("Session");
        var isLocalHost = sessionProperty?.PropertyType.GetProperty("IsLocalHost");
        if (sessionProperty == null || isLocalHost == null) return null;

        var arg = Expression.Parameter(typeof(object), "controller");
        var session = Expression.Variable(sessionProperty.PropertyType);
        var body = Expression.Block(new[] { session },
            Expression.Assign(session, Expression.Property(Expression.Convert(arg, controllerType), sessionProperty)),
            Expression.Condition(
                Expression.NotEqual(session, Expression.Constant(null, sessionProperty.PropertyType)),
                Expression.Property(session, isLocalHost),
                Expression.Constant(false)));
        return Expression.Lambda<Func<object, bool>>(body, arg).Compile();
    }

    private void LogOnce(string message)
    {
        if (Interlocked.Exchange(ref _warned, 1) == 1) return;
        try { _logger?.LogWarning($"[CoopMission] {message}"); } catch { /* logging must never throw here */ }
    }

    private sealed class Binding
    {
        public Type? MissionControllerType;
        public Type? BattleControllerType;
        public Func<object?>? ResolveRegistry;
        public Func<object, object, int>? Probe;
        public Func<object, bool>? SessionIsLocalHost;
    }

    /// <summary>What was bound for one mission. Holds no Mission-owning reference past the next mission.</summary>
    internal sealed class MissionLink
    {
        private readonly WeakReference _mission;
        private readonly WeakReference? _battleController;

        public MissionLink(object mission, bool managed, object? registry, object? battleController)
        {
            _mission = new WeakReference(mission);
            Managed = managed;
            Registry = registry;
            _battleController = battleController == null ? null : new WeakReference(battleController);
        }

        public object? Mission => _mission.Target;

        /// <summary>The mission carries a co-op controller (or co-op's types are unreadable).</summary>
        public bool Managed { get; }

        public object? Registry { get; }

        public object? BattleController => _battleController?.Target;
    }

    /// <summary>The engine reads the ownership decision needs, behind a seam so tests need no engine agent.</summary>
    internal interface IAgentFacts
    {
        /// <summary>What the registry is asked about (the Agent).</summary>
        object Key { get; }

        bool IsHuman { get; }

        /// <summary>The engine controller is not None.</summary>
        bool HasLiveController { get; }

        bool TryGetRider(out object riderKey, out bool riderHasLiveController);
    }

    private readonly struct EngineAgentFacts : IAgentFacts
    {
        private readonly Agent _agent;

        public EngineAgentFacts(Agent agent) => _agent = agent;

        public object Key => _agent;

        public bool IsHuman => _agent.IsHuman;

        public bool HasLiveController => _agent.Controller != AgentControllerType.None;

        public bool TryGetRider(out object riderKey, out bool riderHasLiveController)
        {
            var rider = _agent.RiderAgent;
            riderKey = rider!;
            riderHasLiveController = rider != null && rider.Controller != AgentControllerType.None;
            return rider != null;
        }
    }
}
