namespace TAOM.Adapters;

/// <summary>
/// The player's personality trait experience, through the public <c>TraitLevelingHelper.OnIncidentResolved</c>
/// seam that wraps the private add-and-log method. Trait ids are the engine's: "Honor" and "Mercy".
/// </summary>
public interface IPlayerTraitAdapter
{
    /// <summary>Adds (or, when negative, removes) trait experience; false for an unknown trait.</summary>
    bool AddTraitXp(string traitId, int xp);
}
