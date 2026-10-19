namespace TAOM.Features.FromTheAshes;

/// <summary>The two jobs the player's company can take on. The numeric values are saved (<c>_taomAshesKind</c>): never renumber.</summary>
public enum AshesJobKind
{
    /// <summary>Raise a looted village back to its normal state.</summary>
    VillageRebuild = 0,

    /// <summary>Lend the company's labour to the head of a fief's build queue.</summary>
    ProjectHelp = 1,
}

/// <summary>
/// One job in progress. Progress is never stored: <see cref="StartHours"/> and <see cref="TotalHours"/> derive it, so a
/// mid-job save resumes exactly. Only the main party works, so the store holds at most one.
/// </summary>
public sealed class AshesJob
{
    /// <summary>The longest a job may run, in campaign hours: the config's cap, and the bound a saved record is held to.</summary>
    public const float MaxHours = 168f;

    public AshesJobKind Kind { get; set; }

    /// <summary>The village (rebuild) or the fief (project help) the company works at.</summary>
    public string SettlementId { get; set; } = "";

    /// <summary>Campaign time of the start, in hours.</summary>
    public double StartHours { get; set; }

    public float TotalHours { get; set; }

    /// <summary>Whole hours already paid out as construction points (project help); stays 0 for a rebuild.</summary>
    public int HoursCredited { get; set; }

    /// <summary>
    /// The last non-empty project name seen at the fief. Kept in memory only (never saved): a queue that empties between two
    /// looks leaves the site without a name, and the end message still has to say which building the company worked on.
    /// </summary>
    public string LastProjectName { get; set; } = "";
}
