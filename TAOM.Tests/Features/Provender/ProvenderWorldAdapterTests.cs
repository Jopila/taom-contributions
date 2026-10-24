using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.Provender;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Provender;

/// <summary>
/// The Provender-master's singletons live for the whole process, so none may keep an engine reference type in a field: a hero,
/// party or settlement from the last campaign would be a stale reference in the next (the rule of <c>FarWatchWorldAdapterTests</c>).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ProvenderWorldAdapterTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static bool FromTheEngine(Type type) =>
        type.Assembly.GetName().Name.StartsWith("TaleWorlds.", StringComparison.Ordinal)
        || type.Assembly.GetName().Name.StartsWith("SandBox", StringComparison.Ordinal)
        || type.Assembly.GetName().Name.StartsWith("StoryMode", StringComparison.Ordinal);

    private static IEnumerable<Type> Parts(Type type)
    {
        if (type.HasElementType) foreach (var p in Parts(type.GetElementType()!)) yield return p;
        if (type.IsGenericType) foreach (var a in type.GetGenericArguments()) foreach (var p in Parts(a)) yield return p;
        if (!type.IsValueType) yield return type;
        else
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.FieldType != type)
                    foreach (var p in Parts(field.FieldType)) yield return p;
    }

    [DataTestMethod]
    [DataRow(typeof(ProvenderWorldAdapter))]
    [DataRow(typeof(ProvenderService))]
    [DataRow(typeof(RationCatalogProvider))]
    [DataRow(typeof(ProvenderSettingsProvider))]
    public void TheSingleton_HoldsNoEngineReferenceBetweenCalls(Type singleton)
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var offenders = singleton
            .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(f => Parts(f.FieldType).Any(FromTheEngine))
            .Select(f => f.Name + ": " + f.FieldType)
            .ToList();

        Assert.AreEqual(0, offenders.Count, "engine reference types held between calls: " + string.Join(", ", offenders));
    }
}
