using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.ReevesLedger;

/// <summary>
/// The rules of the Reeve's Ledger (docs/features/reeves-ledger.md), as plain numbers in and plain numbers out: the
/// profit lines of a fief, what a +10K or -10K press moves, the capped stash and prison lists, and whether a fief gets the
/// Patrol row. It reads nothing
/// from the engine, so it takes no adapter; the engine side is <c>IFiefLedgerAdapter</c>, and the step buttons move gold
/// through the reserve view model's own confirm.
/// The one live read is <see cref="MayWriteWorld"/>, which asks the co-op providers each time they are used.
/// </summary>
public sealed class ReevesLedgerService
{
    /// <summary>The most gold one press moves, the same as the vanilla reserve popup's one-time maximum.</summary>
    public const int StepSize = 10000;

    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;

    public ReevesLedgerService(ICoopSessionProvider coop, IDedicatedServerProvider dedicated)
    {
        _coop = coop;
        _dedicated = dedicated;
    }

    /// <summary>
    /// Both step buttons write the world (the player's gold and the town's reserve). They run in single-player only: no
    /// live co-op session, no co-op mod TAOM cannot probe, no dedicated server, and a peer that may write the world.
    /// Read live on every use (ICoopSessionProvider's contract).
    /// </summary>
    public bool MayWriteWorld =>
        !_coop.IsSessionActive && !_coop.ShouldDeferToHost && !_dedicated.IsDedicatedServer && _coop.IsAuthority;

    /// <summary>
    /// The profit tooltip: settlement tax, tariffs, projects, each bound village, then the garrison wage as a negative
    /// line when the fief has a garrison. The total is the signed sum of the lines, saturating at the int range.
    /// </summary>
    public ProfitBreakdown ComposeProfit(ProfitInputs inputs)
    {
        var lines = new List<ProfitLine>(5 + inputs.Villages.Count)
        {
            new(ProfitLineKind.SettlementTax, "", inputs.SettlementTax),
            new(ProfitLineKind.Tariffs, "", inputs.Tariffs),
            new(ProfitLineKind.Projects, "", inputs.Projects),
        };
        foreach (var village in inputs.Villages)
            lines.Add(new ProfitLine(ProfitLineKind.Village, village.Name, village.Amount));
        if (inputs.HasGarrison)
            lines.Add(new ProfitLine(ProfitLineKind.GarrisonWages, "", -inputs.GarrisonWage));

        long total = 0;
        foreach (var line in lines)
            total += line.Amount;
        return new ProfitBreakdown(lines, (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, total)));
    }

    /// <summary>
    /// What one press does to a reserve of <paramref name="reserve"/> with <paramref name="gold"/> in the purse. Add moves
    /// the smaller of 10,000, the gold and the room left below int.MaxValue; Withdraw moves the smaller of 10,000 and the
    /// reserve. A step of zero means the button is disabled; the target is then the reserve unchanged.
    /// </summary>
    public ReserveStep PlanReserveStep(int reserve, int gold, ReserveDirection direction)
    {
        if (direction == ReserveDirection.Add)
        {
            long room = (long)int.MaxValue - reserve;
            var amount = (int)Math.Max(0, Math.Min(Math.Min(StepSize, gold), room));
            return new ReserveStep(reserve + amount, amount);
        }

        var taken = Math.Max(0, Math.Min(StepSize, reserve));
        return new ReserveStep(reserve - taken, taken);
    }

    /// <summary>
    /// Whether the town management window gets the Patrol row (#992): every town, and a castle that has a live patrol or that
    /// the campaign's patrol model allows one (a built Guard House). A castle that cannot have a patrol gets no row, since
    /// the engine's only answer there would be "No Guard House". A village never has the window.
    /// </summary>
    public bool ShowsPatrolRow(bool isTown, bool isCastle, bool patrolAlive, bool modelAllows) =>
        isTown || (isCastle && (patrolAlive || modelAllows));

    /// <summary>The stash tooltip's lines: biggest stack first, ties by name, at most <paramref name="maxLines"/> shown.</summary>
    public StashSummary SummarizeStash(IReadOnlyList<StashEntry> entries, int maxLines)
    {
        var stored = entries.Where(e => e.Count > 0)
            .OrderByDescending(e => e.Count).ThenBy(e => e.Name, StringComparer.Ordinal)
            .ToList();
        var shown = Math.Min(stored.Count, Math.Max(0, maxLines));
        return new StashSummary(stored.GetRange(0, shown), stored.Count - shown);
    }

    /// <summary>The prison tooltip's lines: heroes first, then regulars, each group biggest first and then by name.</summary>
    public PrisonerSummary SummarizePrisoners(IReadOnlyList<PrisonerEntry> entries, int maxLines)
    {
        var held = entries.Where(e => e.Count > 0)
            .OrderByDescending(e => e.IsHero).ThenByDescending(e => e.Count).ThenBy(e => e.Name, StringComparer.Ordinal)
            .ToList();
        var shown = Math.Min(held.Count, Math.Max(0, maxLines));
        return new PrisonerSummary(held.GetRange(0, shown), held.Count - shown, held.Sum(e => e.Count));
    }
}
