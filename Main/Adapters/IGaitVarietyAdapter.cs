using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// Engine reads the gait variety feature needs about action sets, as plain values (ADR-007).
/// </summary>
public interface IGaitVarietyAdapter
{
    /// <summary>
    /// The name of every action set the engine loaded, in engine index order. One silent listing:
    /// <c>MBActionSet.GetNumberOfActionSets</c> and <c>GetActionSetWithIndex(i).GetName()</c> log nothing,
    /// where asking for an unknown id (<c>MBActionSet.GetActionSet</c>) makes the native lookup log
    /// "Action set could not be found, using default action set!" on every miss.
    /// </summary>
    IReadOnlyCollection<string> LoadedActionSetNames();
}
