namespace TAOM.Adapters;

/// <summary>
/// Marks a clan's or kingdom's lands on the map: each town and castle gets the engine's visual tracker mark (the
/// player removes one with Track on the settlement's encyclopedia page) and its living heroes become known to the player
/// (each newly known hero prints the engine's "learned about" line and gets a last-seen place from the engine).
/// Separate from <c>IPlayerIdentityAdapter.MarkClanAndKingdomKnown</c>, which marks a different set (the player's
/// own clan) for the Player Switcher.
/// </summary>
public interface IMapRevealAdapter
{
    void RevealClan(string clanId);

    void RevealKingdom(string kingdomId);
}
