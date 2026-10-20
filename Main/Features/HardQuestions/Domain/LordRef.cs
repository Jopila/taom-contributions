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
    public LordRef(string id, string? kingdomId, string? cultureId, string? relationId = null, string? clanId = null)
    {
        ClanId = clanId;
        Id = id;
        KingdomId = kingdomId;
        CultureId = cultureId;
        RelationId = string.IsNullOrEmpty(relationId) ? id : relationId!;
    }

    public string Id { get; }

    public string? ClanId { get; }

    public string? KingdomId { get; }

    public string? CultureId { get; }

    public string RelationId { get; }
}
