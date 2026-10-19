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

    /// <summary>The player's kingdom id, or "" when his clan is in none. The service resolves his side from this pair.</summary>
    public string PlayerKingdomId { get; set; } = "";

    /// <summary>The culture of that kingdom, the same entity as <see cref="PlayerKingdomId"/> (#744); "" without a kingdom.</summary>
    public string PlayerKingdomCultureId { get; set; } = "";

    /// <summary>The player hero's own culture, used only when he has no kingdom.</summary>
    public string PlayerCultureId { get; set; } = "";

    /// <summary>The main party follows another lord's army.</summary>
    public bool InForeignArmy { get; set; }

    public bool MainPartyInMapEvent { get; set; }
}
