namespace TAOM.Adapters;

/// <summary>The Good Deeds line shown in the message log, localized through a TAOM {=KEY} string.</summary>
public interface IGoodDeedsNoticeAdapter
{
    /// <summary>One green line: the people of the town honor the player's victory. Gender-neutral by design.</summary>
    void ShowTournamentHonor(string townName);
}
