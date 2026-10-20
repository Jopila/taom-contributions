using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Persistence;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.HiddenHosts;

/// <summary>
/// Hidden Hosts' memory (#846 phase 3, docs/features/hidden-hosts.md "Memory"): the campaign events that age and forget
/// remembered counts, the Scouting XP grant and the save (the exact stamps are <see cref="HiddenHostsPlainSightBehavior"/>).
/// The service decides. Built with <c>new</c> by
/// <see cref="HiddenHostsModule"/> for every campaign, so its constructor resets the singleton service before any event or
/// load (pinned by HiddenHostsWiringTests). With the toggle off no XP is granted; the fade, visibility and destroy handlers
/// keep running, because they only shrink state that exists. Rows are per-player presentation, so only the XP grant is co-op
/// gated. The XP is granted from the campaign tick and again just before a save, so a save made while time is stopped holds
/// the XP of the reads whose latch it stores. A handler that throws logs once per kind and carries on.
/// </summary>
internal sealed class HiddenHostsMemoryBehavior : CampaignBehaviorBase
{
    private const string CountKey = "_taomHiddenHostsMemoryCount";
    private const string ChunkPrefix = "_taomHiddenHostsMemory_";
    private const string Tag = "[HiddenHosts]";

    private readonly HiddenHostsService _service;
    private readonly IHiddenHostsCampaignAdapter _adapter;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _reported = new();
    private bool _skippedLogged;

    public HiddenHostsMemoryBehavior(HiddenHostsService service, IHiddenHostsCampaignAdapter adapter, ICoopSessionProvider coop,
        IModLogger logger)
    {
        _service = service;
        _adapter = adapter;
        _coop = coop;
        _logger = logger;
        _service.ResetForNewSession();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, OnBeforeSave);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.PartyVisibilityChangedEvent.AddNonSerializedListener(this, OnVisibilityChanged);
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnPartyDestroyed);
    }

    public override void SyncData(IDataStore dataStore)
    {
        var chunks = SyncDataChunker.SyncChunks(dataStore, CountKey, ChunkPrefix, dataStore.IsSaving ? EncodeForSave() : null, _logger, Tag);
        if (dataStore.IsLoading) LoadSaved(chunks);
    }

    internal void OnTick(float dt) => GrantPendingXp();

    /// <summary><c>SaveHandler.SaveTick</c> raises this (through <c>CampaignEventDispatcher.OnBeforeSave</c>) before the save is written; a stopped game has no tick to grant the XP whose latch is saved.</summary>
    internal void OnBeforeSave() => GrantPendingXp();

    internal void OnHourlyTick()
    {
        try
        {
            _service.FadeHourly(_adapter.NowHour, _adapter.MainScoutingSkill, _adapter.IsSettlementInSight);
            if (_skippedLogged || _service.MemorySkippedInserts == 0) return;
            _skippedLogged = true;
            _logger.LogWarning($"{Tag} memory: every row is fresh and the table is full, new hosts are not remembered this session.");
        }
        catch (Exception ex) { Report("hourly", ex); }
    }

    internal void OnVisibilityChanged(PartyBase party)
    {
        try
        {
            if (!_adapter.TryReadVisibilityChange(party, out var id, out bool visible)) return;
            _service.OnVisibilityChanged(id, visible, visible ? 0 : _adapter.MainScoutingSkill);
        }
        catch (Exception ex) { Report("visibility", ex); }
    }

    internal void OnPartyDestroyed(MobileParty party, PartyBase destroyer)
    {
        try
        {
            var id = party?.StringId;
            if (!string.IsNullOrEmpty(id)) _service.Forget(id!);
        }
        catch (Exception ex) { Report("destroyed", ex); }
    }

    /// <summary>The XP is taken every tick, granted only with the feature on and by the authority, and dropped otherwise, so a client can never bank it.</summary>
    private void GrantPendingXp()
    {
        int xp = _service.TakePendingScoutXp();
        if (xp <= 0) return;
        try
        {
            if (!_service.Enabled || !_coop.IsAuthority || !CoopSessionPolicy.MayWriteSaveBackedState(_coop.IsCoopClient)) return;
            _adapter.GrantScoutingXp(xp);
        }
        catch (Exception ex) { Report("xp", ex); }
    }

    /// <summary>A fault while encoding saves nothing rather than half a payload: zero chunks, logged.</summary>
    private IReadOnlyList<string> EncodeForSave()
    {
        try { return SyncDataChunker.Split(_service.EncodeMemory(_adapter.NowHour)); }
        catch (Exception ex)
        {
            Report("save", ex);
            return Array.Empty<string>();
        }
    }

    private void LoadSaved(IReadOnlyList<string> chunks)
    {
        try
        {
            var report = _service.LoadFrom(SyncDataChunker.Join(chunks));
            if (report.UnknownFormat) _logger.LogWarning($"{Tag} memory load: unknown memory format, starting empty.");
            if (report.Invalid > 0) _logger.LogWarning($"{Tag} memory load: {report.Invalid} invalid rows skipped.");
            if (report.CapReached) _logger.LogWarning($"{Tag} memory load: too many rows, the rest was dropped.");
        }
        catch (Exception ex)
        {
            _service.ResetForNewSession();
            Report("load", ex);
        }
    }

    private void Report(string handler, Exception ex)
    {
        if (_reported.Add(handler + ":" + ex.GetType().Name))
            _logger.LogError($"{Tag} memory {handler} failed, carrying on: {ex}");
    }
}
