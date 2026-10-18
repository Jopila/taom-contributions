namespace TAOM.Adapters;

/// <summary>
/// Engine reads the <c>[AnimVariety]</c> probe needs about action codes, action sets and clips, as plain
/// values (ADR-007). An action set is identified by its engine index, <c>MBActionSet.GetHashCode()</c>.
/// </summary>
public interface IAnimVarietyAdapter
{
    /// <summary>True when the action code name is defined (<c>ActionIndexCache.Create(name).Index &gt;= 0</c>).</summary>
    bool ActionCodeExists(string actionName);

    string ActionSetName(int actionSetKey);

    /// <summary><c>MBActionSet.AreActionsAlternatives</c>: false when the group has one member or the base row has no group.</summary>
    bool AreAlternatives(int actionSetKey, string actionNameA, string actionNameB);

    bool ClipExists(int actionSetKey, string actionName);

    string ClipName(int actionSetKey, string actionName);

    float ClipDuration(int actionSetKey, string actionName);

    /// <summary>
    /// <c>MBActionSet.GetActionBlendOutStartProgress</c>: the progress at which the clip starts to blend out.
    /// Natively this goes through the logging slot lookup, so ask only for a code whose clip exists.
    /// </summary>
    float ClipBlendOutStart(int actionSetKey, string actionName);
}
