using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>Thin <see cref="MBActionSet"/> wrapper for the gait variety feature (<see cref="IGaitVarietyAdapter"/>).</summary>
public sealed class GaitVarietyAdapter : IGaitVarietyAdapter
{
    public IReadOnlyCollection<string> LoadedActionSetNames()
    {
        int count = MBActionSet.GetNumberOfActionSets();
        var names = new List<string>(count > 0 ? count : 0);
        for (int i = 0; i < count; i++)
            names.Add(MBActionSet.GetActionSetWithIndex(i).GetName());
        return names;
    }
}
