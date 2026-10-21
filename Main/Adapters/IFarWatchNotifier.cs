namespace TAOM.Adapters;

/// <summary>
/// Boundary over the two ways Far Watch tells the player something (#847): a report in a modal that pauses the game, and a
/// one-line message for a ride that ended early.
/// </summary>
public interface IFarWatchNotifier
{
    /// <summary>
    /// Opens an inquiry with one Close button. It pauses the game (a modal opened from a game menu must, or the world moves under
    /// it) and its Close does nothing, so there is no menu state to revalidate.
    /// </summary>
    void ShowReport(string title, string body, string closeText);

    void ShowMessage(string text);
}
