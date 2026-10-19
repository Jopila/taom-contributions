namespace TAOM.Adapters;

/// <summary>
/// Changes the player's relation with a hero, by string id (ADR-007). The adapter behind <c>IRelationGrantService</c>,
/// the shared seam for new relation code.
/// </summary>
public interface IHeroRelationAdapter
{
    /// <summary>
    /// Changes the player's relation with a living hero without the on-screen quick notification, and returns what landed:
    /// the relation with the player after the change minus before. The engine runs the change through its diplomacy model
    /// first, so this can differ from <paramref name="delta"/>. 0 for an unknown or dead hero, the player hero, a zero delta,
    /// or an engine fault (logged).
    /// </summary>
    int ApplyPlayerRelation(string heroId, int delta);
}
