using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FromTheAshes.Hooks;

/// <summary>
/// What ends a job from outside its wait menu: the hourly reconcile and the main party's map events. Both reach the job
/// through <see cref="FromTheAshesService"/>, never through the menu, so a job whose menu an encounter pre-empted still ends.
/// </summary>
public sealed class FromTheAshesReconcile
{
    private readonly FromTheAshesService _service;
    private readonly IGameMenuAdapter _menus;
    private readonly FromTheAshesLanding _landing;
    private readonly IModLogger _logger;
    private bool _faultLogged;

    public FromTheAshesReconcile(FromTheAshesService service, IGameMenuAdapter menus, FromTheAshesLanding landing, IModLogger logger)
    {
        _service = service;
        _menus = menus;
        _landing = landing;
        _logger = logger;
    }

    private static double Now => CampaignTime.Now.ToHours;

    /// <summary>The hourly reconcile: validates the job on state alone (never the menu id) and ends it when it is due or has gone wrong.</summary>
    public void OnHourlyTick()
    {
        try
        {
            var result = _service.Advance(Now, finishWhenDue: true);
            if (result.Outcome == AshesOutcome.None || result.Outcome == AshesOutcome.Running)
                return;
            FromTheAshesTexts.Report(result);
            if (_menus.CurrentMenuId == FromTheAshesWaitMenu.WaitMenuId)
                _landing.LeaveWaitMenu(Campaign.Current?.CurrentMenuContext?.GameMenu);
        }
        catch (Exception ex)
        {
            Fault("hourly reconcile", ex);
        }
    }

    /// <summary>A map event that holds the main party ends the job; the engine puts the encounter menu up, so this lands nowhere.</summary>
    public void OnMapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
    {
        try
        {
            if (mapEvent != null && mapEvent.InvolvedParties.Contains(PartyBase.MainParty))
                FromTheAshesTexts.Report(_service.Interrupt());
        }
        catch (Exception ex)
        {
            Fault("map event listener", ex);
        }
    }

    /// <summary>Logs the first fault of this class (the callback, and whether a job was dropped) and drops the job.</summary>
    private void Fault(string callback, Exception ex)
    {
        var jobDropped = _service.HasJob;
        if (!_faultLogged)
        {
            _faultLogged = true;
            _logger.LogError($"[FromTheAshes] the {callback} callback failed{(jobDropped ? " and the job was dropped" : "; no job was running")}: {ex}");
        }

        _service.Abandon();
    }
}
