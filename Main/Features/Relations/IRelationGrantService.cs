namespace TAOM.Features.Relations;

/// <summary>
/// The shared seam for new relation code: it clamps and filters a request so every caller of it gets the same rules, and
/// the engine call lives behind <c>IHeroRelationAdapter</c>. Older code calls the engine directly (see relations.md).
/// </summary>
public interface IRelationGrantService
{
    /// <summary>
    /// Changes the player's relation with the hero by <paramref name="delta"/>, clamped to -100..100. False, with no
    /// change, for a zero delta, a blank id, an unknown or dead hero, a dedicated server, or an engine fault.
    /// <paramref name="reasonForLog"/> goes to the TAOM log only.
    /// </summary>
    bool Grant(string heroId, int delta, string reasonForLog);
}
