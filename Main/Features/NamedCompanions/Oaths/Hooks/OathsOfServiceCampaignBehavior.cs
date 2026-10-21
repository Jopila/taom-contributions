using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.NamedCompanions.Oaths.Hooks;

/// <summary>
/// Oaths of Service campaign entry point (#904): persists the ledger and runs the home anchor. Built with
/// <c>new</c> by <see cref="OathsOfServiceModule"/> for every campaign, before any event and before a load's
/// SyncData, so its constructor is the per-campaign reset. Two tests pin that:
/// OathsOfServiceWiringTests.CampaignBehavior_ConstructorResetsTheLedger (the constructor body) and
/// OathsOfServiceWiringTests.Module_BuildsAFreshBehaviorPerCampaign_OverOneSharedLedger (a fresh instance per
/// Create, over one ledger, and a flag set before Create is gone after it).
/// </summary>
public sealed class OathsOfServiceCampaignBehavior : CampaignBehaviorBase
{
    private readonly OathLedger _ledger;
    private readonly HomeAnchorService _anchor;
    private readonly IModLogger _logger;
    private bool _anchorDue;

    public OathsOfServiceCampaignBehavior(OathLedger ledger, HomeAnchorService anchor, IModLogger logger)
    {
        _ledger = ledger;
        _anchor = anchor;
        _logger = logger;
        _ledger.ResetForNewSession();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
    }

    /// <summary>A list and a dictionary, both basic saveable containers. A load fills a fresh record over the constructor's reset.</summary>
    public override void SyncData(IDataStore dataStore)
    {
        List<string> flags = null;
        Dictionary<string, int> topicDays = null;
        if (dataStore.IsSaving)
        {
            var data = _ledger.Export();
            flags = data.Flags;
            topicDays = data.TopicDays;
        }
        dataStore.SyncData("_taom_oath_flagList", ref flags);
        dataStore.SyncData("_taom_oath_topicDays", ref topicDays);
        if (!dataStore.IsLoading) return;

        _ledger.Import(new OathLedgerSaveData { Flags = flags, TopicDays = topicDays });
    }

    // Vanilla's SwapCompanions runs on the same daily tick after this listener (listeners run newest first), so a
    // move made on the daily tick would be undone the same day. The daily tick only raises the flag; the next hourly
    // tick moves the companions once.
    internal void OnDailyTick() => _anchorDue = true;

    internal void OnHourlyTick()
    {
        if (!_anchorDue) return;
        _anchorDue = false;
        try { _anchor.AnchorAll(); }
        catch (Exception ex) { _logger.LogError($"[Oaths] home anchor failed: {ex}"); }
    }
}
