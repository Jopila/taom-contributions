using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace TAOM.Adapters;

/// <summary>The one capped remove-by-id routine the desertion paths share.</summary>
internal static class TroopRosterRemoval
{
    /// <summary>
    /// Removes up to <paramref name="count"/> of the troop with <paramref name="troopId"/>, never more
    /// than the roster holds, and returns how many were removed (0 for an unknown troop or an empty stack).
    /// </summary>
    internal static int RemoveUpTo(TroopRoster roster, string troopId, int count)
    {
        var character = CharacterObject.Find(troopId);
        if (character == null) return 0;

        var index = roster.FindIndexOfTroop(character);
        if (index < 0) return 0;

        var n = System.Math.Min(count, roster.GetElementNumber(index));
        if (n <= 0) return 0;

        roster.AddToCounts(character, -n);
        return n;
    }
}
