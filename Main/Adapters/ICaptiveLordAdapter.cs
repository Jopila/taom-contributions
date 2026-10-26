using TAOM.Features.HardQuestions.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The captive lord and the conversation he stands in, as Hard Questions sees them
/// (docs/features/hard-questions.md). Boundary only: ids and plain values, no Hero.
/// </summary>
public interface ICaptiveLordAdapter
{
    /// <summary>The id of the hero the player is talking to, or null when there is none.</summary>
    string? ConversationPartnerId { get; }

    /// <summary>True in the conversations that decide a prisoner's fate (a captured lord, a free-or-capture choice).</summary>
    bool IsPrisonerDecisionContext { get; }

    /// <summary>
    /// True while the top party screen is the ransom broker's (<c>PartyScreenMode.Ransom</c>). That screen holds cloned
    /// rosters and never writes them back, so a deed done on a lord shown there would be undone (issue #1001). It scans
    /// the whole state stack with LastOrDefault, never ActiveState, because the conversation runs in a MissionState pushed
    /// above the screen.
    /// </summary>
    bool IsRansomScreenOpen { get; }

    /// <summary>The cheap facts about a living prisoner lord (safe in a dialog condition), or null for a missing, dead or free hero.</summary>
    CaptiveSnapshot? Snapshot(string heroId);

    /// <summary>The people a deed on this lord touches. Walks the world: call it once per deed, never from a dialog condition.</summary>
    CaptiveCircle? Circle(string heroId);

    /// <summary>True when the hero still exists and is alive.</summary>
    bool IsAlive(string heroId);
}
