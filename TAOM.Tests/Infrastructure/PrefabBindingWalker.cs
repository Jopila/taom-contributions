using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// The binding walk the UIExtenderEx prefab tests share. A binding that names nothing is a silent no-op in Gauntlet, so every
/// <c>@Binding</c>, <c>{DataSource}</c> and <c>Command.*</c> in an inserted XML tree must resolve to a mixin member or a vanilla
/// one. The walk reads attribute data and constructs no TaleWorlds attribute, so it runs without the game.
/// </summary>
public static class PrefabBindingWalker
{
    private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Walks <paramref name="root"/> and its <c>Children</c> from the view model <paramref name="vm"/> and its
    /// <paramref name="mixin"/>, keeping a scope stack as Gauntlet does: a <c>{Name}</c> step pushes that property's type
    /// (which has no mixin) and each <c>DataSource="{..}"</c> step pops one level. <paramref name="parentVm"/> and
    /// <paramref name="parentMixin"/> are the one scope under the start, the level the first <c>{..}</c> above it returns to.
    /// <paramref name="owner"/> prefixes each failure when several prefabs share one assertion. Gauntlet counts path nodes, not
    /// steps: a list item adds one node (start a walk inside an item template with the list as <paramref name="parentVm"/>), and
    /// a multi-node path such as <c>{A\B}</c> is two, which this walk reads as one name and so reports as a failure.
    /// </summary>
    public static (List<string> Failures, int Count) Walk(
        XmlElement root, Type vm, Type? mixin, Type? parentVm = null, Type? parentMixin = null, string owner = "")
    {
        var failures = new List<string>();
        var count = 0;
        var parent = parentVm == null ? null : new Scope(parentVm, parentMixin, null);
        Walk(root, new Scope(vm, mixin, parent), owner.Length == 0 ? "" : owner + ": ", failures, ref count);
        return (failures, count);
    }

    // An immutable stack: each branch of the walk holds its own chain, so a pop in one never leaks into a sibling.
    private sealed record Scope(Type Vm, Type? Mixin, Scope? Parent);

    private static void Walk(XmlElement element, Scope current, string prefix, List<string> failures, ref int count)
    {
        // A DataSource on an element scopes the element's own bindings too.
        var dataSource = element.GetAttribute("DataSource");
        if (dataSource.StartsWith("{", StringComparison.Ordinal))
        {
            count++;
            var path = dataSource.Trim('{', '}');
            if (path == "..")
            {
                if (current.Parent == null)
                {
                    failures.Add($"{prefix}DataSource {dataSource} has no parent scope to return to.");
                    return;
                }

                current = current.Parent;
            }
            else
            {
                if (Find(current.Vm, path, current.Mixin, wantMethod: false) is not PropertyInfo property)
                {
                    failures.Add($"{prefix}DataSource {dataSource} resolves to no property on {current.Vm.Name} or its mixin.");
                    return;
                }

                current = new Scope(property.PropertyType, null, current);
            }
        }

        foreach (XmlAttribute attribute in element.Attributes)
        {
            var isBinding = attribute.Value.StartsWith("@", StringComparison.Ordinal);
            var isCommand = attribute.Name.StartsWith("Command.", StringComparison.Ordinal);
            if (!isBinding && !isCommand)
                continue;
            count++;
            var name = isBinding ? attribute.Value.Substring(1) : attribute.Value;
            if (Find(current.Vm, name, current.Mixin, wantMethod: isCommand) == null)
                failures.Add($"{prefix}{attribute.Name}=\"{attribute.Value}\" on <{element.Name}> resolves to no member of {current.Vm.Name} or its mixin.");
        }

        foreach (var child in element.SelectNodes("Children/*")!.Cast<XmlElement>())
            Walk(child, current, prefix, failures, ref count);
    }

    // Gauntlet binds an @value and a DataSource to a property and runs a Command as a method, never the other way round. A
    // mixin member counts only with its UIExtenderEx attribute (the wrapper exposes nothing else). A vanilla member counts at any
    // visibility on the scope's own type, as the binder reads private view model members too, but a private member of a base
    // class is invisible to its reflection, so only non-private members count there.
    private static MemberInfo? Find(Type scope, string name, Type? mixin, bool wantMethod)
    {
        bool IsKind(MemberInfo m) => wantMethod ? m is MethodInfo : m is PropertyInfo;

        if (mixin != null)
        {
            var onMixin = mixin.GetMember(name, BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => IsKind(m) && m.CustomAttributes.Any(a => a.AttributeType.Name == "DataSourceProperty" || a.AttributeType.Name == "DataSourceMethodAttribute"));
            if (onMixin != null)
                return onMixin;
        }

        for (var type = scope; type != null; type = type.BaseType)
        {
            var member = type.GetMember(name, AnyInstance).FirstOrDefault(m => IsKind(m) && (type == scope || !IsPrivate(m)));
            if (member != null)
                return member;
        }

        return null;
    }

    private static bool IsPrivate(MemberInfo member) => member switch
    {
        MethodInfo method => method.IsPrivate,
        PropertyInfo property => property.GetAccessors(nonPublic: true).All(a => a.IsPrivate),
        _ => false,
    };
}
