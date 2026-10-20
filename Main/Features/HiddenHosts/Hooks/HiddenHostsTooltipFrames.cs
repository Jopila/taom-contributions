using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TAOM.Adapters;

namespace TAOM.Features.HiddenHosts.Hooks;

/// <summary>
/// Turns a tooltip's subject into an open <see cref="TooltipFrame"/> (#846 phase 2): the adapter read plus the service
/// verdict, so each tooltip hook stays thin. The one place that knows which engine types a frame can be about. An exempt
/// subject still gets a frame, with the vanilla verdict, so a reader never sees an outer frame through it.
/// </summary>
internal static class HiddenHostsTooltipFrames
{
    /// <summary>True for the five types vanilla registers a map tooltip refresher for and Hidden Hosts reads.</summary>
    internal static bool IsFrameType(Type? type) =>
        type == typeof(MobileParty) || type == typeof(Army) || type == typeof(Settlement)
        || type == typeof(MapEvent) || type == typeof(SiegeEvent);

    /// <summary>The frame for <paramref name="subject"/>, or null when it is none of the five subject types.</summary>
    internal static TooltipFrame? Build(object? subject, HiddenHostsService service, IPartyIntelAdapter adapter)
    {
        switch (subject)
        {
            case MobileParty party:
                return new TooltipFrame(TooltipSubjectKind.Party, service.EvaluateTooltipParty(adapter.Read(party)));
            case Army army:
                // An army is judged on its leader: the leader's healthy count includes the attached parties, as its nameplate does.
                return new TooltipFrame(TooltipSubjectKind.Army, service.EvaluateTooltipArmy(adapter.Read(army.LeaderParty)));
            case Settlement settlement:
                return new TooltipFrame(TooltipSubjectKind.Settlement, service.EvaluateTooltipSettlement(adapter.ReadSettlement(settlement)));
            case MapEvent mapEvent:
                return new TooltipFrame(TooltipSubjectKind.MapEvent, service.EvaluateTooltipEvent(adapter.ReadMapEvent(mapEvent)));
            case SiegeEvent siegeEvent:
                return new TooltipFrame(TooltipSubjectKind.SiegeEvent, service.EvaluateTooltipEvent(adapter.ReadSiegeEvent(siegeEvent)));
            default:
                return null;
        }
    }

    /// <summary>
    /// Opens the frame for <paramref name="subject"/> and returns the token that closes it. Everything that can throw runs
    /// before the frame is published, so a fault leaves no frame open and a default token (that tooltip shows vanilla text).
    /// The toggle off (read inside the try: a throwing settings object is a fault, not an escape), the fault budget spent, a
    /// missing service and an unknown subject also give a default token.
    /// </summary>
    internal static ScopeToken Open(object? subject, string hook)
    {
        var service = HiddenHostsCalls.Service;
        var adapter = HiddenHostsCalls.Adapter;
        if (subject == null || HiddenHostsCalls.Stopped || service == null || adapter == null) return default;
        try
        {
            if (!service.Enabled) return default;
            var frame = Build(subject, service, adapter);
            return frame == null ? default : HiddenHostsTooltipScope.Open(frame);
        }
        catch (Exception ex)
        {
            HiddenHostsCalls.ReportFault(hook, ex);
            return default;
        }
    }
}
