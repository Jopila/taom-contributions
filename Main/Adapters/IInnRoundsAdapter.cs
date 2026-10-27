using TAOM.Features.InnRounds.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The engine facts and writes behind Inn Rounds (docs/features/inn-rounds.md): the tavern keeper and the town, the price of the
/// local drink, the two headcounts, the player's purse, the town's coffers, the party's morale and the notices. Ids and plain
/// values only (ADR-007). The reads never write. The skill experience, the renown, the holds on the map and the random numbers
/// come from the shared adapters (<c>IHeroSkillXpAdapter</c>, <c>IHeroRenownAdapter</c>, <c>IMapRevealAdapter</c>,
/// <c>IRandomSource</c>).
/// </summary>
public interface IInnRoundsAdapter
{
    /// <summary>
    /// The conversation partner and the town the player stands in. Null when there is no conversation partner or the read faults.
    /// Unless the partner is a tavern keeper and the player stands in a town, <c>InTown</c> is false and the town fields are empty.
    /// </summary>
    TavernSnapshot? Keeper();

    /// <summary>The campaign day, whole (<c>CampaignTime.Now.ToDays</c>).</summary>
    int Today { get; }

    /// <summary>
    /// The town market's price for one unit of the named trade good, as a buyer. Null when the item, the settlement, its town or
    /// its market is missing, or the price is not above 0.
    /// </summary>
    int? DrinkPrice(string settlementId, string itemId);

    /// <summary>
    /// The characters standing in the town's tavern, the keeper and the staff included. 0 unless the conversation runs in the
    /// tavern: vanilla also seats a keeper in the town center by day, and a round there is not poured for the tavern's patrons.
    /// </summary>
    int TavernHeadcount(string settlementId);

    /// <summary>Every man in the player's party, heroes included.</summary>
    int PartyHeadcount();

    /// <summary>The player hero's id; null before a campaign.</summary>
    string? PlayerHeroId { get; }

    int PlayerGold { get; }

    /// <summary>
    /// Pays <paramref name="price"/> gold from the player to the town's coffers. True when the purse lost exactly the price, also
    /// when a trade-event listener throws after the gold moved (logged). False, with the purse unchanged, for a short purse, an
    /// unknown settlement, a price of 0 or less, or a fault before the gold moved; a partial loss is given back.
    /// </summary>
    bool PayTown(string settlementId, int price);

    /// <summary>
    /// Adds to the player party's recent-events morale, which the engine clamps to [-100, 100], and returns the change that
    /// landed (0 when the party is gone or already at the ceiling).
    /// </summary>
    float AddMorale(float amount);

    /// <summary>The line after a party round: the town's drink for the culture and the change that landed in the party's recent-events morale.</summary>
    void ShowPartyRoundNotice(string cultureId, int morale);

    /// <summary>The line after a house round: the town's drink for the culture.</summary>
    void ShowHouseRoundNotice(string cultureId);

    /// <summary>The line after Charm experience was earned: the experience that landed.</summary>
    void ShowCharmXpNotice(int xp);
}
