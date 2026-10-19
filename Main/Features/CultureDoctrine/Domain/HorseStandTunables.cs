namespace TAOM.Features.CultureDoctrine.Domain;

/// <summary>
/// The numbers the horse stand decides by, read once from the top-level <c>horseStand</c> block of
/// <c>culture_doctrines.json</c> (validated, defaults on any bad field): when the enemy's horse are
/// enough of a threat to stand against (<see cref="EnemyCavalryShare"/>), how badly outnumbered a
/// side must be before it digs in (<see cref="OutnumberedRatio"/>), and how much an AI soldier in a
/// formation holding a Dismount order, the whole standing wall, favours the polearm he carries
/// (<see cref="PolearmFavor"/>, the engine's
/// <c>AiWeaponFavorMultiplierPolearm</c>, 1 = vanilla). Immutable, so the team-AI tick and the
/// agent-stat model can both read it.
/// </summary>
public readonly struct HorseStandTunables
{
    public const float MinCavalryShare = 0.05f;
    public const float MaxCavalryShare = 1f;
    public const float MinOutnumberedRatio = 0.5f;
    public const float MaxOutnumberedRatio = 5f;
    public const float MinPolearmFavor = 1f;
    public const float MaxPolearmFavor = 4f;

    public static readonly HorseStandTunables Default = new HorseStandTunables(
        enemyCavalryShare: 0.5f, outnumberedRatio: 1f, polearmFavor: 2f);

    public HorseStandTunables(float enemyCavalryShare, float outnumberedRatio, float polearmFavor)
    {
        EnemyCavalryShare = enemyCavalryShare;
        OutnumberedRatio = outnumberedRatio;
        PolearmFavor = polearmFavor;
    }

    /// <summary>The stand starts when the enemy's melee cavalry is at least this share of its
    /// army (<c>TeamQuerySystem.EnemyCavalryRatio</c>, cached by the engine for 15 s).</summary>
    public float EnemyCavalryShare { get; }

    /// <summary>The stand starts when the enemy has more than our whole side (this team and its allies, `AllyUnitCount`) times this.</summary>
    public float OutnumberedRatio { get; }

    /// <summary>Multiplier on a standing wall soldier's liking for polearms, 1 to 4.</summary>
    public float PolearmFavor { get; }
}

/// <summary>A culture that stands against horse. Absent from a <see cref="Doctrine"/> means that
/// culture never does; <see cref="Dismount"/> says whether its cavalry get off their mounts to
/// join the wall (false for cultures whose riders sit on creatures or war rams).</summary>
public sealed class HorseStandPolicy
{
    public HorseStandPolicy(bool dismount)
    {
        Dismount = dismount;
    }

    public bool Dismount { get; }
}
