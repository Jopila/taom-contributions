using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.BondOfHire.Hooks;

/// <summary>
/// Thin boundary for Bond of Hire (docs/features/bond-of-hire.md, #867, ADR-002): one listener on the engine's
/// <c>OnMercenaryServiceStartedEvent</c>, which every hiring path raises inside <c>StartMercenaryServiceAction.ApplyStart</c>
/// after the engine's own clan write. The clan is wrapped in the adapter and the service decides. No Harmony: the route is an
/// event. The body is inside a try, because an <c>MbEvent</c> invokes its listeners with no try and a fault here would reach the
/// engine's hiring code. Saves nothing (the date is the engine's own clan field), and the service holds no per-campaign state;
/// the module still builds this behavior with <c>new</c> for every campaign, as the campaign registers the event on the instance.
/// </summary>
public sealed class BondOfHireBehavior : CampaignBehaviorBase
{
    private readonly BondOfHireService _service;
    private readonly IModLogger _logger;

    public BondOfHireBehavior(BondOfHireService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnMercenaryServiceStartedEvent.AddNonSerializedListener(this, OnMercenaryServiceStarted);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    // Internal for TAOM.Tests (InternalsVisibleTo): the event cannot be raised outside a campaign.
    internal void OnMercenaryServiceStarted(Clan clan, StartMercenaryServiceAction.StartMercenaryServiceActionDetails details) =>
        Handle(clan == null ? null : new MercenaryClanAdapter(clan));

    internal void Handle(IMercenaryClanAdapter? clan)
    {
        try
        {
            if (clan != null)
                _service.OnServiceStarted(clan);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[BondOfHire] signing handling failed: {ex.Message}");
        }
    }
}
