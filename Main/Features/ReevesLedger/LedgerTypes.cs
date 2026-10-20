using System;
using System.Collections.Generic;

namespace TAOM.Features.ReevesLedger;

/// <summary>Which way a reserve step moves gold: from the player's purse into the reserve, or back out of it.</summary>
public enum ReserveDirection
{
    Add,
    Withdraw,
}

/// <summary>The kinds of line a fief's profit tooltip can carry, in the order the tooltip lists them.</summary>
public enum ProfitLineKind
{
    SettlementTax,
    Tariffs,
    Projects,
    Village,
    GarrisonWages,
}

/// <summary>One bound village's income to its town's owner.</summary>
public readonly struct VillageIncome
{
    public VillageIncome(string name, int amount)
    {
        Name = name;
        Amount = amount;
    }

    public string Name { get; }

    public int Amount { get; }
}

/// <summary>The daily numbers of one fief as the clan finance model reports them, in whole gold.</summary>
public sealed class ProfitInputs
{
    public int SettlementTax { get; set; }

    public int Tariffs { get; set; }

    public int Projects { get; set; }

    public IReadOnlyList<VillageIncome> Villages { get; set; } = Array.Empty<VillageIncome>();

    /// <summary>False when the fief has no active garrison party; <see cref="GarrisonWage"/> is then ignored.</summary>
    public bool HasGarrison { get; set; }

    /// <summary>The garrison's daily wage as a positive cost.</summary>
    public int GarrisonWage { get; set; }
}

/// <summary>One line of the profit tooltip. <see cref="Name"/> is set for a village; an expense line carries a negative amount.</summary>
public readonly struct ProfitLine
{
    public ProfitLine(ProfitLineKind kind, string name, int amount)
    {
        Kind = kind;
        Name = name;
        Amount = amount;
    }

    public ProfitLineKind Kind { get; }

    public string Name { get; }

    public int Amount { get; }
}

public sealed class ProfitBreakdown
{
    public ProfitBreakdown(IReadOnlyList<ProfitLine> lines, int total)
    {
        Lines = lines;
        Total = total;
    }

    public IReadOnlyList<ProfitLine> Lines { get; }

    /// <summary>The signed sum of every line. A fief that costs more than it earns reports a negative total.</summary>
    public int Total { get; }
}

/// <summary>What one press of +10K or -10K does: <see cref="Amount"/> gold moves, and the reserve ends at <see cref="Target"/>.</summary>
public readonly struct ReserveStep
{
    public ReserveStep(int target, int amount)
    {
        Target = target;
        Amount = amount;
    }

    public int Target { get; }

    /// <summary>Gold that moves; zero means the button does nothing.</summary>
    public int Amount { get; }

    public bool HasStep => Amount > 0;
}

public readonly struct StashEntry
{
    public StashEntry(string name, int count)
    {
        Name = name;
        Count = count;
    }

    public string Name { get; }

    public int Count { get; }
}

public readonly struct PrisonerEntry
{
    public PrisonerEntry(string name, int count, bool isHero)
    {
        Name = name;
        Count = count;
        IsHero = isHero;
    }

    public string Name { get; }

    public int Count { get; }

    public bool IsHero { get; }
}

/// <summary>The stash tooltip's content: the lines to show and how many entries were left off.</summary>
public sealed class StashSummary
{
    public StashSummary(IReadOnlyList<StashEntry> lines, int moreCount)
    {
        Lines = lines;
        MoreCount = moreCount;
    }

    public IReadOnlyList<StashEntry> Lines { get; }

    /// <summary>Entries beyond the cap, for the "and N more" line; zero when everything fits.</summary>
    public int MoreCount { get; }

    public bool IsEmpty => Lines.Count == 0 && MoreCount == 0;
}

/// <summary>The prison tooltip's content: the lines to show, how many entries were left off, and the head count.</summary>
public sealed class PrisonerSummary
{
    public PrisonerSummary(IReadOnlyList<PrisonerEntry> lines, int moreCount, int total)
    {
        Lines = lines;
        MoreCount = moreCount;
        Total = total;
    }

    public IReadOnlyList<PrisonerEntry> Lines { get; }

    public int MoreCount { get; }

    /// <summary>Every prisoner held, shown or not.</summary>
    public int Total { get; }

    public bool IsEmpty => Lines.Count == 0 && MoreCount == 0;
}
