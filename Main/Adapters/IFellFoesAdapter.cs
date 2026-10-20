using TAOM.Features.FellFoes.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The engine side of Fell Foes (ADR-007): one scan of the enemy soldiers and five writes. Every
/// <c>handle</c> is the opaque token a scan returned; each write re-checks that the agent is still the
/// live occupant of its engine slot and answers false when it is not. Main thread only (OnMissionTick).
/// </summary>
public interface IFellFoesAdapter
{
    /// <summary>Snapshots the active enemy soldiers of the player's team. Never returns null.</summary>
    FellFoesScan Scan(FellFoesScanRequest request);

    /// <summary>Ends a rout: the soldier stops running and fights. False when it was not routing or is already fading out.</summary>
    bool StopRout(object handle);

    /// <summary>Drops the soldier's morale to zero so the engine panics it again by its own rules.</summary>
    bool Unsteady(object handle);

    /// <summary>Sends the soldier off the field the way a router leaves, and keeps his morale up so he is taken, not killed.</summary>
    bool SendOff(object handle);

    /// <summary>Makes the player the archer's fixed target and turns off his automatic target choice.</summary>
    bool HoldTarget(object handle);

    /// <summary>Gives the archer back his automatic target choice. Safe on a stale handle.</summary>
    void ReleaseTarget(object handle);
}
