using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Why a Harmony patch class needs a client-only assembly (ADR-013). Three ways the class names one: a
/// <c>[HarmonyPatch]</c> target type (class or method level), a patch-method parameter or return type, and the declaring
/// type of what a target selector returns (<c>TargetMethod</c>, <c>TargetMethods</c>, or any method carrying
/// <c>[HarmonyTargetMethod]</c> or <c>[HarmonyTargetMethods]</c>; a string-resolved target is invisible
/// to the attributes and to the signatures, so the gate runs it). The caller decides what "client-only" means.
/// </summary>
internal static class PatchClientReasons
{
    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private const BindingFlags AllSelectors =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static readonly Lazy<Harmony> HarmonyForSelectors = new(() => new Harmony("taom.tests.patchclientreasons"));

    /// <summary>
    /// One line per reason, "target", "signature" or "TargetMethod" first. A TargetMethod that throws is added to
    /// <paramref name="errors"/>, never swallowed.
    /// </summary>
    public static List<string> For(Type patchClass, Func<Assembly, bool> isClientOnly, List<string> errors)
    {
        var reasons = new List<string>();
        AddAttributeTargets(patchClass, isClientOnly, reasons);
        foreach (var type in WithNestedTypes(patchClass))
        {
            foreach (var method in type.GetMethods(AllDeclared))
            {
                AddAttributeTargets(method, isClientOnly, reasons);
                AddSignature(method, isClientOnly, reasons);
            }
        }

        AddTargetMethods(patchClass, isClientOnly, reasons, errors);
        return reasons.Distinct(StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<Type> WithNestedTypes(Type type)
    {
        yield return type;
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(WithNestedTypes))
            yield return nested;
    }

    private static void AddAttributeTargets(MemberInfo member, Func<Assembly, bool> isClientOnly, List<string> reasons)
    {
        var attributes = member is Type type
            ? HarmonyMethodExtensions.GetFromType(type)
            : HarmonyMethodExtensions.GetFromMethod((MethodBase)member);
        foreach (var attribute in attributes)
            if (attribute.declaringType is { } target && isClientOnly(target.Assembly))
                reasons.Add($"target {target.FullName} ({target.Assembly.GetName().Name})");
    }

    private static void AddSignature(MethodInfo method, Func<Assembly, bool> isClientOnly, List<string> reasons)
    {
        var types = method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType);
        foreach (var type in types.SelectMany(Flatten))
            if (isClientOnly(type.Assembly))
                reasons.Add($"signature {type.FullName} ({type.Assembly.GetName().Name})");
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.HasElementType)
        {
            foreach (var inner in Flatten(type.GetElementType()!))
                yield return inner;
            yield break;
        }

        yield return type;
        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments().SelectMany(Flatten))
                yield return argument;
    }

    // Harmony 2.4.2 (PatchTools.GetPatchMethod, PatchClassProcessor.RunMethod): a selector is the method carrying
    // [HarmonyTargetMethod(s)] under any name, else the method named TargetMethod(s); TargetMethods wins, and TargetMethod runs
    // only when it gave nothing. A parameter of type Harmony receives Harmony's instance.
    private static void AddTargetMethods(
        Type patchClass, Func<Assembly, bool> isClientOnly, List<string> reasons, List<string> errors)
    {
        foreach (var selector in new[] { typeof(HarmonyTargetMethods), typeof(HarmonyTargetMethod) })
        {
            if (!TryFindSelector(patchClass, selector, errors, out var method)) continue;

            IEnumerable<MethodBase?>? targets;
            try
            {
                var result = method.Invoke(null, method.GetParameters().Select(ArgumentFor).ToArray());
                targets = result switch
                {
                    null => null,
                    MethodBase single => new[] { single },
                    IEnumerable<MethodBase> many => many.ToList(),
                    _ => throw new InvalidOperationException("returned " + result.GetType().FullName),
                };
            }
            catch (Exception ex)
            {
                var cause = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
                errors.Add($"{patchClass.FullName}.{method.Name} threw {cause.GetType().Name}: {cause.Message}");
                continue;
            }

            if (targets == null) continue;
            foreach (var target in targets)
                if (target?.DeclaringType is { } declaring && isClientOnly(declaring.Assembly))
                    reasons.Add($"TargetMethod {declaring.FullName} ({declaring.Assembly.GetName().Name})");
            return;
        }
    }

    private static bool TryFindSelector(Type patchClass, Type attributeType, List<string> errors, out MethodInfo method)
    {
        method = patchClass.GetMethods(AllSelectors).FirstOrDefault(m =>
            m.GetCustomAttributes(true).Any(a => a.GetType().FullName == attributeType.FullName))!;
        if (method != null) return true;

        try
        {
            method = patchClass.GetMethod(attributeType.Name.Replace("Harmony", ""), AllSelectors)!;
        }
        catch (AmbiguousMatchException ex)
        {
            errors.Add($"{patchClass.FullName}.{attributeType.Name} is ambiguous: {ex.Message}");
        }

        return method != null;
    }

    private static object? ArgumentFor(ParameterInfo parameter) =>
        parameter.ParameterType.IsAssignableFrom(typeof(Harmony)) ? HarmonyForSelectors.Value : null;
}
