using TAOM.Features.CoopInterop;

namespace TAOM.Features.FieldCamp.Activities;

/// <summary>
/// The single-player predicate of the camp's added rules (work, rest healing, start in camp), the shape of FarWatchService's: read
/// live on every use (ICoopSessionProvider's contract). False while a co-op session is live, while a co-op mod TAOM cannot probe is
/// loaded, on a dedicated server and on a peer that may not write the world.
/// </summary>
internal static class CampGate
{
    internal static bool IsSinglePlayer(ICoopSessionProvider coop, IDedicatedServerProvider dedicated) =>
        !coop.IsSessionActive && !coop.ShouldDeferToHost && !dedicated.IsDedicatedServer && coop.IsAuthority;
}
