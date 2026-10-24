namespace TAOM.Features.Provender;

/// <summary>
/// The player's two standing orders to the Provender-master. <c>ProvenderBehavior.SyncData</c> saves each as a bool under its own
/// key, and a missing key loads as off. The behavior builds its own instance for every campaign, so a new campaign, or a save
/// made before the feature (which has no record for the behavior), starts with both off.
/// </summary>
public sealed class ProvenderOrders
{
    /// <summary>Buy food at a market the party enters or leaves.</summary>
    public bool AutoFood { get; set; }

    /// <summary>Slaughter livestock each day the stores are low.</summary>
    public bool Slaughter { get; set; }
}
