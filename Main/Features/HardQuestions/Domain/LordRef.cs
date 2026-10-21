using TAOM.Features.Execution;

namespace TAOM.Features.HardQuestions.Domain;

/// <summary>A lord seen from the service: his id, what places him on a side, and who holds his relation.</summary>
public sealed class LordRef
{
    /// <param name="relationId">
    /// The hero whose relation with the player the active DiplomacyModel changes: his clan leader under the
    /// vanilla mapping, the lord himself under Personal Relations (<c>GetHeroesForEffectiveRelation</c>).
    /// Defaults to the lord himself.
    /// </param>
    /// <param name="clanId">His clan, so a Neutral victim's clanmates can be told apart whoever owns their relation.</param>
    /// <param name="side">
    /// The pair the alignment table resolves: the lord's clan map faction id with that faction's culture, or a clanless
    /// hero's own culture (<c>FactionSides.OfHero</c>). <paramref name="kingdomId"/> stays for the lords who share a realm.
    /// </param>
    public LordRef(string id, string? kingdomId, SideIds side, string? relationId = null, string? clanId = null)
    {
        ClanId = clanId;
        Id = id;
        KingdomId = kingdomId;
        Side = side;
        RelationId = string.IsNullOrEmpty(relationId) ? id : relationId!;
    }

    public string Id { get; }

    public string? ClanId { get; }

    public string? KingdomId { get; }

    public SideIds Side { get; }

    public string RelationId { get; }
}
