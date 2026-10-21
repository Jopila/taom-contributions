namespace TAOM.Adapters;

/// <summary>
/// One party in sight of the main party, for a sweep. The fog facts, the distance and whether it leads an army are the
/// <see cref="PartyIntelSnapshot"/> Hidden Hosts reads; the rest are the engine's own flags, the name its map plate prints
/// and the healthy men by kind (foot, archers, horse, horse archers), an army leader's counting its attached parties.
/// </summary>
public sealed record ScoutSighting
{
    public PartyIntelSnapshot Intel { get; init; }

    /// <summary>The name the party's map plate prints: the army's for a leader, else the leader hero's, else the party's own.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Map offset from the main party: +X east, +Y north.</summary>
    public float Dx { get; init; }

    public float Dy { get; init; }

    public bool IsVillager { get; init; }

    public bool IsCaravan { get; init; }

    /// <summary>Attached to an army leader's party (<c>MobileParty.AttachedTo</c>): it moves with the leader. A member not yet attached still moves alone.</summary>
    public bool IsAttachedToArmy { get; init; }

    /// <summary>A lord party with a hero leader.</summary>
    public bool IsLordWithHero { get; init; }

    public bool InSettlement { get; init; }

    public int Foot { get; init; }

    public int Archers { get; init; }

    public int Horse { get; init; }

    public int HorseArchers { get; init; }
}
