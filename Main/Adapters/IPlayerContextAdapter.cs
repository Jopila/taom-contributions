namespace TAOM.Adapters;

public interface IPlayerContextAdapter
{
    string GetPlayerKingdomId();

    /// <summary>
    /// The player clan's StringId, for a seam that only receives the victim and must tell whether
    /// the player is the executioner or the bereaved.
    /// </summary>
    string GetPlayerClanId();
    bool IsUnderMercenaryService();
}
