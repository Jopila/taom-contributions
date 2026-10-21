using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FarWatch.Hooks;

/// <summary>
/// What ends or completes a ride from outside its wait menu (#847): the hourly look, the main party's encounters and its map
/// events. They reach the ride through <see cref="FarWatchService"/>, never through the menu, so a ride whose menu an encounter
/// pre-empted still ends. None of them switches a menu: an encounter is opening its own, and otherwise the wait menu's own tick
/// sees the ride gone and lands the player (<see cref="FarWatchWaitMenu"/>), so there is one exit path. The up-to-an-hour latency
/// of the hourly cases is accepted (docs/features/far-watch.md "What ends the wait"). A listener that runs for every encounter
/// or map event in the world returns on one store read when no ride runs.
/// </summary>
internal sealed class FarWatchReconcile
{
    private readonly FarWatchService _service;
    private readonly IFarWatchWorldAdapter _world;
    private readonly FarWatchPresenter _presenter;
    private readonly IModLogger _logger;
    private bool _faultLogged;

    public FarWatchReconcile(FarWatchService service, IFarWatchWorldAdapter world, FarWatchPresenter presenter, IModLogger logger)
    {
        _service = service;
        _world = world;
        _presenter = presenter;
        _logger = logger;
    }

    /// <summary>A ride that ends here is told in one line; one that is due is completed and its report opens over whatever is current.</summary>
    internal void OnHourlyTick()
    {
        try
        {
            if (!_service.HasRide) return;
            double now = _world.NowHours;
            var turn = _service.Reconcile(_world.ReadCamp(), now);
            if (turn.Kind == ScoutTurnKind.Nothing) return;

            if (turn.Kind == ScoutTurnKind.Ended) _presenter.End(turn.Notice);
            else _presenter.Report(_service.Complete(now));
        }
        catch (Exception ex)
        {
            Fault("hourly look", ex);
        }
    }

    /// <summary>
    /// Any encounter with the camp ends the ride, peaceful or not. The engine raises it at the end of
    /// <c>EncounterManager.StartPartyEncounter</c>, after the meeting or battle menu is chosen, and an attack that opens a plain
    /// meeting (<c>encounter_meeting</c>, no battle) never raises <c>MapEventStarted</c>.
    /// </summary>
    internal void OnPartyEncounter(PartyBase attacker, PartyBase defender)
    {
        try
        {
            if (!_service.HasRide || !_world.InvolvesMainParty(attacker, defender)) return;
            _presenter.End(_service.End(_world.NowHours, ScoutEndReason.Battle));
        }
        catch (Exception ex)
        {
            Fault("encounter listener", ex);
        }
    }

    /// <summary>A map event that holds the main party ends the ride; the engine has already put the encounter menu up, so this lands nowhere.</summary>
    internal void OnMapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
    {
        try
        {
            if (!_service.HasRide || !_world.IsMainPartyInMapEvent) return;
            _presenter.End(_service.End(_world.NowHours, ScoutEndReason.Battle));
        }
        catch (Exception ex)
        {
            Fault("map event listener", ex);
        }
    }

    /// <summary>Logs the first fault of this class and drops the ride, so a fault that repeats every hour cannot hold the party in a half state.</summary>
    private void Fault(string callback, Exception ex)
    {
        if (!_faultLogged)
        {
            _faultLogged = true;
            _logger.LogError($"[FarWatch] the {callback} failed and the ride was dropped: {ex}");
        }

        try
        {
            _service.End(_world.NowHours, ScoutEndReason.CallBack);
        }
        catch
        {
            // The store is plain data; nothing more can be done for it here.
        }
    }
}
