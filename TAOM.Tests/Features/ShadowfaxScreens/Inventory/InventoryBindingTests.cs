using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TAOM.Core.Logging;
using TAOM.Features.ShadowfaxScreens.Inventory;
using TAOM.Features.ShadowfaxScreens.Inventory.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.ShadowfaxScreens.Inventory;

/// <summary>
/// Drift guard for the Inventory fast open. Both transpilers soft-fail (a changed shape leaves vanilla with one
/// warning), so an engine bump would silently drop the speed-up; these feed the REAL installed IL through the
/// production rewrites, resolve every target and engine member the patches touch, and pin the facts the name-sort
/// skip relies on: the constructor and AfterReset sort the rows after InitializeInventory, with ItemDescription as
/// the tie-break.
/// </summary>
[TestClass]
public class InventoryBindingTests
{
    private const BindingFlags AllMembers = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static MethodBase InitializeInventory() =>
        AccessTools.Method(typeof(SPInventoryVM), "InitializeInventory", Type.EmptyTypes)
        ?? throw new AssertFailedException("SPInventoryVM.InitializeInventory() did not resolve.");

    private static ConstructorInfo ItemCtor() =>
        AccessTools.Constructor(typeof(SPItemVM), new[]
        {
            typeof(InventoryLogic), typeof(bool), typeof(bool), typeof(InventoryScreenHelper.InventoryMode),
            typeof(ItemRosterElement), typeof(InventoryLogic.InventorySide), typeof(int), typeof(EquipmentIndex?),
        }) ?? throw new AssertFailedException("The 8-argument SPItemVM constructor did not resolve.");

    private static ConstructorInfo InventoryCtor() =>
        AccessTools.Constructor(typeof(SPInventoryVM), new[]
        {
            typeof(InventoryLogic), typeof(bool), typeof(Func<WeaponComponentData, ItemObject.ItemUsageSetFlags>),
        }) ?? throw new AssertFailedException("The 3-argument SPInventoryVM constructor did not resolve.");

    private static List<CodeInstruction> Il(MethodBase method) => PatchProcessor.GetOriginalInstructions(method).ToList();

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void NameSortRewrite_SwapsBothSortSites_InTheInstalledEngine()
    {
        RequireGame();
        var logger = Substitute.For<IModLogger>();

        var rewritten = NameSortRewrite.Rewrite(Il(InitializeInventory()), logger, out var swapped);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(2, swapped);
        Assert.AreEqual(2, rewritten.Count(c => c.operand is MethodInfo m && m == ShadowfaxInventoryCalls.SortByNameMethod));
        Assert.AreEqual(0, rewritten.Count(c => c.Calls(NameSortRewrite.OrderByTarget)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void ConceptLookupRewrite_SwapsBothLookups_InTheInstalledEngine()
    {
        RequireGame();
        var logger = Substitute.For<IModLogger>();

        var rewritten = ConceptLookupRewrite.Rewrite(Il(ItemCtor()), logger, out var swapped);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(2, swapped);
        CollectionAssert.AreEqual(ShadowfaxInventoryCalls.ConceptMethods.ToList(),
            rewritten.Where(c => c.operand is MethodInfo m && m.DeclaringType == typeof(ShadowfaxInventoryCalls))
                .Select(c => (MethodInfo)c.operand).ToList());
        Assert.AreEqual(0, rewritten.Count(c => c.Calls(ConceptLookupRewrite.SingleTarget)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void SecondPassOverTheRealIl_BailsWithoutThrowing()
    {
        RequireGame();
        var logger = Substitute.For<IModLogger>();
        var sort = NameSortRewrite.Rewrite(Il(InitializeInventory()), logger, out _);
        var concept = ConceptLookupRewrite.Rewrite(Il(ItemCtor()), logger, out _);
        logger.ClearReceivedCalls();

        NameSortRewrite.Rewrite(sort, logger, out var sortAgain);
        ConceptLookupRewrite.Rewrite(concept, logger, out var conceptAgain);

        Assert.AreEqual(0, sortAgain);
        Assert.AreEqual(0, conceptAgain);
        logger.Received(2).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void EveryRealSite_HasAHelperWithTheSameStackShape()
    {
        RequireGame();
        var orderBy = NameSortRewrite.OrderByTarget;
        var sort = ShadowfaxInventoryCalls.SortByNameMethod;
        CollectionAssert.AreEqual(orderBy.GetParameters().Select(p => p.ParameterType).ToList(),
            sort.GetParameters().Select(p => p.ParameterType).ToList());
        var toArrayParameter = NameSortRewrite.ToArrayTarget.GetParameters().Single().ParameterType;
        Assert.IsTrue(toArrayParameter.IsAssignableFrom(sort.ReturnType),
            "The sort helper's result must be accepted where ToArray<ItemRosterElement> reads its source.");

        var single = ConceptLookupRewrite.SingleTarget;
        foreach (var helper in ShadowfaxInventoryCalls.ConceptMethods)
        {
            CollectionAssert.AreEqual(single.GetParameters().Select(p => p.ParameterType).ToList(),
                helper.GetParameters().Select(p => p.ParameterType).ToList(), helper.Name);
            Assert.AreEqual(single.ReturnType, helper.ReturnType, helper.Name);
            Assert.IsTrue(helper.IsStatic);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchTargets_ResolveWithTheParameterNamesTheHooksInject()
    {
        RequireGame();
        var screen = AccessTools.TypeByName("SandBox.GauntletUI.GauntletInventoryScreen");
        Assert.IsNotNull(screen, "GauntletInventoryScreen did not resolve.");
        var onReady = AccessTools.Method(screen, "OnReady");
        Assert.IsNotNull(onReady, "GauntletInventoryScreen.OnReady did not resolve.");
        var layer = AccessTools.Field(screen, "_gauntletLayer");
        Assert.IsNotNull(layer, "GauntletInventoryScreen._gauntletLayer did not resolve.");
        Assert.AreEqual(typeof(GauntletLayer), layer.FieldType);

        var prefix = typeof(GauntletInventoryScreen_OnReady_ShadowfaxPatch).GetMethod("Prefix")!;
        CollectionAssert.AreEqual(new[] { "____gauntletLayer", "__state" }, prefix.GetParameters().Select(p => p.Name).ToList());
        var finalizer = typeof(GauntletInventoryScreen_OnReady_ShadowfaxPatch).GetMethod("Finalizer")!;
        CollectionAssert.AreEqual(new[] { "__exception", "__state" }, finalizer.GetParameters().Select(p => p.Name).ToList());

        var rowsPrefix = typeof(SPInventoryVM_InitializeInventory_ShadowfaxPatch).GetMethod("Prefix")!;
        CollectionAssert.AreEqual(new[] { "__state" }, rowsPrefix.GetParameters().Select(p => p.Name).ToList());
        var rowsFinalizer = typeof(SPInventoryVM_InitializeInventory_ShadowfaxPatch).GetMethod("Finalizer")!;
        CollectionAssert.AreEqual(new[] { "__state" }, rowsFinalizer.GetParameters().Select(p => p.Name).ToList());

        Assert.IsNotNull(InitializeInventory());
        Assert.IsNotNull(ItemCtor());
        Assert.IsNotNull(InventoryCtor());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void InitializeInventory_IsCalledOnlyByTheConstructorAndAfterReset()
    {
        RequireGame();
        var target = InitializeInventory();
        var callers = new List<string>();
        var bodies = typeof(SPInventoryVM).GetMethods(AllMembers).Cast<MethodBase>()
            .Concat(typeof(SPInventoryVM).GetConstructors(AllMembers));

        foreach (var method in bodies.Where(m => m.DeclaringType == typeof(SPInventoryVM) && !m.IsAbstract))
        {
            List<CodeInstruction> il;
            try { il = Il(method); }
            catch { continue; }
            if (il.Any(c => c.Calls((MethodInfo)target))) callers.Add(method.IsConstructor ? ".ctor" : method.Name);
        }

        CollectionAssert.AreEquivalent(new[] { ".ctor", "AfterReset" }, callers.Distinct().ToList(),
            "A new caller of InitializeInventory must also sort its rows, or the name-sort skip shows an unsorted list.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void ConstructorAndAfterReset_SortTheRowsAfterInitializeInventory()
    {
        RequireGame();
        var initialize = (MethodInfo)InitializeInventory();
        var sortByDefault = AccessTools.Method(typeof(SPInventorySortControllerVM), "SortByDefaultState");
        Assert.IsNotNull(sortByDefault, "SPInventorySortControllerVM.SortByDefaultState did not resolve.");
        var afterReset = AccessTools.Method(typeof(SPInventoryVM), "AfterReset");
        Assert.IsNotNull(afterReset, "SPInventoryVM.AfterReset did not resolve.");

        foreach (var method in new MethodBase[] { InventoryCtor(), afterReset })
        {
            var il = Il(method);
            var init = il.FindIndex(c => c.Calls(initialize));
            var sort = il.FindLastIndex(c => c.Calls(sortByDefault));
            Assert.IsTrue(init >= 0, method.Name + " no longer calls InitializeInventory.");
            Assert.IsTrue(sort > init, method.Name + " no longer sorts the rows after InitializeInventory.");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void ItemComparerTieBreak_StillReadsItemDescription()
    {
        RequireGame();
        var comparer = AccessTools.Inner(typeof(SPInventorySortControllerVM), "ItemComparer");
        Assert.IsNotNull(comparer, "SPInventorySortControllerVM.ItemComparer did not resolve.");
        var resolve = comparer.GetMethod("ResolveEquality", AllMembers);
        Assert.IsNotNull(resolve, "ItemComparer.ResolveEquality did not resolve.");

        var getter = AccessTools.PropertyGetter(typeof(SPItemVM), "ItemDescription");
        Assert.IsNotNull(getter, "SPItemVM.ItemDescription did not resolve.");
        Assert.IsTrue(Il(resolve).Count(c => c.operand is MethodInfo m && m.MetadataToken == getter.MetadataToken && m.Module == getter.Module) >= 2,
            "ResolveEquality no longer compares both rows' ItemDescription.");
    }
}
