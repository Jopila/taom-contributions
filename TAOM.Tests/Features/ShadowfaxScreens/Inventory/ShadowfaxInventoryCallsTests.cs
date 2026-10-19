using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Inventory;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ShadowfaxScreens.Inventory;

/// <summary>The static helpers the two rewrites call: vanilla's result with no service, the toggle off or on.</summary>
[TestClass]
public class ShadowfaxInventoryCallsTests
{
    private IShadowfaxScreensSettingsProvider _settings = null!;
    private InventoryOpenFastPathService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        _settings.InventoryFastOpen.Returns(true);
        _service = new InventoryOpenFastPathService(_settings, new FakeStampClock(), new RecordingLogger());
        ShadowfaxInventoryCalls.Initialize(_service, null, null);
    }

    [TestCleanup]
    public void Cleanup() => ShadowfaxInventoryCalls.Initialize(null, null, null);

    private static Concept Concept(string id) => new() { StringId = id };

    private static readonly Func<ItemRosterElement, string> NeverCalled = _ => throw new InvalidOperationException("key");

    [TestMethod]
    public void SortByNameOrPassThrough_On_ReturnsTheSourceUntouched()
    {
        var source = new List<ItemRosterElement> { default, default };

        var result = ShadowfaxInventoryCalls.SortByNameOrPassThrough(source, NeverCalled);

        Assert.AreSame(source, result);
    }

    [TestMethod]
    public void SortByNameOrPassThrough_Off_SortsByTheKeyLikeVanilla()
    {
        _settings.InventoryFastOpen.Returns(false);
        var source = new List<ItemRosterElement> { default, default, default };
        var keys = new Queue<string>(new[] { "b", "c", "a" });
        var order = new List<string>();

        var result = ShadowfaxInventoryCalls.SortByNameOrPassThrough(source, _ => { var k = keys.Dequeue(); order.Add(k); return k; })
            .ToArray();

        Assert.AreEqual(3, result.Length);
        Assert.AreEqual(3, order.Count);
    }

    [TestMethod]
    public void SortByNameOrPassThrough_NoService_SortsLikeVanilla()
    {
        ShadowfaxInventoryCalls.Initialize(null, null, null);
        var calls = 0;

        ShadowfaxInventoryCalls.SortByNameOrPassThrough(new List<ItemRosterElement> { default, default }, _ => { calls++; return "x"; }).ToArray();

        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void CachedConcept_On_RunsThePredicateOncePerSourceAndSite()
    {
        var list = new List<Concept> { Concept("a"), Concept("b") };
        var calls = 0;
        Func<Concept, bool> predicate = c => { calls++; return c.StringId == "b"; };

        var first = ShadowfaxInventoryCalls.CachedConcept0(list, predicate);
        var second = ShadowfaxInventoryCalls.CachedConcept0(list, predicate);
        var otherSite = ShadowfaxInventoryCalls.CachedConcept1(list, predicate);

        Assert.AreSame(list[1], first);
        Assert.AreSame(first, second);
        Assert.AreSame(first, otherSite);
        Assert.AreEqual(4, calls, "2 predicate runs for the first site's one miss, 2 for the other site's one miss.");
    }

    [TestMethod]
    public void CachedConcept_On_CachesANullResult()
    {
        var list = new List<Concept> { Concept("a") };
        var calls = 0;
        Func<Concept, bool> predicate = c => { calls++; return false; };

        Assert.IsNull(ShadowfaxInventoryCalls.CachedConcept0(list, predicate));
        Assert.IsNull(ShadowfaxInventoryCalls.CachedConcept0(list, predicate));

        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void CachedConcept_On_NewSourceRecomputes()
    {
        var calls = 0;
        Func<Concept, bool> predicate = c => { calls++; return true; };

        ShadowfaxInventoryCalls.CachedConcept0(new List<Concept> { Concept("a") }, predicate);
        ShadowfaxInventoryCalls.CachedConcept0(new List<Concept> { Concept("a") }, predicate);

        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void CachedConcept_DuplicateMatch_ThrowsLikeVanilla_AndIsNotCached()
    {
        var list = new List<Concept> { Concept("a"), Concept("a") };
        Func<Concept, bool> predicate = c => c.StringId == "a";

        Assert.ThrowsException<InvalidOperationException>(() => ShadowfaxInventoryCalls.CachedConcept0(list, predicate));
        Assert.ThrowsException<InvalidOperationException>(() => ShadowfaxInventoryCalls.CachedConcept0(list, predicate));

        list.RemoveAt(1);
        Assert.AreSame(list[0], ShadowfaxInventoryCalls.CachedConcept0(list, predicate));
    }

    [TestMethod]
    public void CachedConcept_Off_RunsThePredicateEveryCall()
    {
        _settings.InventoryFastOpen.Returns(false);
        var list = new List<Concept> { Concept("a") };
        var calls = 0;
        Func<Concept, bool> predicate = c => { calls++; return true; };

        ShadowfaxInventoryCalls.CachedConcept0(list, predicate);
        ShadowfaxInventoryCalls.CachedConcept0(list, predicate);

        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void CachedConcept_NoService_IsPlainSingleOrDefault()
    {
        ShadowfaxInventoryCalls.Initialize(null, null, null);
        var list = new List<Concept> { Concept("a") };

        Assert.AreSame(list[0], ShadowfaxInventoryCalls.CachedConcept1(list, c => true));
        Assert.IsNull(ShadowfaxInventoryCalls.CachedConcept1(list, c => false));
    }
}
