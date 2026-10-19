using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.ShadowfaxScreens.Inventory;

namespace TAOM.Tests.Features.ShadowfaxScreens.Inventory;

/// <summary>The concept-lookup rewrite on synthetic IL shaped like the SPItemVM constructor's two lookups.</summary>
[TestClass]
public class ConceptLookupRewriteTests
{
    private static readonly MethodInfo GetAll = AccessTools.PropertyGetter(typeof(Concept), nameof(Concept.All))!;

    private static readonly MethodInfo Single = typeof(Enumerable).GetMethods()
        .Single(m => m.Name == "SingleOrDefault" && m.GetParameters().Length == 2
            && m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2)
        .MakeGenericMethod(typeof(Concept));

    private static readonly MethodInfo Helper0 = typeof(ShadowfaxInventoryCalls).GetMethod(nameof(ShadowfaxInventoryCalls.CachedConcept0))!;
    private static readonly MethodInfo Helper1 = typeof(ShadowfaxInventoryCalls).GetMethod(nameof(ShadowfaxInventoryCalls.CachedConcept1))!;

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private static List<CodeInstruction> Statement(int fillers = 3, bool withAnchor = true)
    {
        var list = new List<CodeInstruction>();
        if (withAnchor) list.Add(new CodeInstruction(OpCodes.Call, GetAll));
        else list.Add(new CodeInstruction(OpCodes.Ldnull));
        for (var i = 0; i < fillers; i++) list.Add(new CodeInstruction(OpCodes.Nop));
        list.Add(new CodeInstruction(OpCodes.Call, Single));
        list.Add(new CodeInstruction(OpCodes.Pop));
        return list;
    }

    private static List<CodeInstruction> Method(int sites)
    {
        var list = new List<CodeInstruction>();
        for (var i = 0; i < sites; i++) list.AddRange(Statement());
        list.Add(new CodeInstruction(OpCodes.Ret));
        return list;
    }

    [TestMethod]
    public void Rewrite_TwoSites_SwapsEachForItsOwnHelper_InPlace()
    {
        var list = Method(2);

        var result = ConceptLookupRewrite.Rewrite(list, _logger, out var swapped);

        Assert.AreEqual(2, swapped);
        Assert.AreEqual(list.Count, result.Count);
        Assert.AreSame(Helper0, result.First(c => c.operand is MethodInfo m && m.DeclaringType == typeof(ShadowfaxInventoryCalls)).operand);
        Assert.AreSame(Helper1, result.Last(c => c.operand is MethodInfo m && m.DeclaringType == typeof(ShadowfaxInventoryCalls)).operand);
        Assert.AreEqual(0, result.Count(c => c.operand is MethodInfo m && m == Single));
        Assert.AreEqual(2, result.Count(c => c.operand is MethodInfo m && m == GetAll));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_KeepsLabelsOnTheSwappedInstruction()
    {
        var label = new DynamicMethod("t", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel();
        var list = Method(2);
        list[4].labels.Add(label);

        var result = ConceptLookupRewrite.Rewrite(list, _logger, out var swapped);

        Assert.AreEqual(2, swapped);
        CollectionAssert.AreEqual(new[] { label }, result[4].labels);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(3)]
    public void Rewrite_WrongSiteCount_ReturnsTheIlUnmodified_WithOneWarning(int sites)
    {
        var result = ConceptLookupRewrite.Rewrite(Method(sites), _logger, out var swapped);

        Assert.AreEqual(0, swapped);
        Assert.AreEqual(sites, result.Count(c => c.operand is MethodInfo m && m == Single));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_MissingAnchor_Bails()
    {
        var list = Statement();
        list.AddRange(Statement(withAnchor: false));
        list.Add(new CodeInstruction(OpCodes.Ret));

        var result = ConceptLookupRewrite.Rewrite(list, _logger, out var swapped);

        Assert.AreEqual(0, swapped);
        Assert.AreEqual(2, result.Count(c => c.operand is MethodInfo m && m == Single));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_AnchorBeyondTheWindow_Bails()
    {
        var list = Statement(fillers: 40);
        list.AddRange(Statement());
        list.Add(new CodeInstruction(OpCodes.Ret));

        ConceptLookupRewrite.Rewrite(list, _logger, out var swapped);

        Assert.AreEqual(0, swapped);
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_SecondPass_BailsWithoutThrowing()
    {
        var first = ConceptLookupRewrite.Rewrite(Method(2), _logger, out _);
        _logger.ClearReceivedCalls();

        var second = ConceptLookupRewrite.Rewrite(first, _logger, out var swapped);

        Assert.AreEqual(0, swapped);
        Assert.AreEqual(2, second.Count(c => c.operand is MethodInfo m && m.DeclaringType == typeof(ShadowfaxInventoryCalls)));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }
}
