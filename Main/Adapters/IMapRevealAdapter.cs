namespace TAOM.Adapters;

/// <summary>
/// Marks a clan's or kingdom's lands as seen: its settlements visited and its living heroes known to the player
/// (each newly known hero prints the engine's "learned about" line and gets a last-seen place from the engine).
/// Separate from <c>IPlayerIdentityAdapter.MarkClanAndKingdomKnown</c>, which marks a different set (the player's
/// own clan) for the Player Switcher.
/// </summary>
public interface IMapRevealAdapter
{
    void RevealClan(string clanId);

    void RevealKingdom(string kingdomId);
}
