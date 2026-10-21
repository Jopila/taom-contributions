using System;
using System.Diagnostics;
using System.Reflection;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;

namespace TAOM.Features.CoopInterop;

/// <summary>
/// Binds <see cref="ICoopSessionProvider"/> to BannerlordCoop's runtime state BY REFLECTION.
///
/// No compile-time reference to Coop's assemblies, on purpose: TAOM must load and behave identically
/// when Coop is absent, when it is present but idle, and when a future Coop build renames these
/// members. Every probe therefore fails to "no session", which
/// <see cref="CoopSessionPolicy.IsAuthority"/> maps to authoritative — i.e. exactly today's
/// singleplayer behaviour.
///
/// Members bound (verified against the shipped v0.0.3 build, 2026-08-01):
///   GameInterface.ContainerProvider.TryGetContainer(out ILifetimeScope) -> bool
///   Common.ModInformation.IsServer -> bool (static property)
///
/// Two traps this class exists to avoid, both verified in the decompiled source:
///   * ContainerProvider.Alive is `public static bool Alive { get; } = _lifetimeScope != null;` —
///     an inline static initialiser evaluated while the field is still null, so it is PERMANENTLY
///     FALSE. We bind TryGetContainer instead.
///   * ModInformation.IsServer defaults false, IsClient is its negation, and neither is reset on
///     teardown. Read alone, IsClient reports true in plain singleplayer whenever the Coop module is
///     enabled. It is only meaningful when a session is actually live, which is why the policy takes
///     both inputs.
///
/// See docs/research/bannerlordcoop-internals.md.
/// </summary>
public sealed class CoopSessionProvider : ICoopSessionProvider
{
    private const string ContainerProviderTypeName = "GameInterface.ContainerProvider";
    private const string ModInformationTypeName = "Common.ModInformation";

    /// <summary>
    /// Minimum gap between bind attempts while the co-op types are not found. The module is enabled
    /// but its assembly can load after the first probe, so a failed bind is retried, but never on
    /// every call: the type scan walks the whole AppDomain.
    /// </summary>
    internal const long BindRetryIntervalMs = 5000;

    private readonly IModLogger _logger;
    private readonly Func<string, Type?> _findType;
    private readonly Func<long> _nowMs;

    // Resolved once, then reused. Binding is the expensive part (an AppDomain-wide type scan);
    // the INVOCATION must still happen per call because the answer changes across a session.
    //
    // Guarded by _bindLock rather than a bare `if (_bound)` check. Campaign ticks are main-thread
    // today, so a race is unlikely — but the consequence is not merely a duplicated type scan. The
    // failure branch below deliberately NULLS _tryGetContainer when the role property is missing, so
    // that an unreadable role keeps us authoritative. A second thread observing the intermediate
    // state (method bound, null-out not yet applied) would read sessionActive=true with isServer
    // defaulting false and conclude IsAuthority=false — standing TAOM down on exactly the input the
    // fail-open design promises to survive. Cheap to hold: the lock is contended only until bound.
    private readonly object _bindLock = new();
    // volatile because EnsureBound double-checks this OUTSIDE the lock; without it the fast path
    // may observe a stale or reordered write and use a half-bound provider.
    private volatile bool _bound;
    private volatile MethodInfo? _tryGetContainer;
    private volatile PropertyInfo? _isServer;
    private bool _bindingFailedLogged;
    private bool _recoveryLogged;
    private bool _bindAttempted;
    private long _nextBindAttemptMs;

    public CoopSessionProvider(IModLogger logger)
        : this(logger, ReflectionUtils.FindTypeAcrossLoadedAssemblies,
               () => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency)
    {
    }

    // Internal for TAOM.Tests (InternalsVisibleTo): the type lookup and the clock are the two seams.
    internal CoopSessionProvider(IModLogger logger, Func<string, Type?> findType, Func<long> nowMs)
    {
        _logger = logger;
        _findType = findType;
        _nowMs = nowMs;
    }

    // Internal for TAOM.Tests (InternalsVisibleTo).
    internal bool RoleProbeAvailable => _tryGetContainer != null;

    public bool IsSessionActive => Probe().sessionActive;

    public bool IsAuthority
    {
        get
        {
            var (sessionActive, isServer) = Probe();
            return CoopSessionPolicy.IsAuthority(sessionActive, isServer);
        }
    }

    public bool IsCoopClient
    {
        get
        {
            var (sessionActive, isServer) = Probe();
            return CoopSessionPolicy.IsCoopClient(sessionActive, isServer);
        }
    }

    public bool ShouldDeferToHost
    {
        get
        {
            // Deliberately reads CoopPresence directly rather than going through Probe()'s early
            // return, because the two signals differ here: a co-op module can be ACTIVE while the
            // role probe is unavailable (an unprobeable mod such as BannerlordTogether), and that
            // combination must defer rather than fall through to "no session, therefore solo".
            if (!CoopPresence.IsActive)
                return CoopSessionPolicy.ShouldDeferToHost(false, false, false, false);

            EnsureBound();
            var roleProbeAvailable = RoleProbeAvailable;
            var (sessionActive, isServer) = roleProbeAvailable ? Probe() : (false, false);

            return CoopSessionPolicy.ShouldDeferToHost(
                coopModuleActive: true, roleProbeAvailable, sessionActive, isServer);
        }
    }

    private (bool sessionActive, bool isServer) Probe()
    {
        // Skip the AppDomain type scan entirely when no co-op module was ever enabled. This is the
        // overwhelmingly common case (every solo player), and CoopPresence has already cached it.
        if (!CoopPresence.IsActive) return (false, false);

        EnsureBound();
        if (_tryGetContainer == null) return (false, false);

        try
        {
            // bool TryGetContainer(out ILifetimeScope) — we only care about the bool.
            var args = new object?[] { null };
            var sessionActive = _tryGetContainer.Invoke(null, args) is true;
            if (!sessionActive) return (false, false);

            var isServer = _isServer?.GetValue(null) is true;
            return (true, isServer);
        }
        catch (Exception ex)
        {
            // Never let another mod's internals throw into a campaign tick.
            LogOnce($"probe threw, treating as no session: {ex.Message}");
            return (false, false);
        }
    }

    internal void EnsureBound()
    {
        if (_bound) return;
        lock (_bindLock)
        {
            if (_bound) return;

            // A failed bind is not final: the co-op assembly may load after the first probe. Retry,
            // but at most once per BindRetryIntervalMs so a hot path does not rescan every call.
            var now = _nowMs();
            if (_bindAttempted && now < _nextBindAttemptMs) return;

            BindLocked();
            _bindAttempted = true;
            if (_tryGetContainer == null)
            {
                _nextBindAttemptMs = now + BindRetryIntervalMs;
                return;
            }

            _bound = true;   // set LAST, so no other thread can observe a half-bound state
            LogRecoveryIfFailedBefore();
        }
    }

    private void BindLocked()
    {
        try
        {
            var containerProvider = _findType(ContainerProviderTypeName);
            var tryGetContainer = containerProvider?.GetMethod(
                "TryGetContainer", BindingFlags.Public | BindingFlags.Static);

            var modInformation = _findType(ModInformationTypeName);
            var isServer = modInformation?.GetProperty(
                "IsServer", BindingFlags.Public | BindingFlags.Static);

            // Bind through locals and publish at the end: a failed bind is now retried while other
            // threads read these fields outside the lock, so they must never see the method set
            // before the role-property check below has had its say.
            _isServer = isServer;
            _tryGetContainer = isServer != null ? tryGetContainer : null;

            if (tryGetContainer == null)
            {
                LogOnce($"'{ContainerProviderTypeName}.TryGetContainer' not found — a co-op module is " +
                        "enabled but its session state is unreadable, so TAOM stays authoritative. " +
                        "If this is BannerlordCoop, its API may have changed.");
            }
            else if (isServer == null)
            {
                // Session detectable but role is not. Authority would then hinge on a default-false
                // flag, making every peer a client and disabling TAOM's campaign logic for everyone.
                // Treating the whole probe as unavailable keeps the host correct.
                LogOnce($"'{ModInformationTypeName}.IsServer' not found — cannot tell host from " +
                        "client, so TAOM stays authoritative rather than standing every peer down.");
            }
        }
        catch (Exception ex)
        {
            _tryGetContainer = null;
            LogOnce($"binding failed, treating as no session: {ex.Message}");
        }
    }

    private void LogRecoveryIfFailedBefore()
    {
        if (!_bindingFailedLogged || _recoveryLogged) return;
        _recoveryLogged = true;
        try { _logger?.LogInfo("[CoopSession] role probe bound on a later attempt; co-op session state is now readable."); }
        catch { /* logging must never throw here */ }
    }

    private void LogOnce(string message)
    {
        if (_bindingFailedLogged) return;
        _bindingFailedLogged = true;
        try { _logger?.LogWarning($"[CoopSession] {message}"); } catch { /* logging must never throw here */ }
    }
}
