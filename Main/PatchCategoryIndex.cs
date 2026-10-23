using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace TAOM;

/// <summary>
/// TAOM's index of Harmony patch classes by category, built class by class. Harmony's own
/// PatchCategory builds the same index for the whole assembly in one pass with no catch, and never
/// caches a build that threw, so one class whose attributes cannot be read (a [HarmonyPatch] naming
/// a type the engine no longer has) made every category call throw. Here that class is skipped and
/// listed in SkippedClasses, and every other class still applies. Build and Apply mirror
/// Harmony.BuildCategoryCache and Harmony.PatchCategory(Assembly, string) in Lib.Harmony 2.4.2,
/// using only Harmony's public API. It indexes one or more assemblies (ADR-013: TAOM.dll, then TAOM.Client.dll
/// when it loaded), so a category holds its core classes first and its client classes after them.
/// </summary>
internal sealed class PatchCategoryIndex
{
    private readonly Dictionary<string, List<Type>> _classesByCategory;

    private PatchCategoryIndex(
        Dictionary<string, List<Type>> classesByCategory, List<KeyValuePair<Type, Exception>> skipped)
    {
        _classesByCategory = classesByCategory;
        SkippedClasses = skipped;
    }

    /// <summary>Each class whose attributes threw when read, with the exception; never applied.</summary>
    internal IReadOnlyList<KeyValuePair<Type, Exception>> SkippedClasses { get; }

    internal static PatchCategoryIndex Build(params Assembly[] assemblies)
    {
        var classesByCategory = new Dictionary<string, List<Type>>();
        var skipped = new List<KeyValuePair<Type, Exception>>();
        foreach (var type in assemblies.SelectMany(AccessTools.GetTypesFromAssembly))
        {
            string category;
            try
            {
                var attributes = HarmonyMethodExtensions.GetFromType(type);
                if (attributes.Count == 0) continue;
                category = HarmonyMethod.Merge(attributes).category;
            }
            catch (Exception ex)
            {
                skipped.Add(new KeyValuePair<Type, Exception>(type, ex));
                continue;
            }

            if (string.IsNullOrEmpty(category)) continue;
            if (!classesByCategory.TryGetValue(category, out var classes))
                classesByCategory[category] = classes = new List<Type>();
            classes.Add(type);
        }

        return new PatchCategoryIndex(classesByCategory, skipped);
    }

    /// <summary>The classes the category holds, in apply order; empty for an unknown category.</summary>
    internal IReadOnlyList<Type> ClassesOf(string category) =>
        _classesByCategory.TryGetValue(category, out var classes) ? classes : new List<Type>();

    /// <summary>
    /// Patches the category's classes in the order of the assemblies, then in assembly order. Like Harmony, the first class that fails
    /// throws, and the classes before it stay patched; an unknown category applies nothing.
    /// A class in SkippedClasses belongs to no category here, so its category applies the rest
    /// without throwing: that class is reported only through SkippedClasses.
    /// </summary>
    internal void Apply(Harmony harmony, string category)
    {
        if (!_classesByCategory.TryGetValue(category, out var classes)) return;
        foreach (var type in classes)
            harmony.CreateClassProcessor(type).Patch();
    }
}
