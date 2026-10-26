namespace TAOM.Adapters;

/// <summary>What Bandit Voices (#900) reads from the player's bandit encounter and the two text variables it writes; an unreadable fact is null or false, never a throw.</summary>
public interface IBanditEncounterTextAdapter
{
    /// <summary>The encountered party's map faction id, the key vanilla builds its own per-faction variations from; null with no encounter.</summary>
    string? EncounteredFactionId { get; }

    /// <summary>True when the encountered party is on the water.</summary>
    bool EncounteredAtSea { get; }

    /// <summary>True when vanilla has no record of the conversation party yet; false when it has one or the record cannot be read.</summary>
    bool IsFirstMeeting();

    /// <summary>
    /// Sets the text variable the greeting line shows to the game text <paramref name="textId"/> under <paramref name="variation"/>;
    /// false, with nothing set, when that row does not exist.
    /// </summary>
    bool TrySetGreeting(string textId, string variation);

    /// <summary>Sets the text variable the player's ultimatum line shows to <paramref name="keyedText"/>, a <c>{=KEY}English</c> string.</summary>
    void SetUltimatum(string keyedText);
}
