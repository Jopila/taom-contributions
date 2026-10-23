namespace TAOM.Adapters;

/// <summary>
/// ADR-007 boundary over <c>BattleRewardModel</c> for the personal spoils (#834). Every method fails
/// to null: a spoil is a bonus and never worth an exception.
/// </summary>
public interface IBattleLootAdapter
{
    /// <summary>The model's expected looted value for a casualty of the given character, or null when unreadable or not finite.</summary>
    float? GetExpectedLootValue(string characterId);

    /// <summary>
    /// Rolls an item from the character's battle gear for the target value and adds it to the main
    /// party's inventory in one step, keeping the modifier the engine rolled. Returns the item's display
    /// name, or null when nothing was granted: the character is missing, has no battle set (vanilla would
    /// throw), the roll came up empty (it often does), the item is a banner, or anything failed.
    /// </summary>
    string GrantLootFrom(string characterId, float targetValue);
}
