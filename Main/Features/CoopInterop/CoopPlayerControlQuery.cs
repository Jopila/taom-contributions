using System;
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
/// The registry lists joiners only; the server's own placeholder hero is never in it. Only the MethodInfo
/// is cached: whether an entity is a player changes as peers join and leave.
/// </summary>
public sealed class CoopPlayerControlQuery : IPlayerControlQuery
{
    private const string PlayerManagerTypeName = "GameInterface.Services.Players.PlayerManager";

    private readonly ICoopSessionProvider _session;
    private readonly IModLogger _logger;
    private readonly Func<string, Type?> _findType;
    private readonly Lazy<MethodInfo?> _tryGetControlledObjectInfo;
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
        _tryGetControlledObjectInfo = new Lazy<MethodInfo?>(Bind);
    }

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
        var method = _tryGetControlledObjectInfo.Value;
        if (method == null) return PlayerControl.Unknown;

        try
        {
            // bool TryGetControlledObjectInfo(object, out ControlledObjectInfo): only the bool matters.
            var args = new object?[] { entity, null };
            return method.Invoke(null, args) is true ? PlayerControl.Player : PlayerControl.NotPlayer;
        }
        catch (Exception ex)
        {
            // Never let another mod's internals throw into a campaign tick.
            LogOnce($"registry probe threw, treating as unknown: {ex.Message}");
            return PlayerControl.Unknown;
        }
    }

    private MethodInfo? Bind()
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
            }
            return method;
        }
        catch (Exception ex)
        {
            LogOnce($"binding failed, treating player control as unknown: {ex.Message}");
            return null;
        }
    }

    private void LogOnce(string message)
    {
        if (_warnedOnce) return;
        _warnedOnce = true;
        try { _logger?.LogWarning($"[CoopPlayerControl] {message}"); } catch { /* logging must never throw here */ }
    }
}
