using TAOM.Features.CoopInterop;

namespace TAOM.Features.AlignmentDesertion;

/// <summary>
/// Who an owner clan counts as for alignment desertion. Pure so the co-op table is a unit test.
/// </summary>
internal static class DesertionOwnerPolicy
{
    /// <summary>
    /// True when the owner loses no troops to alignment desertion: a remote co-op player's clan (not
    /// this peer's own) and any owner the player registry could not classify. Singleplayer is
    /// unchanged because <c>OfClan</c> answers Player only for <c>Clan.PlayerClan</c> there.
    /// </summary>
    internal static bool ShouldSkip(PlayerControl owner, bool isLocalPlayerClan)
        => owner == PlayerControl.Unknown || (owner == PlayerControl.Player && !isLocalPlayerClan);

    /// <summary>The local player's clan follows "Apply To Player"; an AI clan follows "Apply To AI Lords".</summary>
    internal static bool IsPlayerOwned(PlayerControl owner) => owner == PlayerControl.Player;
}
