namespace TAOM.Features.HardQuestions.Domain;

/// <summary>A lord seen from the service: his id, what places him on a side, and who holds his relation.</summary>
public sealed class LordRef
{
    /// <param name="relationId">
    /// The hero whose relation with the player the engine actually changes when this lord's changes: his clan
    /// leader unless he is clanless or a player companion (<c>DiplomacyModel.GetHeroesForEffectiveRelation</c>).
    /// Defaults to the lord himself.
    /// </param>
    public LordRef(string id, string? kingdomId, string? cultureId, string? relationId = null)
    {
        Id = id;
        KingdomId = kingdomId;
        CultureId = cultureId;
        RelationId = string.IsNullOrEmpty(relationId) ? id : relationId!;
    }

    public string Id { get; }

    public string? KingdomId { get; }

    public string? CultureId { get; }

    public string RelationId { get; }
}
