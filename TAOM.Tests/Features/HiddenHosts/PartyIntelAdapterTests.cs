using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// The adapters cache half a second of numbers between calls (#846). A campaign object kept in a field would outlive its
/// campaign: an adapter is a process singleton, and a faction, party or hero from the last game is a stale reference in the
/// next. The memory's campaign adapter (phase 3) keeps nothing at all, and is held to the same rule.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class PartyIntelAdapterTests
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

    [DataTestMethod]
    [DataRow(typeof(PartyIntelAdapter))]
    [DataRow(typeof(HiddenHostsCampaignAdapter))]
    public void HiddenHostsAdapters_KeepNoCampaignObjectBetweenCalls(Type adapter)
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var offenders = adapter
            .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(f => Parts(f.FieldType).Any(FromTheEngine))
            .Select(f => f.Name + ": " + f.FieldType)
            .ToList();

        Assert.AreEqual(0, offenders.Count, "engine reference types held between calls: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void Predicate_FlagsAnEngineReferenceFieldAndLetsNumbersThrough()
    {
        Assert.IsTrue(Parts(typeof(TaleWorlds.CampaignSystem.Party.MobileParty)).Any(FromTheEngine));
        Assert.IsTrue(Parts(typeof(List<TaleWorlds.CampaignSystem.Party.MobileParty>)).Any(FromTheEngine));
        Assert.IsFalse(Parts(typeof(Dictionary<string, long>)).Any(FromTheEngine));
        Assert.IsFalse(Parts(typeof(float)).Any(FromTheEngine));
    }
}
