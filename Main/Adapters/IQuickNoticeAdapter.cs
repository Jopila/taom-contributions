namespace TAOM.Adapters;

/// <summary>
/// ADR-007 boundary over <c>MBInformationManager.AddQuickInformation</c>: a notice that shows a hero's
/// portrait beside the text (#835). False when showing it threw, so the caller can fall back to a plain
/// message; a notice the UI does not display cannot be detected.
/// </summary>
public interface IQuickNoticeAdapter
{
    /// <summary>Shows <paramref name="text"/> (already localized) with the given hero's portrait; false on any failure.</summary>
    bool Show(string text, string announcerHeroId);
}
