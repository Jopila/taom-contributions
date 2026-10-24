namespace TAOM.Features.RedArrow;

/// <summary>
/// Why the map-screen action behind the Army Management screen is blocked, or <see cref="None"/> when it is allowed.
/// Declared in the order vanilla's <c>CampaignUIHelper.GetMapScreenActionIsEnabledWithReason</c> tests them (v1.5.5).
/// <see cref="Siege"/> is vanilla's siege text for a settlement the player is not besieging.
/// </summary>
public enum MapActionReason
{
    None = 0,
    Prisoner,
    RaftState,
    FerryState,
    InMission,
    Encounter,
    Raid,
    Siege,
    MapEvent,
}
