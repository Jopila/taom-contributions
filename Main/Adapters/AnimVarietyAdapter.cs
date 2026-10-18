using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// Thin <see cref="MBActionSet"/> and <see cref="ActionIndexCache"/> wrapper for the <c>[AnimVariety]</c>
/// probe. Every member is a one-line engine call; the logic is in the service.
/// </summary>
public sealed class AnimVarietyAdapter : IAnimVarietyAdapter
{
    public bool ActionCodeExists(string actionName) => ActionIndexCache.Create(actionName).Index >= 0;

    public string ActionSetName(int actionSetKey) => MBActionSet.GetActionSetWithIndex(actionSetKey).GetName();

    public bool AreAlternatives(int actionSetKey, string actionNameA, string actionNameB)
    {
        var a = ActionIndexCache.Create(actionNameA);
        var b = ActionIndexCache.Create(actionNameB);
        return MBActionSet.GetActionSetWithIndex(actionSetKey).AreActionsAlternatives(in a, in b);
    }

    public bool ClipExists(int actionSetKey, string actionName)
    {
        var action = ActionIndexCache.Create(actionName);
        return MBActionSet.CheckActionAnimationClipExists(MBActionSet.GetActionSetWithIndex(actionSetKey), in action);
    }

    public string ClipName(int actionSetKey, string actionName)
    {
        var action = ActionIndexCache.Create(actionName);
        return MBActionSet.GetActionAnimationName(MBActionSet.GetActionSetWithIndex(actionSetKey), in action);
    }

    public float ClipDuration(int actionSetKey, string actionName)
    {
        var action = ActionIndexCache.Create(actionName);
        return MBActionSet.GetActionAnimationDuration(MBActionSet.GetActionSetWithIndex(actionSetKey), in action);
    }

    public float ClipBlendOutStart(int actionSetKey, string actionName)
    {
        var action = ActionIndexCache.Create(actionName);
        return MBActionSet.GetActionBlendOutStartProgress(MBActionSet.GetActionSetWithIndex(actionSetKey), in action);
    }
}
