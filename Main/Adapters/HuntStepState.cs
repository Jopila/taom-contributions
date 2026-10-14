namespace TAOM.Adapters;

/// <summary>
/// Raw facts for one hourly out-of-sight step of the Eye of Sauron hunter (#765 M8), read without side effects. The
/// service judges every flag (<c>EyeOfSauronService.StepBlock</c>); the adapter decides nothing.
/// </summary>
public sealed class HuntStepState
{
    public static readonly HuntStepState Missing = new();

    /// <summary>Both parties were found.</summary>
    public bool Valid { get; init; }

    public bool HunterActive { get; init; }

    public bool HunterInMapEvent { get; init; }

    /// <summary><c>BesiegerCamp != null</c>.</summary>
    public bool HunterBesieging { get; init; }

    /// <summary>In an army, or attached to another party.</summary>
    public bool HunterInArmy { get; init; }

    public bool HunterInSettlement { get; init; }

    /// <summary>At sea, or in a land-sea transition.</summary>
    public bool HunterAtSea { get; init; }

    /// <summary><c>MobileParty.IsVisible</c>: the player can see the hunter's party on the map.</summary>
    public bool HunterVisible { get; init; }

    /// <summary>Its default behaviour is to engage the player's party.</summary>
    public bool HunterEngagingPlayer { get; init; }

    /// <summary><c>MainParty.IsActive</c>; false while the player is enlisted or a prisoner.</summary>
    public bool PlayerActive { get; init; }

    /// <summary>In a map event, or besieging.</summary>
    public bool PlayerInMapEventOrSiege { get; init; }

    public bool PlayerInSettlement { get; init; }

    /// <summary>In an army another lord leads.</summary>
    public bool PlayerInAnotherLordsArmy { get; init; }

    public bool PlayerAtSea { get; init; }

    /// <summary><c>PlayerEncounter.Current != null</c>.</summary>
    public bool PlayerInEncounter { get; init; }

    /// <summary><c>Campaign.TrueSight</c>: the map shows every party.</summary>
    public bool TrueSight { get; init; }

    /// <summary>Both parties stand on the same navigation-mesh island.</summary>
    public bool SameIsland { get; init; }

    /// <summary>The player's seeing range, S.</summary>
    public float SeeingRange { get; init; }

    public float HunterX { get; init; }

    public float HunterY { get; init; }

    public float PlayerX { get; init; }

    public float PlayerY { get; init; }
}
