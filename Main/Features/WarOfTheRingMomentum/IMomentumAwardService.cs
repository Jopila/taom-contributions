using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Momentum from a script rather than a map event (#765, M6): the War Chronicle awards a side when one of
/// its events resolves. The award is a <see cref="MomentumActionType.Chronicle"/> event that decays like
/// every other.
/// </summary>
public interface IMomentumAwardService
{
    /// <summary>
    /// Adds <paramref name="units"/> displayed momentum to <paramref name="side"/>, clamped to 1 to
    /// <see cref="MomentumAwardService.MaxScriptedUnits"/>. Refuses (false) while momentum is off, before
    /// the war starts, after it ends, for units below 1 and for a non-finite clock.
    /// </summary>
    bool TryAwardScripted(MomentumSide side, int units, string description, double nowHours);
}
