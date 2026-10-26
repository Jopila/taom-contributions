namespace TAOM.Features.FieldCamp.Activities;

/// <summary>
/// What the men are set to do from a raised camp (#860, docs/features/field-camp.md "Camp work"). The integer value is what
/// <c>CampState.Activity</c> saves, so the numbers never change; 0 is the saved "nothing", which is also what a save from
/// before the feature reads as.
/// </summary>
public enum CampActivity
{
    None = 0,
    Hunt = 1,
    Timber = 2,
    Fish = 3,
}

/// <summary>Why an activity cannot start. None means it can.</summary>
public enum CampWorkBlock
{
    None = 0,

    /// <summary>A shared campaign: the work is single-player only (the Far Watch predicate).</summary>
    SharedCampaign,

    /// <summary>An encounter or a battle is on.</summary>
    Busy,

    /// <summary>No camp, a camp still being raised, or an ambush or lookout camp (those stay small and hidden).</summary>
    CampNotReady,

    /// <summary>A Far Watch ride is out; the camp's men are not at hand for both.</summary>
    ScoutRideOut,

    /// <summary>A work is already saved; the camp panel's option returns to it instead of starting another.</summary>
    AlreadyWorking,

    /// <summary>This ground offers nothing to this work (no game, no trees, no water).</summary>
    NothingHere,

    /// <summary>No healthy man to send.</summary>
    NoMen,

    /// <summary>More than ten men need tools, and the party holds too few.</summary>
    NoTools,

    /// <summary>The Field Camps master toggle is off: no work starts, and a saved one ends the way a Far Watch ride does.</summary>
    FeatureDisabled,
}

/// <summary>The answer the menus draw from: can this activity start, and if not, why.</summary>
public readonly struct CampWorkOffer
{
    public CampWorkOffer(CampWorkBlock block, int toolsNeeded = 0)
    {
        Block = block;
        ToolsNeeded = toolsNeeded;
    }

    public CampWorkBlock Block { get; }

    /// <summary>Tools the work would use up at its end (0 for ten men or fewer).</summary>
    public int ToolsNeeded { get; }

    public bool Ready => Block == CampWorkBlock.None;
}

/// <summary>How far along the saved work is. Not running covers no camp and no work alike.</summary>
public readonly struct CampWorkProgress
{
    public CampWorkProgress(CampActivity activity, float fraction, double totalHours, int injured, bool endedBySharedCampaign = false)
    {
        Activity = activity;
        Fraction = fraction;
        TotalHours = totalHours;
        Injured = injured;
        EndedBySharedCampaign = endedBySharedCampaign;
    }

    public CampActivity Activity { get; }

    public bool Running => Activity != CampActivity.None;

    /// <summary>0 to 1.</summary>
    public float Fraction { get; }

    public double TotalHours { get; }

    /// <summary>Men wounded by the roll this call made (0 on every other call).</summary>
    public int Injured { get; }

    /// <summary>
    /// This call ended the work because a shared campaign began (never set by any other end). The wait menu prints the line; the
    /// service composes no player text.
    /// </summary>
    public bool EndedBySharedCampaign { get; }
}

/// <summary>What a finished activity paid, for the message the menu shows.</summary>
public readonly struct CampWorkReport
{
    public CampWorkReport(CampActivity activity, int goods, float moraleCost, int toolsUsed)
    {
        Activity = activity;
        Goods = goods;
        MoraleCost = moraleCost;
        ToolsUsed = toolsUsed;
    }

    public CampActivity Activity { get; }
    public int Goods { get; }
    /// <summary>Morale the long work took from the party (a cost, never a gain); 0 for ten men or fewer.</summary>
    public float MoraleCost { get; }
    public int ToolsUsed { get; }
}

/// <summary>
/// The party facts a start or a payout reads, taken in one pass at the campaign boundary. Plain numbers: the service holds
/// no engine object (ADR-007).
/// </summary>
public struct CampWorkFacts
{
    /// <summary>The camp stands and its raise is done.</summary>
    public bool CampReady { get; set; }

    public int HealthyMen { get; set; }

    /// <summary>Healthy men who are not heroes: the ones an injury can fall on.</summary>
    public int HealthyRegulars { get; set; }

    public int ClanTier { get; set; }
    public int ToolsHeld { get; set; }

    /// <summary>The <c>TaleWorlds.Core.TerrainType</c> value under the party, or -1 when the map cannot say.</summary>
    public int TerrainType { get; set; }

    /// <summary>In an encounter or a map event.</summary>
    public bool Busy { get; set; }

    /// <summary>A Far Watch ride is out.</summary>
    public bool ScoutRideOut { get; set; }
}
