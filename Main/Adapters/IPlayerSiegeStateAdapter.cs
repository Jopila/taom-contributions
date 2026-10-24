using TAOM.Features.RedArrow;

namespace TAOM.Adapters;

/// <summary>
/// The player's side of the Red Arrow (#873, ADR-007): whether the player leads a besieging army, and the facts vanilla's
/// map-action check reads. Each call reads the live campaign and keeps nothing. Both return the all-false state when there is
/// no campaign or no main party. Main thread only.
/// </summary>
public interface IPlayerSiegeStateAdapter
{
    /// <summary>Besieger side (<c>MainParty.BesiegerCamp != null</c>) and army leadership of the player's party.</summary>
    PlayerSiegeState ReadSiege();

    /// <summary>The facts of <c>CampaignUIHelper.GetMapScreenActionIsEnabledWithReason</c>, in vanilla's order.</summary>
    MapActionState ReadMapAction();
}
