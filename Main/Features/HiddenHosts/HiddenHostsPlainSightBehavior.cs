using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.HiddenHosts;

/// <summary>
/// The exact stamps (#846 phase 3, docs/features/hidden-hosts.md "Memory"): everything in the player's plain sight is
/// counted exactly and remembered. Two triggers, because one is not enough. <c>TickEvent</c> never fires while a plain
/// game menu stops time (<c>Campaign.Tick</c> raises it only for a positive <c>dt</c>, and a menu sets the time mode to
/// stop), so a town, castle, village, encounter or conversation visit would never be stamped from it; <c>GameMenuOpened</c>
/// (raised for every menu by <c>GameMenu.RunOnInit</c>) stamps at once, unthrottled. The tick stamp covers waiting and
/// sieges, throttled in real time (campaign <c>dt</c> is a quarter of real <c>dt</c> at play speed). Built with
/// <c>new</c> by <see cref="HiddenHostsModule"/> for every campaign; it holds no campaign state. With the toggle off nothing
/// is stamped. A fault is logged once per kind and the next trigger tries again.
/// </summary>
internal sealed class HiddenHostsPlainSightBehavior : CampaignBehaviorBase
{
    /// <summary>Plain sight changes at the speed of a player's actions; a stamp every half second is more than enough.</summary>
    private static readonly long IntervalTicks = Stopwatch.Frequency / 2;

    private readonly HiddenHostsService _service;
    private readonly IPartyIntelAdapter _adapter;
    private readonly IModLogger _logger;
    private readonly Func<long> _clock;
    private readonly List<PartyIntelSnapshot> _scratch = new();
    private readonly HashSet<string> _reported = new();
    private long _lastStamp;
    private bool _hasStamped;

    public HiddenHostsPlainSightBehavior(HiddenHostsService service, IPartyIntelAdapter adapter, IModLogger logger,
        Func<long>? clock = null)
    {
        _service = service;
        _adapter = adapter;
        _logger = logger;
        _clock = clock ?? Stopwatch.GetTimestamp;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        CampaignEvents.GameMenuOpened.AddNonSerializedListener(this, OnMenuOpened);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    /// <summary>The frame time is the campaign's, which a stopped or slowed game shrinks: the throttle reads the real clock instead.</summary>
    internal void OnTick(float dt)
    {
        long now = _clock();
        if (_hasStamped && now - _lastStamp < IntervalTicks) return;
        Stamp(now);
    }

    internal void OnMenuOpened(MenuCallbackArgs args) => Stamp(_clock());

    private void Stamp(long now)
    {
        _lastStamp = now;
        _hasStamped = true;
        try
        {
            if (!_service.Enabled) return;
            var settlement = _adapter.CollectPlainSight(_scratch);
            for (int i = 0; i < _scratch.Count; i++)
                _service.Stamp(_scratch[i]);
            _service.StampSettlement(settlement);
        }
        catch (Exception ex)
        {
            if (_reported.Add(ex.GetType().Name))
                _logger.LogError($"[HiddenHosts] plain-sight stamp failed, carrying on: {ex}");
        }
        finally
        {
            _scratch.Clear();
        }
    }
}
