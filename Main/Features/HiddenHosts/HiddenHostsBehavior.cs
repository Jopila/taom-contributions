using System;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.HiddenHosts;

/// <summary>
/// Hidden Hosts' campaign entry point (#846): resets the singleton service for every campaign and shows the two one-time
/// popups, one for the count symbols and, once a remembered "~" count has been drawn, one for the memory (each under its
/// own key and flag, never on the tick the other shows). Built with <c>new</c> by <see cref="HiddenHostsModule"/> for every campaign,
/// before any event and before a load's SyncData, so its constructor is the per-campaign reset (pinned by
/// HiddenHostsWiringTests). The popup waits for a free map, for the feature to be on and for the hooks to be running (it
/// must not explain a mask that is off); the flag rides the save. A popup that throws is marked shown, so it is saved
/// and not retried in that campaign. A throw from the map test gives the hint up for this session only (not saved) and
/// logs once, for either popup.
/// </summary>
internal sealed class HiddenHostsBehavior : CampaignBehaviorBase
{
    private readonly HiddenHostsService _service;
    private readonly IHiddenHostsHintAdapter _hint;
    private readonly IModLogger _logger;
    private bool _taomHiddenHostsHintShown;
    private bool _taomHiddenHostsMemoryHintShown;
    private bool _hintAbandoned;
    private bool _memoryHintAbandoned;

    public HiddenHostsBehavior(HiddenHostsService service, IHiddenHostsHintAdapter hint, IModLogger logger)
    {
        _service = service;
        _hint = hint;
        _logger = logger;
        _service.ResetForNewSession();
    }

    public override void RegisterEvents() => CampaignEvents.TickEvent.AddNonSerializedListener(this, OnCampaignTick);

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("_taomHiddenHostsHintShown", ref _taomHiddenHostsHintShown);
        dataStore.SyncData("_taomHiddenHostsMemoryHintShown", ref _taomHiddenHostsMemoryHintShown);
    }

    private void OnCampaignTick(float dt) => OnTick();

    internal void OnTick()
    {
        if (HiddenHostsCalls.Stopped || !_service.Enabled) return;
        if (!_taomHiddenHostsHintShown)
        {
            if (!_hintAbandoned) TryShowSymbolHint();
        }
        else if (!_taomHiddenHostsMemoryHintShown && !_memoryHintAbandoned && _service.MemoryDrawn)
        {
            TryShowMemoryHint();
        }
    }

    private void TryShowSymbolHint()
    {
        try
        {
            if (!_hint.IsMapFree()) return;
            // Set before the popup: if it throws, the hint counts as shown and is saved, never retried in this campaign.
            _taomHiddenHostsHintShown = true;
            _hint.ShowSymbolHint(HiddenHostsMath.FormatMask(100, 1), HiddenHostsMath.HorizonText);
        }
        catch (Exception ex)
        {
            // Abandoning also covers a throw from IsMapFree, which nothing has marked shown: that one is session-only.
            _hintAbandoned = true;
            _logger.LogError($"[HiddenHosts] symbol hint failed, given up for this session: {ex}");
        }
    }

    private void TryShowMemoryHint()
    {
        try
        {
            if (!_hint.IsMapFree()) return;
            _taomHiddenHostsMemoryHintShown = true;
            _hint.ShowMemoryHint(HiddenHostsMath.FormatMemory(150, 1));
        }
        catch (Exception ex)
        {
            _memoryHintAbandoned = true;
            _logger.LogError($"[HiddenHosts] memory hint failed, given up for this session: {ex}");
        }
    }
}
