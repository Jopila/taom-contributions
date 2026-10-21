namespace TAOM.Adapters;

/// <summary>
/// The tribute envoy's reads and its one write on the player (#870 slice 2, ADR-007): who the player is, the kingdom of
/// their clan, their gold and Charm, and the payment. It reads and acts; every decision is the service's. Main thread only.
/// </summary>
public interface ITributeEnvoyAdapter
{
    /// <summary>The player now, read live; <see cref="EnvoyPlayerReading.Missing"/> when there is no main hero or clan.</summary>
    EnvoyPlayerReading ReadPlayer();

    /// <summary>
    /// Takes <paramref name="amount"/> denars from the player. False, with nothing taken, when the player has less (or the
    /// amount is negative).
    /// </summary>
    bool TryCharge(int amount);
}
