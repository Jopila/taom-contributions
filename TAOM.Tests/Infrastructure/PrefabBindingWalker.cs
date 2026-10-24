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
    /// <paramref name="mixin"/>. A <c>DataSource="{..}"</c> step returns to <paramref name="parentVm"/> and
    /// <paramref name="parentMixin"/> (the scope one level up); a <c>{Name}</c> step moves into that property's type, which
    /// has no mixin. <paramref name="owner"/> prefixes each failure when several prefabs share one assertion.
    /// </summary>
    public static (List<string> Failures, int Count) Walk(
        XmlElement root, Type vm, Type? mixin, Type? parentVm = null, Type? parentMixin = null, string owner = "")
    {
        var failures = new List<string>();
        var count = 0;
        Walk(root, vm, mixin, parentVm, parentMixin, owner.Length == 0 ? "" : owner + ": ", failures, ref count);
        return (failures, count);
    }

    private static void Walk(
        XmlElement element, Type scope, Type? mixin, Type? parentVm, Type? parentMixin, string prefix, List<string> failures, ref int count)
    {
        // A DataSource on an element scopes the element's own bindings too.
        var dataSource = element.GetAttribute("DataSource");
        if (dataSource.StartsWith("{", StringComparison.Ordinal))
        {
            count++;
            var path = dataSource.Trim('{', '}');
            if (path == "..")
            {
                if (parentVm == null)
                {
                    failures.Add($"{prefix}DataSource {dataSource} has no parent scope to return to.");
                    return;
                }

                scope = parentVm;
                mixin = parentMixin;
            }
            else
            {
                if (Find(scope, path, mixin) is not PropertyInfo property)
                {
                    failures.Add($"{prefix}DataSource {dataSource} resolves to no property on {scope.Name} or its mixin.");
                    return;
                }

                scope = property.PropertyType;
                mixin = null;
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
            if (Find(scope, name, mixin) == null)
                failures.Add($"{prefix}{attribute.Name}=\"{attribute.Value}\" on <{element.Name}> resolves to no member of {scope.Name} or its mixin.");
        }

        foreach (var child in element.SelectNodes("Children/*")!.Cast<XmlElement>())
            Walk(child, scope, mixin, parentVm, parentMixin, prefix, failures, ref count);
    }

    // A mixin member counts only with its UIExtenderEx attribute (the wrapper exposes nothing else); a vanilla member counts at
    // any visibility, as the Gauntlet binder reads private view model methods too.
    private static MemberInfo? Find(Type scope, string name, Type? mixin)
    {
        if (mixin != null)
        {
            var onMixin = mixin.GetMember(name, BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.CustomAttributes.Any(a => a.AttributeType.Name == "DataSourceProperty" || a.AttributeType.Name == "DataSourceMethodAttribute"));
            if (onMixin != null)
                return onMixin;
        }

        for (var type = scope; type != null; type = type.BaseType)
        {
            var member = type.GetMember(name, AnyInstance).FirstOrDefault(m => m is PropertyInfo || m is MethodInfo);
            if (member != null)
                return member;
        }

        return null;
    }
}
