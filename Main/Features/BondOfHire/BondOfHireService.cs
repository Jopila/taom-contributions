using TAOM.Adapters;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.BondOfHire;

/// <summary>
/// Bond of Hire (docs/features/bond-of-hire.md, #867): a clan that signs on as a mercenary stays at least the configured days
/// before it can leave a ruler who is in debt, be hired away by another kingdom or leave in the daily leave consideration. The
/// engine reads <c>Clan.ShouldStayInKingdomUntil</c> for three departures (a debt departure, a hire away and the daily leave
/// consideration) and, for a new mercenary, writes only the caller's date in <c>ChangeKingdomAction.ApplyInternal</c>, which every
/// vanilla caller leaves at the default. The date is the whole mechanism and nothing else is saved here. Pure over
/// the adapter; the one entry point is called from the engine's mercenary-service-started event, after the engine's own write.
/// The service keeps no state, so it needs no per-campaign reset.
/// <para>
/// The date is moved only when it is nearer than the minimum, so a longer date set by anything else is never shortened. A
/// remaining time that is not a number counts as nearer: the positive requirement <c>remaining &gt;= days</c> fails NaN, so a
/// poisoned date is replaced by a real one. This game's own player clan is skipped (vanilla never reads the date for it). A co-op
/// peer that defers to the host writes nothing; the dedicated server is the authority for AI clans, so it binds them.
/// </para>
/// </summary>
public sealed class BondOfHireService
{
    private readonly IBondOfHireSettingsProvider _settings;
    private readonly ICoopSessionProvider _coop;

    public BondOfHireService(IBondOfHireSettingsProvider settings, ICoopSessionProvider coop)
    {
        _settings = settings;
        _coop = coop;
    }

    // A co-op peer that is not the authority leaves a shared-world change to the host (CoopInterop: ShouldDeferToHost also
    // covers co-op mods TAOM cannot probe). The dedicated server is not skipped: it is the authority for AI clans.
    private bool Suppressed => _coop.ShouldDeferToHost;

    /// <summary>
    /// A clan has just signed on as a mercenary. Moves its "stay until" date to the minimum when it is nearer than that.
    /// Returns true when the date was moved.
    /// </summary>
    public bool OnServiceStarted(IMercenaryClanAdapter clan)
    {
        if (Suppressed)
            return false;
        var days = _settings.MinimumDays;
        if (days <= 0 || clan.IsPlayerClan)
            return false;
        if (clan.RemainingStayDays >= days)
            return false;

        clan.SetStayUntilDaysFromNow(days);
        return true;
    }
}
