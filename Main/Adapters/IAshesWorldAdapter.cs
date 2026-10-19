using System.Collections.Generic;
using TAOM.Features.FromTheAshes;

namespace TAOM.Adapters;

/// <summary>
/// The campaign world as From the Ashes sees it and changes it (docs/features/from-the-ashes.md). Materials come from the
/// party first, then from the stash of the fortification that owns the settlement the party is in, and only when the
/// player's clan owns that fortification; counting and taking walk the same sources in the same order.
/// </summary>
public interface IAshesWorldAdapter
{
    /// <summary>The settlement the main party is inside, or null when it is in none.</summary>
    AshesSiteSnapshot? CurrentSite();

    /// <summary>How many of the item the party and the usable stash hold together.</summary>
    int CountMaterial(string itemId);

    /// <summary>Takes <paramref name="count"/> of the item, party first. False, with nothing taken, when there is not that many.</summary>
    bool TakeMaterial(string itemId, int count);

    /// <summary>Puts items back into the party's inventory (a project's materials after a forced cancel); the stash is not refilled.</summary>
    void ReturnMaterial(string itemId, int count);

    /// <summary>Raises the looted village to its normal state (the engine's daily heal path). False when it is not Normal afterwards.</summary>
    bool RestoreVillage(string settlementId);

    /// <summary>
    /// Adds construction points to the head of the fief's build queue, and completes it when the points reach its cost.
    /// False when nothing was credited, because the queue is empty, the head is at level 3 or the fief is under siege;
    /// otherwise true.
    /// </summary>
    bool AddConstruction(string settlementId, float points);

    /// <summary>Changes the player's relation with every notable now in the settlement.</summary>
    void ChangeNotableRelations(string settlementId, int delta);

    /// <summary>Changes the player's relation with the settlement's owner, unless the owner is the player.</summary>
    void ChangeOwnerRelation(string settlementId, int delta);

    /// <summary>The ids of every hero in the main party, the player included.</summary>
    IReadOnlyList<string> PartyHeroIds();
}
