namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// The engine side of the chronicle's popups: whether the map is free for one, and the one-button inquiry
/// itself. Never a scene notification: those NRE for custom races (docs/features/war-chronicle.md).
/// </summary>
public interface IChroniclePresenter
{
    /// <summary>
    /// The player is on the campaign map with no encounter, no open menu and no inquiry showing, this
    /// chronicle's own included (the Messenger <c>IsPlayerAvailable</c> check plus
    /// <c>InformationManager.IsAnyInquiryActive</c>). The queue relies on it to never stack popups.
    /// </summary>
    bool IsMapFree();

    /// <summary>Shows one inquiry with a single button that only closes it.</summary>
    void Show(ChronicleAnnouncement announcement);
}
