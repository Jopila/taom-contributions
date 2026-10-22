using System;
using System.Collections.Generic;
using System.Reflection;

namespace TAOM.Tests.Infrastructure;

/// <summary>The four ways a type can fail <c>Assembly.GetTypes()</c> for want of a client-only assembly.</summary>
internal enum ClientShape
{
    /// <summary>A base type or interface, generic arguments included, recursively.</summary>
    Hierarchy = 1,

    /// <summary>A declared field of a VALUE type from such an assembly (enum, struct, Nullable, KeyValuePair).</summary>
    ValueField = 2,

    /// <summary>A generic parameter constraint of the type or of a declared method.</summary>
    Constraint = 3,

    /// <summary>A declared override of a virtual from another assembly that names such a type in its signature.</summary>
    Override = 4,
}

/// <summary>
/// The walk behind <see cref="ServerLoadGateTests"/>. Reference-typed fields (<c>List&lt;Client&gt;</c>, arrays)
/// and method bodies do not fail GetTypes, so they are not shapes. The client-only test is a parameter so a
/// fixture can stand in a loaded assembly for a missing one (<c>ServerLoadFixtureTests</c>).
/// </summary>
internal static class ServerLoadScan
{
    private const BindingFlags AllDeclared = BindingFlags.Instance | BindingFlags.Static
        | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>Each of <paramref name="types"/> that cannot load, mapped to the first offending assembly and the shape.</summary>
    internal static Dictionary<Type, (string Assembly, ClientShape Shape)> FindClientDependentTypes(
        IEnumerable<Type> types, Func<string, bool> isClientOnly)
    {
        var result = new Dictionary<Type, (string, ClientShape)>();
        foreach (var type in types)
        {
            if (ClientAssemblyOfHierarchy(type, isClientOnly) is { } hierarchy)
                result[type] = (hierarchy, ClientShape.Hierarchy);
            else if (ClientAssemblyOfValueFields(type, isClientOnly) is { } field)
                result[type] = (field, ClientShape.ValueField);
            else if (ClientAssemblyOfConstraints(type, isClientOnly) is { } constraint)
                result[type] = (constraint, ClientShape.Constraint);
            else if (ClientAssemblyOfOverrides(type, isClientOnly) is { } over)
                result[type] = (over, ClientShape.Override);
        }
        return result;
    }

    private static string? ClientAssemblyOfHierarchy(Type type, Func<string, bool> isClientOnly)
    {
        for (var b = type.BaseType; b != null; b = b.BaseType)
            if (ClientAssemblyOf(b, isClientOnly) is { } hit)
                return hit;
        foreach (var i in type.GetInterfaces())
            if (ClientAssemblyOf(i, isClientOnly) is { } hit)
                return hit;
        return null;
    }

    private static string? ClientAssemblyOfValueFields(Type type, Func<string, bool> isClientOnly)
    {
        foreach (var field in type.GetFields(AllDeclared))
            if (field.FieldType.IsValueType && ClientAssemblyOf(field.FieldType, isClientOnly) is { } hit)
                return hit;
        return null;
    }

    private static string? ClientAssemblyOfConstraints(Type type, Func<string, bool> isClientOnly)
    {
        var own = type.IsGenericTypeDefinition ? type.GetGenericArguments() : Type.EmptyTypes;
        if (ClientAssemblyOfParameters(own, isClientOnly) is { } ownHit)
            return ownHit;
        foreach (var method in type.GetMethods(AllDeclared))
            if (method.IsGenericMethodDefinition
                && ClientAssemblyOfParameters(method.GetGenericArguments(), isClientOnly) is { } hit)
                return hit;
        return null;
    }

    private static string? ClientAssemblyOfParameters(Type[] genericParameters, Func<string, bool> isClientOnly)
    {
        foreach (var parameter in genericParameters)
            foreach (var constraint in parameter.GetGenericParameterConstraints())
                if (ClientAssemblyOf(constraint, isClientOnly) is { } hit)
                    return hit;
        return null;
    }

    private static string? ClientAssemblyOfOverrides(Type type, Func<string, bool> isClientOnly)
    {
        foreach (var method in type.GetMethods(AllDeclared))
        {
            if (!method.IsVirtual || (method.Attributes & MethodAttributes.NewSlot) != 0)
                continue;
            var baseDefinition = method.GetBaseDefinition();
            if (baseDefinition.DeclaringType == null || baseDefinition.DeclaringType.Assembly == type.Assembly)
                continue;
            if (ClientAssemblyOf(method.ReturnType, isClientOnly) is { } returned)
                return returned;
            foreach (var parameter in method.GetParameters())
                if (ClientAssemblyOf(parameter.ParameterType, isClientOnly) is { } hit)
                    return hit;
        }
        return null;
    }

    private static string? ClientAssemblyOf(Type t, Func<string, bool> isClientOnly)
    {
        if (t.HasElementType)
            return ClientAssemblyOf(t.GetElementType()!, isClientOnly);
        if (t.IsGenericParameter)
            return null;
        var name = t.Assembly.GetName().Name!;
        if (isClientOnly(name))
            return name;
        if (t.IsGenericType)
            foreach (var arg in t.GetGenericArguments())
            {
                var hit = ClientAssemblyOf(arg, isClientOnly);
                if (hit != null) return hit;
            }
        return null;
    }
}
