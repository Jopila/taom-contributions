using System;
using System.Collections.Generic;

namespace TAOM.Features.FromTheAshes;

/// <summary>Which table of numbers a job uses. Thralls is the evil player's rebuild, worked by captives.</summary>
public enum AshesVariant
{
    Free,
    Thralls,
    Project,
}

/// <summary>
/// Why a job cannot start, or why a running one must end. The menu hides the option for <see cref="Disabled"/>,
/// <see cref="NotApplicable"/>, <see cref="NotLooted"/>, <see cref="NotOwner"/> and <see cref="Busy"/>, and shows a disabled option
/// with a tooltip for the rest (<c>FromTheAshesTexts.IsHidden</c> is the one list).
/// </summary>
public enum AshesBlock
{
    None,
    Disabled,
    NotSinglePlayer,
    NotApplicable,
    NotLooted,
    NotYours,
    NotOwner,
    NoProject,
    Enlisted,
    ForeignArmy,
    Busy,
    NeedLabour,
    NeedMaterial,
}

/// <summary>What a running job should do next, by the rules in the design's state matrix.</summary>
public enum AshesVerdict
{
    Continue,
    Complete,
    Cancel,
    CancelRecovered,
}

/// <summary>How a call into the job left it.</summary>
public enum AshesOutcome
{
    /// <summary>There was no job.</summary>
    None,
    Running,
    Completed,
    Stopped,

    /// <summary>The village recovered by itself.</summary>
    CancelledRecovered,
    Cancelled,

    /// <summary>The materials were gone at completion: nothing was taken and nothing was rebuilt.</summary>
    ShortOfMaterials,
}

/// <summary>An item the job needs, by item id.</summary>
public readonly struct AshesMaterial
{
    public AshesMaterial(string itemId, int count)
    {
        ItemId = itemId;
        Count = count;
    }

    public string ItemId { get; }

    public int Count { get; }
}

public sealed class AshesStartCheck
{
    public AshesBlock Block { get; set; }

    public AshesVariant Variant { get; set; }

    /// <summary>For <see cref="AshesBlock.NeedLabour"/>: the number of people needed.</summary>
    public int Count { get; set; }

    /// <summary>For <see cref="AshesBlock.NeedMaterial"/>: every item the party and the stash cannot cover, with the full amount needed.</summary>
    public IReadOnlyList<AshesMaterial> Missing { get; set; } = Array.Empty<AshesMaterial>();

    public bool Allowed => Block == AshesBlock.None;
}

public sealed class AshesCost
{
    public AshesVariant Variant { get; set; }

    public float Hours { get; set; }

    public int Labour { get; set; }

    /// <summary>The labour is counted from the prison roster, not the troop roster.</summary>
    public bool LabourFromPrisoners { get; set; }

    public IReadOnlyList<AshesMaterial> Materials { get; set; } = Array.Empty<AshesMaterial>();

    /// <summary>Project help pays its materials when the work starts; a rebuild pays them at completion.</summary>
    public bool PayAtStart { get; set; }

    public float ConstructionPerHour { get; set; }

    public int StopPenalty { get; set; }
}

public sealed class AshesRewards
{
    public int NotableRelation { get; set; }

    public int OwnerRelation { get; set; }

    public int AthleticsXp { get; set; }
}

/// <summary>What the menu needs after a call: the outcome and the words for the message and status line.</summary>
public sealed class AshesResult
{
    public AshesOutcome Outcome { get; set; }

    public AshesJobKind Kind { get; set; }

    public AshesVariant Variant { get; set; }

    public string SettlementName { get; set; } = "";

    public string ProjectName { get; set; } = "";

    /// <summary>0 to 1; set while <see cref="AshesOutcome.Running"/>.</summary>
    public float Progress { get; set; }

    /// <summary>The job's whole length in hours; set while <see cref="AshesOutcome.Running"/>.</summary>
    public float TotalHours { get; set; }

    /// <summary>For <see cref="AshesOutcome.ShortOfMaterials"/>.</summary>
    public IReadOnlyList<AshesMaterial> Missing { get; set; } = Array.Empty<AshesMaterial>();

    public static AshesResult Of(AshesOutcome outcome) => new() { Outcome = outcome };
}
