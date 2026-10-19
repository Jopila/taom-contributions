using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ShadowfaxScreens.Inventory;

namespace TAOM.Tests.Features.ShadowfaxScreens.Inventory;

[TestClass]
public class SourceScopedMemoTests
{
    private readonly SourceScopedMemo<List<int>, string?> _sut = new();

    [TestMethod]
    public void TryGet_BeforeAnyStore_Misses()
    {
        Assert.IsFalse(_sut.TryGet(new List<int>(), out _));
    }

    [TestMethod]
    public void TryGet_SameSource_HitsWithTheStoredValue()
    {
        var source = new List<int>();
        _sut.Store(source, "concept");

        Assert.IsTrue(_sut.TryGet(source, out var value));
        Assert.AreEqual("concept", value);
    }

    [TestMethod]
    public void TryGet_NewSource_MissesSoTheValueIsRecomputed()
    {
        _sut.Store(new List<int>(), "old");

        Assert.IsFalse(_sut.TryGet(new List<int>(), out _));
    }

    [TestMethod]
    public void Store_Null_IsCachedAsAHit()
    {
        var source = new List<int>();
        _sut.Store(source, null);

        Assert.IsTrue(_sut.TryGet(source, out var value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void Store_ReplacesThePreviousSource()
    {
        var first = new List<int>();
        var second = new List<int>();
        _sut.Store(first, "a");
        _sut.Store(second, "b");

        Assert.IsFalse(_sut.TryGet(first, out _));
        Assert.IsTrue(_sut.TryGet(second, out var value));
        Assert.AreEqual("b", value);
    }

    [TestMethod]
    public void Memo_HoldsTheSourceWeakly()
    {
        var memo = new SourceScopedMemo<object, string?>();
        var reference = Fill(memo);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        Assert.IsFalse(reference.IsAlive, "The memo kept its source list alive.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference Fill(SourceScopedMemo<object, string?> memo)
    {
        var source = new object();
        memo.Store(source, "x");
        return new WeakReference(source);
    }
}
