using TAOM.Features.Relations;

namespace TAOM.Features.LongMemory;

/// <summary>
/// The "Long Memory" MCM group after clamping. Every amount is a magnitude from 0 up: the relation asked to be lost, before the
/// alignment multiplier, and the service negates it. The raid amounts apply at a whole raid (a smaller raid scales them down);
/// the coercion amounts apply to forcing a village to give goods or recruits. <see cref="Multipliers"/> scale by how the
/// player's side stands to the side of the village owner's realm.
/// </summary>
public sealed record LongMemorySettings(
    bool Enabled,
    bool RaidEnabled,
    bool CoercionEnabled,
    int RaidOwnerAmount,
    int RaidNotableAmount,
    int CoercionOwnerAmount,
    int CoercionNotableAmount,
    SideMultipliers Multipliers);
