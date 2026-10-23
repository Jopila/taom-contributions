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
/// On a dedicated server Build leaves out every [ClientOnly] class before it reads the class attributes, so a patch
/// class that names an assembly the server cannot load costs nothing there, not even a skip.
/// </summary>
internal sealed class PatchCategoryIndex
{
    private readonly Dictionary<string, List<Type>> _classesByCategory;

    private PatchCategoryIndex(
        Dictionary<string, List<Type>> classesByCategory, List<KeyValuePair<Type, Exception>> skipped, List<Type> clientOnly)
    {
        _classesByCategory = classesByCategory;
        SkippedClasses = skipped;
        ClientOnlyClasses = clientOnly;
    }

    /// <summary>Each class whose attributes threw when read, with the exception; never applied.</summary>
    internal IReadOnlyList<KeyValuePair<Type, Exception>> SkippedClasses { get; }

    /// <summary>On a dedicated server, each [ClientOnly] class, left out before its attributes were read (ADR-013). Empty on a client.</summary>
    internal IReadOnlyList<Type> ClientOnlyClasses { get; }

    internal static PatchCategoryIndex Build(params Assembly[] assemblies) => Build(false, assemblies);

    /// <summary>
    /// As Build(assemblies); on a dedicated server (the caller reads GameNetwork.IsDedicatedServer) every [ClientOnly]
    /// class is left out and listed in ClientOnlyClasses, so a category applies its other classes there (ADR-013).
    /// </summary>
    internal static PatchCategoryIndex Build(bool dedicatedServer, params Assembly[] assemblies)
    {
        var classesByCategory = new Dictionary<string, List<Type>>();
        var skipped = new List<KeyValuePair<Type, Exception>>();
        var clientOnly = new List<Type>();
        foreach (var type in assemblies.SelectMany(AccessTools.GetTypesFromAssembly))
        {
            string category;
            try
            {
                // Before the Harmony attributes: a [ClientOnly] class names a type a server cannot load. IsDefined
                // resolves the TYPE of every attribute on the class, so it can throw for an unmarked class too (a
                // class-level [GameStateScreen] from a client-only assembly): inside the try, that class is skipped.
                if (dedicatedServer && type.IsDefined(typeof(ClientOnlyAttribute), inherit: false))
                {
                    clientOnly.Add(type);
                    continue;
                }

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

        return new PatchCategoryIndex(classesByCategory, skipped, clientOnly);
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
