namespace TAOM.Adapters;

/// <summary>The world changes Hard Questions makes, each one engine action behind an id.</summary>
public interface IHardQuestionsActionAdapter
{
    /// <summary>
    /// Changes the player's relation with a hero; false when the hero is unknown. The engine lands the change on
    /// the pair from <c>DiplomacyModel.GetHeroesForEffectiveRelation</c> (the hero's clan leader), so callers pass
    /// that relation owner's id and charge each owner once.
    /// </summary>
    bool ChangeRelation(string heroId, int delta);

    /// <summary>Moves gold from the player to the hero; false when the player has less or the hero is unknown.</summary>
    bool PayGold(string heroId, int amount);

    bool Wound(string heroId);

    /// <summary>Kills a wounded hero the way a battle wound does. True only when he is dead afterwards: the engine may refuse.</summary>
    bool KillByWounds(string heroId);

    /// <summary>Takes clan renown from the player's clan, never below the floor of its current tier.</summary>
    void LoseRenown(int amount);

    /// <summary>
    /// Shows one summary line: the deed hurt the player's relations with this many clans. Callers have checked
    /// that the count is above 0 and that this is not a dedicated server.
    /// </summary>
    void ShowRelationsHurt(int count);

    /// <summary>Shows a short line in which a companion reproaches the player.</summary>
    void ShowReproach(string companionId);

    /// <summary>
    /// Commits the party screen that is still open under the conversation, if any. A conversation started from
    /// the party screen leaves the screen's pre-conversation snapshot alive, and closing it with Escape refills
    /// the prison and item rosters from that snapshot, undoing a death or a loot. Mirrors vanilla's reaction to
    /// a roster change under the screen (<c>PartyScreenLogic.DoneLogic(true)</c>).
    /// </summary>
    void CommitOpenPartyScreen();
}
