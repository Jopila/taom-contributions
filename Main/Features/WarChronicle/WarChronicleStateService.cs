using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Events;
using TAOM.Features.WarChronicle.Rally;
using TAOM.Features.WarOfTheRingMomentum;

namespace TAOM.Features.WarChronicle;

/// <summary>
/// The War Chronicle's per-campaign state as one unit: it assembles the save payload from the effect
/// registry, the baselines and the rally tiers, hands it to the behavior in chunks, and restores each
/// section from a load, the chronicle's event rows (<see cref="ChronicleStateStore"/>, #765 M3) included.
/// It resets every registry for a new session.
///
/// The chunks come from <see cref="MomentumSyncChunker"/>: one SyncData string over 32,767 UTF-8
/// bytes corrupts the save at write time, and a developed campaign's effects and baselines can grow
/// past that.
/// </summary>
public sealed class WarChronicleStateService
{
    private readonly IWarEffectService _effects;
    private readonly WarBaselineService _baselines;
    private readonly RallyTierStore _rallyTiers;
    private readonly IModLogger _logger;

    // #765 (M3): the chronicle section's owner.
    private readonly ChronicleStateStore _chronicle;

    public WarChronicleStateService(
        IWarEffectService effects, WarBaselineService baselines, RallyTierStore rallyTiers, IModLogger logger,
        ChronicleStateStore chronicle)
    {
        _effects = effects;
        _baselines = baselines;
        _rallyTiers = rallyTiers;
        _logger = logger;
        _chronicle = chronicle;
    }

    /// <summary>The payload split into chunks that each stay far under the engine's per-entry limit.</summary>
    public IReadOnlyList<string> CaptureChunks()
    {
        var payload = new WarChroniclePayload
        {
        };
        payload.Effects.AddRange(_effects.Snapshot());
        payload.Chronicle.AddRange(_chronicle.Snapshot());
        foreach (var pair in _chronicle.PhaseSeenSnapshot())
            payload.PhaseSeenDays[pair.Key] = pair.Value;
        foreach (var pair in _baselines.Snapshot())
            payload.Baselines[pair.Key] = pair.Value;
        foreach (var pair in _rallyTiers.Snapshot())
            payload.RallyTiers[pair.Key] = pair.Value;

        return MomentumSyncChunker.Split(WarChronicleSaveCodec.Serialize(payload));
    }

    /// <summary>
    /// Restores every section from the saved chunks. An empty list means a corrupt or out-of-range record:
    /// a save from before this feature never reaches SyncData (CampaignBehaviorDataStore.LoadBehaviorData).
    /// </summary>
    public void RestoreFromChunks(IReadOnlyList<string> chunks)
    {
        ResetForNewSession();

        var payload = WarChronicleSaveCodec.Parse(MomentumSyncChunker.Join(chunks));
        foreach (var warning in payload.Warnings)
            _logger.LogWarning("[WarChronicle] Save: " + warning + ".");

        _effects.RestoreFromSave(payload.Effects);
        _baselines.RestoreFromSave(payload.Baselines);
        _rallyTiers.Restore(payload.RallyTiers);
        _chronicle.Restore(payload.Chronicle);
        _chronicle.RestorePhaseSeenDays(payload.PhaseSeenDays);
    }

    /// <summary>Empties every registry. Called from the campaign behavior's constructor (the session-reset rule).</summary>
    public void ResetForNewSession()
    {
        _effects.ResetForNewSession();
        _baselines.ResetForNewSession();
        _rallyTiers.ResetForNewSession();
        _chronicle.ResetForNewSession();
    }
}
