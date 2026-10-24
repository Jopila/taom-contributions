using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Equipment;
using TAOM.Features.Enlistment.Presentation;
using System;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment;

/// <summary>Remembers past service (#837): writes an entry when a term ends, and restores what is owed at the next oath.</summary>
public interface IServiceHistoryService
{
    /// <summary>
    /// Called at the start of a discharge, while the commander and the enlisted hero are still known:
    /// remembers them until <see cref="RecordEnd"/>.
    /// </summary>
    void NoteCommander(string commanderHeroId, string enlistedHeroId, CommanderSnapshot snapshot);

    /// <summary>
    /// Writes the entry for the term that just ended, with the rank and the kit tier that stayed with him
    /// (null when none did). The one owner of which reasons are quiet: heir succession and save
    /// normalization write nothing. Host only (the caller already is).
    /// </summary>
    void RecordEnd(DischargeReason reason, ServiceRank rank, EnlistmentKitTier? kitTier);

    /// <summary>True while a desertion by the current main hero from this commander is still inside the cooldown.</summary>
    bool IsInDesertionCooldown(string commanderHeroId);

    /// <summary>Applies the rank this oath is owed to the hero who swears it (and the kit tier he kept) and says so.</summary>
    void RestoreOnOath(string commanderHeroId, string enlistedHeroId, CommanderSnapshot snapshot);
}

/// <inheritdoc />
public sealed class ServiceHistoryService : IServiceHistoryService
{
    private readonly IEnlistmentContentStore _content;
    private readonly IEnlistmentContentConfigProvider _config;
    private readonly IEquipmentIssueLedger _ledger;
    private readonly IServiceDailyAnnouncer _announcer;
    private readonly ICampaignClockAdapter _clock;
    private readonly IPlayerPartyAdapter _playerParty;
    private readonly IModLogger _logger;

    private string _pendingCommander;
    private string _pendingHero;
    private string _pendingClan;
    private string _pendingKingdom;

    public ServiceHistoryService(
        IEnlistmentContentStore content,
        IEnlistmentContentConfigProvider config,
        IEquipmentIssueLedger ledger,
        IServiceDailyAnnouncer announcer,
        ICampaignClockAdapter clock,
        IPlayerPartyAdapter playerParty,
        IModLogger logger)
    {
        _content = content;
        _config = config;
        _ledger = ledger;
        _announcer = announcer;
        _clock = clock;
        _playerParty = playerParty;
        _logger = logger;
    }

    public void NoteCommander(string commanderHeroId, string enlistedHeroId, CommanderSnapshot snapshot)
    {
        _pendingCommander = commanderHeroId;
        _pendingHero = enlistedHeroId;
        _pendingClan = snapshot?.ClanId ?? "";
        _pendingKingdom = KingdomOf(snapshot);
    }

    public void RecordEnd(DischargeReason reason, ServiceRank rank, EnlistmentKitTier? kitTier)
    {
        if (reason == DischargeReason.HeirSuccessionOrPossessionMismatch || reason == DischargeReason.SaveNormalization)
        {
            Forget();
            return;
        }
        if (string.IsNullOrEmpty(_pendingCommander) || string.IsNullOrEmpty(_pendingHero))
        {
            _logger?.LogInfo($"[Enlistment] no past-service entry for {reason}: the commander or the hero was not noted");
            Forget();
            return;
        }

        var config = _config.GetConfig();
        var now = _clock.NowDays;
        var history = _content.History;
        history.Entries.Add(new ServiceHistoryEntry(_pendingCommander, _pendingClan, _pendingKingdom, rank, reason, now, kitTier, _pendingHero));
        history.Prune(now, config.ServiceHistoryExpiryDays, config.ServiceHistoryMaxEntries, config.DesertionCooldownDays);
        _logger?.LogInfo($"[Enlistment] past service recorded: {_pendingCommander}, rank {rank}, {reason}, kit tier {kitTier?.ToString() ?? "none"}");
        Forget();
    }

    public bool IsInDesertionCooldown(string commanderHeroId) =>
        ReenlistmentPolicy.InDesertionCooldown(
            _content.History.Entries, commanderHeroId, _clock.NowDays, _config.GetConfig().DesertionCooldownDays,
            _playerParty.GetMainHeroId());

    public void RestoreOnOath(string commanderHeroId, string enlistedHeroId, CommanderSnapshot snapshot)
    {
        var config = _config.GetConfig();
        var result = ReenlistmentPolicy.Restore(
            _content.History.Entries, _clock.NowDays, config.ServiceHistoryExpiryDays,
            commanderHeroId, snapshot?.ClanId, KingdomOf(snapshot), enlistedHeroId);
        var record = _content.Record;
        // Only rank: service XP and days served restart, so the next promotion's gates count from this oath.
        if (!result.Restored || (int)result.Rank <= (int)record.Rank)
            return;

        record.Rank = result.Rank;
        // The ledger is seeded with the tier he really kept, and only then; after a full reclaim it stays
        // empty so the veteran can draw kit for his rank again.
        if (result.KitTier.HasValue)
            _ledger.RecordIssue(result.KitTier.Value, Array.Empty<string>());
        _logger?.LogInfo($"[Enlistment] past service restored: begins as {result.Rank} (kit tier {result.KitTier?.ToString() ?? "none"})");
        _announcer.AnnounceRemembered(snapshot?.Name ?? "", result.Rank);
    }

    private void Forget()
    {
        _pendingCommander = null;
        _pendingHero = null;
        _pendingClan = null;
        _pendingKingdom = null;
    }

    /// <summary>The commander's kingdom id: his faction when it is not his own clan, otherwise empty.</summary>
    private static string KingdomOf(CommanderSnapshot snapshot) =>
        snapshot == null || string.IsNullOrEmpty(snapshot.FactionId) || snapshot.FactionId == snapshot.ClanId
            ? ""
            : snapshot.FactionId;
}
