namespace TAOM.Adapters;

/// <summary>Boundary over the one-time Hidden Hosts popup (#846, ADR-007): the map-free test and the inquiry itself.</summary>
public interface IHiddenHostsHintAdapter
{
    /// <summary>On the open campaign map, no encounter, no menu and no other popup open (no stacking).</summary>
    bool IsMapFree();

    /// <summary>
    /// Explains the symbols on the nameplate counts: a one-button popup that pauses the game.
    /// <paramref name="maskExample"/> is a partly hidden count such as "1??", <paramref name="horizon"/> the text a host at
    /// the edge of sight shows. The symbols come from the caller so the text and the nameplate can never disagree.
    /// </summary>
    void ShowSymbolHint(string maskExample, string horizon);

    /// <summary>
    /// Explains the "~" on a remembered count: a one-button popup that pauses the game. <paramref name="memoryExample"/> is a
    /// remembered count such as "~1??"; it comes from the caller so the text and the nameplate can never disagree.
    /// </summary>
    void ShowMemoryHint(string memoryExample);
}
