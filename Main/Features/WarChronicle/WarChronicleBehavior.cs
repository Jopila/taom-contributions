using TAOM.Core.Logging;
using TAOM.Core.Persistence;
using TAOM.Features.CoopInterop;
using TAOM.Features.WarChronicle.Ledger;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.WarChronicle;

/// <summary>
/// Thin entry point for the War Chronicle (docs/features/war-chronicle.md, issue #765): the daily tick,
/// the kingdom-destroyed and game-loaded ledger lines and the SyncData halves. Always registered, so the save payload
/// survives a toggle. Every singleton holding campaign state is reset in the CONSTRUCTOR: the module
/// builds this behavior fresh in each campaign's OnGameStart (WarChronicleModule, pinned by
/// WarChronicleWiringTests), before any campaign event and before a load's SyncData, which then
/// restores the saved state over the reset. A save without a record stays reset. No latch.
/// </summary>
public class WarChronicleBehavior : CampaignBehaviorBase
{
    private const string CountKey = "_taom_war_chronicle_v1_count";
    private const string ChunkKeyPrefix = "_taom_war_chronicle_v1_";

    private readonly WarChronicleTickService _tick;
    private readonly WarChronicleStateService _state;
    private readonly WarLedgerService _ledger;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;

    public WarChronicleBehavior(
        WarChronicleTickService tick, WarChronicleStateService state, WarLedgerService ledger,
        ICoopSessionProvider coop, IModLogger logger)
    {
        _tick = tick;
        _state = state;
        _ledger = ledger;
        _coop = coop;
        _logger = logger;

        _state.ResetForNewSession();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, OnKingdomDestroyed);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, _ => HandleGameLoaded());
    }

    public override void SyncData(IDataStore dataStore)
    {
        var saved = SyncDataChunker.SyncChunks(
            dataStore, CountKey, ChunkKeyPrefix, dataStore.IsSaving ? _state.CaptureChunks() : null, _logger, "[WarChronicle]");
        if (dataStore.IsLoading)
            _state.RestoreFromChunks(saved);
    }

    // internal for TAOM.Tests (InternalsVisibleTo): the co-op gate is asserted directly.
    internal void OnDailyTick()
    {
        // World work and save-backed writes (effects, baselines) are the authority's: a co-op client
        // takes the host's state through the save, and a second writer would diverge it.
        if (!_coop.IsAuthority || !CoopSessionPolicy.MayWriteSaveBackedState(_coop.IsCoopClient))
            return;

        _tick.RunDaily();
    }

    // The ledger's load marker: the next daily tick can repeat the save's own day, so only this line tells
    // the analyzer which later days belong to the run the player abandoned.
    internal void HandleGameLoaded()
    {
        if (!_coop.IsAuthority)
            return;

        _ledger.LogLoad();
    }

    private void OnKingdomDestroyed(Kingdom kingdom) => HandleKingdomDestroyed(kingdom?.StringId);

    internal void HandleKingdomDestroyed(string? kingdomId)
    {
        if (!_coop.IsAuthority)
            return;

        _ledger.LogDestroyed(kingdomId);
    }
}
