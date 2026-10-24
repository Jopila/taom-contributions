namespace TAOM.Features.RedArrow;

/// <summary>
/// The two facts that decide whether the Red Arrow applies, read by <c>IPlayerSiegeStateAdapter</c>. Primitives only
/// (ADR-007). A defender has a siege event of its own but no besieger camp, so <see cref="IsBesieger"/> is the camp.
/// </summary>
public readonly struct PlayerSiegeState
{
    public PlayerSiegeState(bool isBesieger, bool leadsArmy)
    {
        IsBesieger = isBesieger;
        LeadsArmy = leadsArmy;
    }

    /// <summary>The player's party is in a besieger camp (<c>MobileParty.MainParty.BesiegerCamp != null</c>).</summary>
    public bool IsBesieger { get; }

    /// <summary>The player's party is the leader party of its army.</summary>
    public bool LeadsArmy { get; }
}
