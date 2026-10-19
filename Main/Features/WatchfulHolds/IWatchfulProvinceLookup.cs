namespace TAOM.Features.WatchfulHolds;

/// <summary>
/// Which fort's province a map point lies in, when the Realm Borders provinces exist. A fort harasses only parties
/// inside its own province, so it does not reach across a river or a mountain range; with no provinces the gate is off.
/// </summary>
public interface IWatchfulProvinceLookup
{
    /// <summary>
    /// True when the provinces are ready and answer for the point; <paramref name="fiefId"/> is then the fort whose
    /// province holds it, or empty when the point lies in no province. False when there are no provinces (Realm
    /// Borders off, a dedicated server, the map still being sampled), and <paramref name="fiefId"/> is empty.
    /// </summary>
    bool TryGetProvinceFief(float x, float y, out string fiefId);
}
