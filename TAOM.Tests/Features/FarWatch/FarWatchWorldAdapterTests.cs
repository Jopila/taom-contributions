using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.FarWatch;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FarWatch;

/// <summary>
/// Every Far Watch singleton (the adapter, the notifier, the store, the service, the contributor) lives for the whole
/// process, so none may keep an engine reference type in a field: a hero, party or faction from the last campaign would be a
/// stale reference in the next and keep the finished campaign alive (the rule of <c>PartyIntelAdapterTests</c>, from the
/// Hidden Hosts review). The adapter's bucket mapping and the engine facts it relies on are pinned in
/// <see cref="FarWatchEngineFactsTests"/>.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FarWatchWorldAdapterTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static bool FromTheEngine(Type type) =>
        type.Assembly.GetName().Name.StartsWith("TaleWorlds.", StringComparison.Ordinal)
        || type.Assembly.GetName().Name.StartsWith("SandBox", StringComparison.Ordinal)
        || type.Assembly.GetName().Name.StartsWith("StoryMode", StringComparison.Ordinal);

    /// <summary>The reference types the field's type is made of: the type, its generic arguments and its element type.</summary>
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

    private static List<string> EngineFields(Type type) => type
        .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
        .Where(f => Parts(f.FieldType).Any(FromTheEngine))
        .Select(f => f.Name + ": " + f.FieldType)
        .ToList();

    [DataTestMethod]
    [DataRow(typeof(FarWatchWorldAdapter))]
    [DataRow(typeof(FarWatchNotifier))]
    [DataRow(typeof(FarWatchStore))]
    [DataRow(typeof(FarWatchService))]
    [DataRow(typeof(FarWatchSightContributor))]
    public void FarWatchSingletons_KeepNoEngineObjectBetweenCalls(Type singleton)
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var offenders = EngineFields(singleton);

        Assert.AreEqual(0, offenders.Count, "engine reference types held between calls: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void TheRule_FlagsAnEngineReferenceField_AndLetsPlainDataThrough()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.IsTrue(Parts(typeof(TaleWorlds.CampaignSystem.Hero)).Any(FromTheEngine));
        Assert.IsTrue(Parts(typeof(List<TaleWorlds.CampaignSystem.Party.MobileParty>)).Any(FromTheEngine));
        Assert.IsFalse(Parts(typeof(Dictionary<string, double>)).Any(FromTheEngine));
        Assert.IsFalse(Parts(typeof(string[])).Any(FromTheEngine));
    }
}
