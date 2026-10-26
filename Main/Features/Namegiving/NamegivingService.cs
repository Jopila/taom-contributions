using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;

namespace TAOM.Features.Namegiving;

/// <summary>
/// The rules of Namegiving (docs/features/namegiving.md): who may rename what and at what price, which typed names are
/// acceptable, and which lore name a settlement takes when it changes hands. It reads and writes the engine only through
/// <see cref="IFiefNamingAdapter"/>. Every public method that runs from an engine callback (the two reconcile passes) fails
/// open: an exception out of a load handler loops the loading screen, so it is logged and swallowed here.
/// </summary>
public sealed class NamegivingService
{
    public const int MaxNameLength = 50;
    public const int TownCost = 2000;
    public const int CastleCost = 1000;
    public const int VillageCost = 500;

    private readonly IFiefNamingAdapter _fiefs;
    private readonly IAlignmentService _alignment;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly NamegivingSettingsProvider _settings;
    private readonly IModLogger _logger;

    public NamegivingService(IFiefNamingAdapter fiefs, IAlignmentService alignment, ICoopSessionProvider coop,
        IDedicatedServerProvider dedicated, NamegivingSettingsProvider settings, IModLogger logger)
    {
        _fiefs = fiefs;
        _alignment = alignment;
        _coop = coop;
        _dedicated = dedicated;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>The MCM switch, which covers the button and the lore names.</summary>
    public bool Enabled => _settings.Enabled;

    /// <summary>
    /// Both halves write the world (a settlement's name and the player's influence). They run in single-player only: no live
    /// co-op session, no co-op mod TAOM cannot probe, no dedicated server, and a peer that may write the world. Read live on
    /// every use (ICoopSessionProvider's contract).
    /// </summary>
    public bool MayWriteWorld =>
        CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _dedicated);

    public static int CostFor(FiefKind kind) => kind switch
    {
        FiefKind.Town => TownCost,
        FiefKind.Castle => CastleCost,
        _ => VillageCost,
    };

    // --- Name rules -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The first rule a typed name breaks, in the order the player should fix them. A name is 1 to 50 characters of letters,
    /// digits, single spaces and punctuation; braces and square brackets are refused because a name goes into text that
    /// reads them as markup (the engine's own check allows them). It must differ from the current name and from every name in
    /// <paramref name="otherNames"/> (case-insensitive), which the caller builds once per prompt without the target itself.
    /// </summary>
    public static NameProblem ValidateName(string? candidate, string currentName, IEnumerable<string> otherNames)
    {
        if (string.IsNullOrEmpty(candidate) || candidate!.Length > MaxNameLength)
            return NameProblem.Length;
        if (candidate.IndexOfAny(Brackets) >= 0)
            return NameProblem.Brackets;
        foreach (var c in candidate)
            if (!(char.IsLetter(c) || char.IsDigit(c) || c == ' ' || char.IsPunctuation(c)))
                return NameProblem.Characters;
        if (candidate[0] == ' ' || candidate[candidate.Length - 1] == ' ')
            return NameProblem.Edge;
        if (candidate.Contains("  "))
            return NameProblem.Double;
        if (string.Equals(candidate, currentName, StringComparison.Ordinal))
            return NameProblem.Same;
        foreach (var other in otherNames)
            if (string.Equals(candidate, other, StringComparison.InvariantCultureIgnoreCase))
                return NameProblem.Taken;
        return NameProblem.None;
    }

    private static readonly char[] Brackets = { '{', '}', '[', ']' };

    // --- Gates ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Whether the player may rename <paramref name="target"/> now, and if not, the first reason. Run again when the name is
    /// applied, because the prompts take time and the fief, the purse or the ruler can change between them.
    /// <paramref name="detail"/> is the engine's own reason when the block is <see cref="RenameBlock.MapAction"/>, else empty.
    /// </summary>
    public RenameBlock Check(FiefNameSnapshot target, out string detail)
    {
        var gate = Gate(out detail);
        return gate != RenameBlock.None ? gate : OwnBlock(target);
    }

    /// <summary>The same check without the engine's reason text.</summary>
    public RenameBlock Check(FiefNameSnapshot target) => Check(target, out _);

    // The gates that do not depend on the target, in order: switch, single-player, ruler, then the engine's map-action gate (the
    // annex button's: not a prisoner, not on a raft or ferry, not in a mission, an encounter, a raid, a siege or a map event).
    private RenameBlock Gate(out string detail)
    {
        detail = "";
        if (!Enabled)
            return RenameBlock.Disabled;
        if (!MayWriteWorld)
            return RenameBlock.NotSinglePlayer;
        if (!_fiefs.IsRuler)
            return RenameBlock.NotRuler;
        var reason = _fiefs.MapActionBlockedReason();
        if (reason == null)
            return RenameBlock.None;
        detail = reason;
        return RenameBlock.MapAction;
    }

    // The gates that depend on the target: the realm, then the purse.
    private RenameBlock OwnBlock(FiefNameSnapshot target)
    {
        if (!target.InPlayerRealm)
            return RenameBlock.NotRealm;
        return HasInfluence(CostFor(target.Kind)) ? RenameBlock.None : RenameBlock.ShortOfInfluence;
    }

    // Written so that a NaN purse fails: NaN >= cost is false.
    private bool HasInfluence(int cost) => _fiefs.Influence >= cost;

    /// <summary>The Rename button for the selected fief: shown to the ruler in single-player, enabled while some row of the picker is.</summary>
    public RenameButtonState ButtonState(string? fiefId)
    {
        var hidden = new RenameButtonState(false, false, 0, RenameBlock.None, "");
        if (fiefId == null)
            return hidden;
        var rows = Targets(fiefId);
        if (rows.Count == 0)
            return hidden;
        var first = rows[0];
        if (first.Block == RenameBlock.Disabled || first.Block == RenameBlock.NotSinglePlayer || first.Block == RenameBlock.NotRuler)
            return hidden;
        if (rows.Any(t => t.Block == RenameBlock.None))
            return new RenameButtonState(true, true, first.Cost, RenameBlock.None, "");
        return new RenameButtonState(true, false, first.Cost, first.Block, first.Detail);
    }

    /// <summary>
    /// The picker's rows: the fief, then its villages that exist, each with its price and its own reason when it cannot be
    /// renamed. The target-independent gates (and the engine's map-action reason) are read once for the whole list.
    /// </summary>
    public IReadOnlyList<RenameTarget> Targets(string fiefId)
    {
        var rows = new List<RenameTarget>();
        var fief = _fiefs.Snapshot(fiefId);
        if (fief == null)
            return rows;
        var gate = Gate(out var detail);
        rows.Add(Row(fief, gate, detail));
        foreach (var villageId in _fiefs.VillageIds(fiefId))
        {
            var village = _fiefs.Snapshot(villageId);
            if (village != null)
                rows.Add(Row(village, gate, detail));
        }

        return rows;
    }

    private RenameTarget Row(FiefNameSnapshot snap, RenameBlock gate, string detail) =>
        new(snap.Id, snap.Name, CostFor(snap.Kind), gate != RenameBlock.None ? gate : OwnBlock(snap), detail);

    // --- Applying a name --------------------------------------------------------------------------------------------

    /// <summary>The display names of every other settlement, snapshotted once for a prompt. Duplicates stay: data names are not unique.</summary>
    public IReadOnlyList<string> OtherNames(string targetId) =>
        _fiefs.AllNames().Where(n => n.Id != targetId).Select(n => n.Name).ToList();

    /// <summary>
    /// Renames <paramref name="targetId"/> after re-running every gate and every name rule against the engine's state now.
    /// On success the adapter has written the name and then charged the influence.
    /// </summary>
    public RenameResult Apply(string targetId, string candidate)
    {
        var snap = _fiefs.Snapshot(targetId);
        if (snap == null)
            return RenameResult.Blocked(RenameBlock.NotRealm, "");
        var block = Check(snap, out var detail);
        if (block != RenameBlock.None)
            return RenameResult.Blocked(block, detail);
        var problem = ValidateName(candidate, snap.Name, OtherNames(targetId));
        if (problem != NameProblem.None)
            return RenameResult.Invalid(problem);

        var cost = CostFor(snap.Kind);
        _fiefs.ApplyPlayerName(targetId, candidate, cost);
        return RenameResult.Done();
    }

    // --- Lore names -------------------------------------------------------------------------------------------------

    /// <summary>Who named a settlement: no custom name, a TAOM lore key, or anything else (a player's name, or one TAOM did not write).</summary>
    public static NameOrigin Classify(string? customName)
    {
        if (string.IsNullOrEmpty(customName))
            return NameOrigin.None;
        return customName!.StartsWith("{=" + LoreNames.KeyPrefix, StringComparison.Ordinal) ? NameOrigin.Lore : NameOrigin.Player;
    }

    /// <summary>
    /// What to do to one lore settlement given the side that holds it now. A name a player chose is never touched. With the
    /// switch off a lore name goes back to the map's name, whoever holds the fief. Otherwise the side the name belongs to sets
    /// it, the other side takes it back off, and Neutral keeps whatever is there.
    /// </summary>
    public static LoreAction DecideLore(LoreRow row, FactionSide side, NameOrigin current, bool enabled)
    {
        if (current == NameOrigin.Player)
            return LoreAction.Keep;
        if (!enabled)
            return current == NameOrigin.Lore ? LoreAction.Restore : LoreAction.Keep;
        if (side == FactionSide.Neutral)
            return LoreAction.Keep;
        if (side == row.Side)
            return current == NameOrigin.Lore ? LoreAction.Keep : LoreAction.Set;
        return current == NameOrigin.Lore ? LoreAction.Restore : LoreAction.Keep;
    }

    /// <summary>Brings one lore settlement's name in line with its holder. Does nothing for a settlement without a lore row. Never throws.</summary>
    public void ReconcileLore(string settlementId)
    {
        try
        {
            ReconcileCore(settlementId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Namegiving] the lore name of {settlementId} could not be reconciled: {ex}");
        }
    }

    /// <summary>
    /// The session pass: every lore settlement, each in its own try, so one that throws leaves the others done. One error line
    /// for the whole pass (the first cause), because a failure that repeats for one fief repeats on every load. The outer try
    /// covers the table itself: a malformed row must not throw out of the handler.
    /// </summary>
    public void ReconcileAllLore()
    {
        try
        {
            string? firstFailure = null;
            var failed = 0;
            var rows = LoreNames.All;
            foreach (var row in rows)
            {
                try
                {
                    ReconcileCore(row.SettlementId);
                }
                catch (Exception ex)
                {
                    failed++;
                    firstFailure ??= $"{row.SettlementId}: {ex}";
                }
            }

            if (failed > 0)
                _logger.LogError($"[Namegiving] the session pass failed for {failed} of {rows.Count} lore settlements; the first was {firstFailure}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Namegiving] the session pass could not read the lore table: {ex}");
        }
    }

    private void ReconcileCore(string settlementId)
    {
        // Before any probe or read: a settlement without a lore row costs nothing, and a peer that may not write the world restores nothing.
        var row = LoreNames.Find(settlementId);
        if (row == null || !MayWriteWorld)
            return;
        var snap = _fiefs.Snapshot(settlementId);
        if (snap == null)
            return;

        var side = _alignment.ResolveSide(snap.MapFactionId, snap.CultureId);
        switch (DecideLore(row, side, Classify(snap.CustomName), Enabled))
        {
            case LoreAction.Set:
                if (OtherNames(settlementId).Any(n => string.Equals(n, row.English, StringComparison.InvariantCultureIgnoreCase)))
                    _logger.LogWarning($"[Namegiving] {settlementId} keeps its map name: another place already bears \"{row.English}\"");
                else
                    _fiefs.ApplyLore(settlementId, row.Text);
                break;
            case LoreAction.Restore:
                _fiefs.Restore(settlementId);
                break;
        }
    }
}
