using TAOM.Features.Execution;

namespace TAOM.Features.FromTheAshes;

/// <summary>
/// The engine facts the rules read, taken once from the settlement the main party is inside. Engine-free, so the service
/// is testable without a campaign. For a rebuild the site is the looted village; for project help it is the fief.
/// </summary>
public sealed class AshesSiteSnapshot
{
    public string SettlementId { get; set; } = "";

    public string SettlementName { get; set; } = "";

    public bool IsVillage { get; set; }

    public bool IsFortification { get; set; }

    /// <summary>The village is in the Looted state.</summary>
    public bool IsLooted { get; set; }

    /// <summary>The player's clan owns the site: the village for a rebuild, the fortification for project help.</summary>
    public bool PlayerClanOwns { get; set; }

    /// <summary>The fief's owner is unassigned (vanilla's stash gate refuses it too).</summary>
    public bool OwnerUnassigned { get; set; }

    /// <summary>The site's map faction is the player's own (an independent player's is his clan).</summary>
    public bool SameMapFactionAsPlayer { get; set; }

    /// <summary>The site is under siege or being raided.</summary>
    public bool UnderSiegeOrRaid { get; set; }

    /// <summary>The fief's build queue holds a project the company can still advance (head below level 3).</summary>
    public bool HasProjectInProgress { get; set; }

    /// <summary>The name of that project, for the status line.</summary>
    public string ProjectName { get; set; } = "";

    public int HealthyTroops { get; set; }

    /// <summary>Regular captives in the prison roster that are not wounded: the thralls' labour. Heroes never count.</summary>
    public int Prisoners { get; set; }

    /// <summary>
    /// The player's clan map faction (the kingdom, or the clan itself when it serves none) with that faction's own
    /// culture (#744, #957). The service resolves the side from this pair.
    /// </summary>
    public SideIds PlayerSide { get; set; } = SideIds.None;

    /// <summary>The main party follows another lord's army.</summary>
    public bool InForeignArmy { get; set; }

    public bool MainPartyInMapEvent { get; set; }
}
