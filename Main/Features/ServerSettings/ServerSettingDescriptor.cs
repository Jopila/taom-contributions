using System;
using System.Collections.Generic;
using System.Reflection;

namespace TAOM.Features.ServerSettings;

/// <summary>One settable MCM property as the server file sees it: its key, owner class, kind, range or options, and group.</summary>
internal sealed class ServerSettingDescriptor
{
    public ServerSettingDescriptor(PropertyInfo property, ServerSettingKind kind, double min, double max,
        IReadOnlyList<string> options, string group)
    {
        Property = property;
        Kind = kind;
        Min = min;
        Max = max;
        Options = options;
        Group = group;
    }

    /// <summary>The JSON key: the property name, matched ordinally.</summary>
    public string Name => Property.Name;

    public Type Owner => Property.DeclaringType!;

    public PropertyInfo Property { get; }

    public ServerSettingKind Kind { get; }

    /// <summary>False for a button or a parked setting: the file cannot set either, and the defaults reference omits both.</summary>
    public bool IsSettableFromFile => Kind is not (ServerSettingKind.Button or ServerSettingKind.Parked);

    /// <summary>The MCM attribute's MinValue for Int and Float; 0 otherwise.</summary>
    public double Min { get; }

    /// <summary>The MCM attribute's MaxValue for Int and Float; 0 otherwise.</summary>
    public double Max { get; }

    /// <summary>The option strings of a Dropdown, as the compiled default lists them; empty otherwise.</summary>
    public IReadOnlyList<string> Options { get; }

    /// <summary>The MCM group path, as in "Battle Tactics/Mixed Formations"; empty when the property has none.</summary>
    public string Group { get; }
}
