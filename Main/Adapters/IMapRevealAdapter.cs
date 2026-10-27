using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// Marks lands on the map: each town and castle gets the engine's visual tracker mark (the player removes one with Track on
/// the settlement's encyclopedia page). A clan or a kingdom is revealed whole, and its living heroes become known to the player
/// (each newly known hero prints the engine's "learned about" line and gets a last-seen place from the engine). Separate from
/// <c>IPlayerIdentityAdapter.MarkClanAndKingdomKnown</c>, which marks a different set (the player's own clan) for the Player
/// Switcher. The by-id pair (<see cref="FortificationsAround"/>, <see cref="Track"/>) marks single holds and makes nobody known.
/// </summary>
public interface IMapRevealAdapter
{
    void RevealClan(string clanId);

    void RevealKingdom(string kingdomId);

    /// <summary>
    /// Every town and castle other than the named settlement, with its straight-line distance from it and whether it is
    /// tracked now. Reads only; an unknown settlement gives an empty list.
    /// </summary>
    IReadOnlyList<RevealCandidate> FortificationsAround(string settlementId);

    /// <summary>
    /// Marks each named town or castle that is not tracked yet and returns how many it marked. A settlement that is
    /// tracked, unknown or a village is left alone.
    /// </summary>
    int Track(IReadOnlyList<string> settlementIds);

    /// <summary>The campaign map's diagonal, the unit the reveal bound is a share of.</summary>
    float MapDiagonal { get; }

    /// <summary>The line after holds were marked on the map.</summary>
    void ShowHoldsMarkedNotice(int count);
}
