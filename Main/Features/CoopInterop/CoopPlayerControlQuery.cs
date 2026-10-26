using System;
using System.Linq.Expressions;
using System.Reflection;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.CoopInterop;

/// <summary>
/// Binds <see cref="IPlayerControlQuery"/> to BannerlordCoop's player registry BY REFLECTION, with
/// the same no-compile-time-reference rule as <see cref="CoopSessionProvider"/>.
///
/// Member bound: <c>GameInterface.Services.Players.PlayerManager.TryGetControlledObjectInfo(object, out
/// ControlledObjectInfo)</c> -> bool. One method answers for a party and a clan (a clan counts when its
/// leader is registered). During a session the registry alone decides, including for the local entity.
///
/// The registry lists joiners only; the server's own placeholder hero is never in it. Only the bound method and its
/// compiled form are cached: whether an entity is a player changes as peers join and leave. The probe is compiled once into
/// a <c>Func&lt;object, bool&gt;</c>; a method of another shape keeps the <c>MethodInfo.Invoke</c> path.
/// </summary>
public sealed class CoopPlayerControlQuery : IPlayerControlQuery
{
    private const string PlayerManagerTypeName = "GameInterface.Services.Players.PlayerManager";

    private readonly ICoopSessionProvider _session;
    private readonly IModLogger _logger;
    private readonly Func<string, Type?> _findType;
    private readonly Lazy<RegistryProbe?> _probe;
    private bool _warnedOnce;

    public CoopPlayerControlQuery(ICoopSessionProvider session, IModLogger logger)
        : this(session, logger, ReflectionUtils.FindTypeAcrossLoadedAssemblies)
    {
    }

    // Internal for TAOM.Tests (InternalsVisibleTo): the type lookup is the only seam.
    internal CoopPlayerControlQuery(ICoopSessionProvider session, IModLogger logger,
        Func<string, Type?> findType)
    {
        _session = session;
        _logger = logger;
        _findType = findType;
        // Bind once, success or failure. The registry type lives in GameInterface, which Coop loads
        // in its own OnSubModuleLoad, so it is present before any session can be live; a retry would
        // only rescan the AppDomain for an answer that cannot change.
        _probe = new Lazy<RegistryProbe?>(Bind);
    }

    // Internal for TAOM.Tests: true when the registry method was bound and compiled to a delegate.
    internal bool CompiledProbeBound => _probe.Value?.Compiled != null;

    public PlayerControl OfParty(MobileParty? party)
    {
        if (party == null) return PlayerControl.NotPlayer;
        return Classify(party, party == MobileParty.MainParty);
    }

    public PlayerControl OfClan(Clan? clan)
    {
        if (clan == null) return PlayerControl.NotPlayer;
        return Classify(clan, clan == Clan.PlayerClan);
    }

    public PlayerControl OfHero(Hero? hero)
    {
        if (hero == null) return PlayerControl.NotPlayer;
        return Classify(hero, hero == Hero.MainHero);
    }

    // Internal for TAOM.Tests: the whole decision below the engine-type comparisons.
    internal PlayerControl Classify(object entity, bool isLocalPlayer)
    {
        // Without a session the answer is the local comparison and never touches reflection
        // (IsSessionActive is false without a type scan when no co-op module is enabled).
        if (!_session.IsSessionActive) return isLocalPlayer ? PlayerControl.Player : PlayerControl.NotPlayer;

        // In a session the registry alone decides, for the local entity too. No human plays on the
        // authority process, and the registry lists joiners only, never the server's own hero. On the
        // co-op server Hero.MainHero, MainParty and PlayerClan are a guest's for a while (inside
        // BarterPlayerContext and MainHeroSubstitutionScope), so "is the local entity" is not a stable
        // identity there.
        return ReadRegistry(entity);
    }

    private PlayerControl ReadRegistry(object entity)
    {
        var probe = _probe.Value;
        if (probe == null) return PlayerControl.Unknown;

        try
        {
            bool registered;
            if (probe.Compiled != null)
            {
                registered = probe.Compiled(entity);
            }
            else
            {
                // bool TryGetControlledObjectInfo(object, out ControlledObjectInfo): only the bool matters.
                var args = new object?[] { entity, null };
                registered = probe.Method.Invoke(null, args) is true;
            }

            return registered ? PlayerControl.Player : PlayerControl.NotPlayer;
        }
        catch (Exception ex)
        {
            // Never let another mod's internals throw into a campaign tick.
            LogOnce($"registry probe threw, treating as unknown: {ex.Message}");
            return PlayerControl.Unknown;
        }
    }

    private RegistryProbe? Bind()
    {
        try
        {
            var manager = _findType(PlayerManagerTypeName);
            var method = manager?.GetMethod(
                "TryGetControlledObjectInfo", BindingFlags.Public | BindingFlags.Static);

            if (method == null)
            {
                LogOnce($"'{PlayerManagerTypeName}.TryGetControlledObjectInfo' not found: a co-op session " +
                        "is live but its player registry is unreadable, so player control is treated as " +
                        "unknown. If this is BannerlordCoop, its API may have changed.");
                return null;
            }

            return new RegistryProbe(method, Compile(method));
        }
        catch (Exception ex)
        {
            LogOnce($"binding failed, treating player control as unknown: {ex.Message}");
            return null;
        }
    }

    // Builds e => TryGetControlledObjectInfo(e, out _). Any other shape or a compile failure returns null, so the
    // reflection path still answers: the compiled form is an optimisation, never a requirement.
    private static Func<object, bool>? Compile(MethodInfo method)
    {
        try
        {
            var parameters = method.GetParameters();
            if (method.ReturnType != typeof(bool) || parameters.Length != 2
                || parameters[0].ParameterType != typeof(object) || !parameters[1].ParameterType.IsByRef)
                return null;

            var entity = Expression.Parameter(typeof(object), "entity");
            var info = Expression.Variable(parameters[1].ParameterType.GetElementType()!);
            return Expression.Lambda<Func<object, bool>>(
                Expression.Block(new[] { info }, Expression.Call(method, entity, info)), entity).Compile();
        }
        catch
        {
            return null;
        }
    }

    private sealed class RegistryProbe
    {
        internal RegistryProbe(MethodInfo method, Func<object, bool>? compiled)
        {
            Method = method;
            Compiled = compiled;
        }

        internal MethodInfo Method { get; }

        internal Func<object, bool>? Compiled { get; }
    }

    private void LogOnce(string message)
    {
        if (_warnedOnce) return;
        _warnedOnce = true;
        try { _logger?.LogWarning($"[CoopPlayerControl] {message}"); } catch { /* logging must never throw here */ }
    }
}
