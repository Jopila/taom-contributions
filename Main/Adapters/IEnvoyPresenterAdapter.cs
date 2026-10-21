using System;

namespace TAOM.Adapters;

/// <summary>
/// What the player sees of a tribute envoy (#870 slice 2, ADR-007): the single-choice popup and the lines after a
/// choice. The popup is the engine's multi-selection inquiry with exactly one option to pick; it has no cancel button, so
/// the player must answer. Main thread only.
/// </summary>
public interface IEnvoyPresenterAdapter
{
    /// <summary>
    /// The player is on the map with no encounter, menu or other inquiry open (<c>MapPopupGate.IsMapFree</c>, the one
    /// test every TAOM map popup asks).
    /// </summary>
    bool IsMapFree();

    /// <summary>
    /// Shows the offer. <paramref name="onChosen"/> runs when the player answers, inside the engine's click handler, so a
    /// popup shown from it queues behind the closing one. A popup that closes any other way (a load, the engine hiding the
    /// inquiry) runs nothing; <see cref="IsMapFree"/> is true again and the caller asks afresh.
    /// </summary>
    void ShowOffer(EnvoyOffer offer, Action<EnvoyChoice> onChosen);

    /// <summary>A line telling the player what their choice did; <paramref name="price"/> and <paramref name="days"/> fill the sentence.</summary>
    void ShowOutcome(EnvoyOutcome outcome, string keyedHostName, int price, int days);
}
