using TAOM.Core.Validation;

namespace TAOM.Features.HearthRoll;

/// <summary>
/// The two decisions behind the clan list badges (#906), pure so they test without the game. The tint marks a player
/// companion whose relation with the player has gone below zero (vanilla removes a companion at less than minus ten, so this is the
/// early warning). The age is shown only on the rows vanilla draws as children, as whole years the way the encyclopedia prints them.
/// </summary>
public static class HearthRollRules
{
    /// <summary>True when the row gets the red tint: the toggle is on, the hero is the player's companion and the relation is below zero.</summary>
    public static bool ShowsUnhappyTint(bool enabled, bool isPlayerCompanion, int relation)
        => enabled && isPlayerCompanion && relation < 0;

    /// <summary>Whole years for a child's row, or null for any other row or an age that is not a usable number.</summary>
    public static int? AgeYears(bool isChild, float age)
        => isChild && FiniteFloatValidator.IsFiniteAtLeast(age, 0f) ? (int)age : (int?)null;
}
