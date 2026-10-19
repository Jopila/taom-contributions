using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.ShadowfaxScreens.Inventory;

namespace TAOM.Tests.Features.ShadowfaxScreens.Inventory;

/// <summary>The name-sort rewrite on synthetic IL shaped like InitializeInventory's two sort statements.</summary>
[TestClass]
public class NameSortRewriteTests
{
    private static readonly MethodInfo OrderBy = typeof(Enumerable).GetMethods()
        .Single(m => m.Name == "OrderBy" && m.GetParameters().Length == 2)
        .MakeGenericMethod(typeof(ItemRosterElement), typeof(string));

    private static readonly MethodInfo ToArray = typeof(Enumerable).GetMethod("ToArray")!
        .MakeGenericMethod(typeof(ItemRosterElement));

    private static readonly MethodInfo Helper =
        typeof(ShadowfaxInventoryCalls).GetMethod(nameof(ShadowfaxInventoryCalls.SortByNameOrPassThrough))!;

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private static List<CodeInstruction> Statement(Label? label = null)
    {
        var first = new CodeInstruction(OpCodes.Ldarg_0);
        if (label.HasValue) first.labels.Add(label.Value);
        return new List<CodeInstruction>
        {
            first,
            new(OpCodes.Ldnull),
            new(OpCodes.Call, OrderBy),
            new(OpCodes.Call, ToArray),
            new(OpCodes.Pop),
        };
    }

    private static List<CodeInstruction> Method(int sites)
    {
        var list = new List<CodeInstruction>();
        for (var i = 0; i < sites; i++) list.AddRange(Statement());
        list.Add(new CodeInstruction(OpCodes.Ret));
        return list;
    }

    [TestMethod]
    public void Rewrite_TwoSites_SwapsBothInPlace_WithNoWarning()
    {
        var list = Method(2);

        var result = NameSortRewrite.Rewrite(list, _logger, out var swapped);

        Assert.AreEqual(2, swapped);
        Assert.AreEqual(list.Count, result.Count);
        Assert.AreEqual(2, result.Count(c => c.operand is MethodInfo m && m == Helper));
        Assert.AreEqual(0, result.Count(c => c.operand is MethodInfo m && m == OrderBy));
        Assert.AreEqual(2, result.Count(c => c.operand is MethodInfo m && m == ToArray));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_KeepsLabelsOnTheSwappedInstructions()
    {
        var generator = new DynamicMethod("t", typeof(void), Type.EmptyTypes).GetILGenerator();
        var label = generator.DefineLabel();
        var list = Statement(label);
        list.AddRange(Statement());
        list.Add(new CodeInstruction(OpCodes.Ret));
        list[2].labels.Add(generator.DefineLabel());

        var result = NameSortRewrite.Rewrite(list, _logger, out var swapped);

        Assert.AreEqual(2, swapped);
        CollectionAssert.AreEqual(new[] { label }, result[0].labels);
        Assert.AreEqual(1, result[2].labels.Count);
        Assert.AreEqual(OpCodes.Call, result[2].opcode);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(3)]
    public void Rewrite_WrongSiteCount_ReturnsTheIlUnmodified_WithOneWarning(int sites)
    {
        var list = Method(sites);

        var result = NameSortRewrite.Rewrite(list, _logger, out var swapped);

        Assert.AreEqual(0, swapped);
        Assert.AreEqual(sites, result.Count(c => c.operand is MethodInfo m && m == OrderBy));
        Assert.AreEqual(0, result.Count(c => c.operand is MethodInfo m && m == Helper));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_AnOrderByNotFollowedByToArray_Bails()
    {
        var list = Method(2);
        list[3] = new CodeInstruction(OpCodes.Pop);

        var result = NameSortRewrite.Rewrite(list, _logger, out var swapped);

        Assert.AreEqual(0, swapped);
        Assert.AreEqual(2, result.Count(c => c.operand is MethodInfo m && m == OrderBy));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_SecondPass_BailsWithoutThrowing_AndChangesNothing()
    {
        var first = NameSortRewrite.Rewrite(Method(2), _logger, out _);
        _logger.ClearReceivedCalls();

        var second = NameSortRewrite.Rewrite(first, _logger, out var swapped);

        Assert.AreEqual(0, swapped);
        Assert.AreEqual(2, second.Count(c => c.operand is MethodInfo m && m == Helper));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_NullLogger_StillBailsCleanly()
    {
        var result = NameSortRewrite.Rewrite(Method(1), null, out var swapped);

        Assert.AreEqual(0, swapped);
        Assert.AreEqual(Method(1).Count, result.Count);
    }
}
