namespace TAOM.Features.WatchfulHolds.Models;

/// <summary>
/// One party as the harassment rules read it: plain values plus an opaque <see cref="Handle"/> that goes
/// back to <c>IWatchfulHoldsAdapter</c> to act on the party (the object-token shape of
/// <c>ICavalryChargeService</c>'s <c>object formationKey</c> and <c>IMissionStartGuardService.BeginMission(object?)</c>,
/// which keep a TaleWorlds type out of a service signature). The service never looks inside the handle.
/// A struct, because the hourly event reads every party in the world and a snapshot must not allocate.
/// </summary>
public readonly struct WatchfulPartySnapshot
{
    public object? Handle { get; init; }

    public float X { get; init; }

    public float Y { get; init; }

    public bool IsMainParty { get; init; }

    public bool IsLordParty { get; init; }

    public bool IsMoving { get; init; }

    public bool InSettlement { get; init; }

    public bool InMapEvent { get; init; }

    public bool InSiegeEvent { get; init; }

    /// <summary>In an army but not its leader: the army leader is the one checked.</summary>
    public bool IsArmyFollower { get; init; }

    public bool IsArmyLeader { get; init; }

    public bool IsDisorganized { get; init; }

    /// <summary>The engine's own rule (<c>PartyImpairmentModel.CanGetDisorganized</c>).</summary>
    public bool CanGetDisorganized { get; init; }

    public int MenCount { get; init; }
}
