using System;
using System.Collections.Generic;

namespace TAOM.Features.NameGrammarTags;

/// <summary>A variable the render binds to the <c>Index</c>th object of its kind; <c>IsFaction</c> marks a FACTION variable, which may hold a kingdom or a clan.</summary>
public readonly record struct VariableBinding(string Variable, NameKind Kind, NameForm Form, int Index, bool IsFaction);

/// <summary>Numbers the objects a row needs: its first kingdom slot is object 0, its second kingdom slot object 1, and so on per kind.</summary>
public static class BindingPlanner
{
    public static IReadOnlyList<VariableBinding> Plan(IEnumerable<string> variables)
    {
        var slotIndexes = new Dictionary<(NameKind Kind, string Slot), int>();
        var perKind = new Dictionary<NameKind, int>();
        var plan = new List<VariableBinding>();
        foreach (var variable in variables)
        {
            var v = RenderVariableClassifier.Classify(variable);
            if (v.Kind == NameKind.Other)
                continue;
            if (!slotIndexes.TryGetValue((v.Kind, v.Slot), out var index))
            {
                perKind.TryGetValue(v.Kind, out index);
                perKind[v.Kind] = index + 1;
                slotIndexes[(v.Kind, v.Slot)] = index;
            }
            plan.Add(new VariableBinding(v.Name, v.Kind, v.Form, index, v.Name.IndexOf("FACTION", StringComparison.Ordinal) >= 0));
        }
        return plan;
    }
}
