using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.SettlementServices;

/// <summary>
/// The rules of Tidings of the Lists (#859, docs/features/settlement-services.md): for a small fee the arena master names the
/// nearest towns holding a tournament now that are not at war with the player's realm. The adapter only reads the survey; the
/// filter, the order and the fee are decided here. Nothing is marked on the map and no settlement state changes. The caller (the
/// menu behavior) catches what escapes.
/// </summary>
public sealed class TidingsService
{
    public const int Fee = 20;
    public const int MaxListed = 3;

    private readonly ISettlementServicesAdapter _adapter;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly SettlementServicesSettingsProvider _settings;
    private readonly IModLogger _logger;

    public TidingsService(ISettlementServicesAdapter adapter, ICoopSessionProvider coop, IDedicatedServerProvider dedicated,
        SettlementServicesSettingsProvider settings, IModLogger logger)
    {
        _adapter = adapter;
        _coop = coop;
        _dedicated = dedicated;
        _settings = settings;
        _logger = logger;
    }

    // The fee is the only write, so the same single-player predicate as the other world-writing services.
    private bool MayWriteWorld =>
        CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _dedicated);

    /// <summary>The option is shown while nothing blocks it or only a short purse does (then it is greyed); every other block hides it.</summary>
    public static bool IsShown(TidingsBlock block) => block == TidingsBlock.None || block == TidingsBlock.NotEnoughGold;

    /// <summary>
    /// The first reason the option is hidden or greyed, in this order: switch off, not single-player, disguised
    /// (all hidden), then a purse under the fee (greyed). Reads the engine now.
    /// </summary>
    public TidingsBlock Check()
    {
        if (!_settings.TidingsEnabled)
            return TidingsBlock.Disabled;
        if (!MayWriteWorld)
            return TidingsBlock.NotSinglePlayer;
        if (_adapter.IsPlayerDisguised)
            return TidingsBlock.Disguised;
        return _adapter.PlayerGold >= Fee ? TidingsBlock.None : TidingsBlock.NotEnoughGold;
    }

    /// <summary>
    /// Asks after the tournaments: re-runs the check, surveys the towns, and charges the fee only when there is something to
    /// name. An empty answer costs nothing. A short receipt gives the gold back and shows no list.
    /// </summary>
    public TidingsResult Ask()
    {
        var block = Check();
        if (block != TidingsBlock.None)
            return Refused(block);

        var rows = Nearest(_adapter.ReadTournamentSites(), _adapter.TournamentDistanceCutoff);
        if (rows.Count == 0)
            return Refused(TidingsBlock.NoTournaments);

        if (!PlayerPurse.TryCharge(_adapter, _logger, Fee, "Tidings of the Lists"))
            return Refused(TidingsBlock.Problem);

        return new TidingsResult(TidingsBlock.None, rows);
    }

    private static TidingsResult Refused(TidingsBlock block) => new(block, Array.Empty<TidingsRow>());

    // Not the current town, not at war, a finite distance above zero (0 is a failed path search) and below the cutoff; nearest first, then by id.
    private static List<TidingsRow> Nearest(IReadOnlyList<TournamentSiteSnapshot> sites, float cutoff) =>
        sites
            .Where(s => !s.IsCurrentTown && !s.IsAtWarWithPlayer && IsReachable(s.Distance, cutoff))
            .OrderBy(s => s.Distance)
            .ThenBy(s => s.TownId, StringComparer.Ordinal)
            .Take(MaxListed)
            .Select(s => new TidingsRow(s.TownName, s.RealmName))
            .ToList();

    private static bool IsReachable(float distance, float cutoff) =>
        FiniteFloatValidator.IsFinite(distance) && distance > 0f && distance < cutoff;
}
