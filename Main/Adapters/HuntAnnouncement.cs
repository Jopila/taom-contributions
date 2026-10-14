namespace TAOM.Adapters;

/// <summary>The four lines the Eye of Sauron hunt shows the player (#765 decision: once at the start, once at the end).</summary>
public enum HuntAnnouncement
{
    /// <summary>A Free-side player: the Eye turns, a Nazgul rides.</summary>
    StartFree,

    /// <summary>An Evil-side player: Elrond sends Glorfindel.</summary>
    StartEvil,

    /// <summary>The hunter was beaten by the player.</summary>
    Won,

    /// <summary>Any other end. Side-neutral, since Glorfindel's hunt ends with it too.</summary>
    Over,
}
