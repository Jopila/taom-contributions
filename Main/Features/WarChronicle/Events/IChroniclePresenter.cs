using System;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// The engine side of the chronicle's player-facing text: whether the map is free for a popup, the
/// one-button inquiry itself, and resolving a <c>{=key}English</c> text for the momentum breakdown.
/// Never a scene notification: those NRE for custom races (docs/features/war-chronicle.md).
/// </summary>
public interface IChroniclePresenter
{
    /// <summary>
    /// The player is on the campaign map with no encounter, no open menu and no other inquiry showing
    /// (the Messenger <c>IsPlayerAvailable</c> check plus <c>InformationManager.IsAnyInquiryActive</c>).
    /// </summary>
    bool IsMapFree();

    /// <summary>Shows one inquiry with a single button; <paramref name="onClosed"/> runs when it is dismissed.</summary>
    void Show(ChronicleAnnouncement announcement, Action onClosed);

    /// <summary>The localized text of a <c>{=key}English</c> string; empty for null.</summary>
    string Resolve(string? text);
}
