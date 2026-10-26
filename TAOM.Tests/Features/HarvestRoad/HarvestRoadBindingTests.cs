using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TAOM.Composition;
using TAOM.Features.HarvestRoad;
using TAOM.Features.HarvestRoad.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HarvestRoad;

/// <summary>
/// Pins the vanilla seams Harvest Road rides (#912) against the installed engine, with no campaign: both targets resolve
/// with the parameter names Harmony injects by, the dispatcher entry point still takes the list shape the nameplate reads,
/// every engine member the patch bodies name still exists (a body that cannot JIT escapes its own try block), and the
/// category attaches exactly one prefix and one postfix to each target.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HarvestRoadBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static MethodInfo SaleTarget()
    {
        RequireGame();
        var method = AccessTools.Method(typeof(SellGoodsForTradeAction), "ApplyByVillagerTrade", new[] { typeof(Settlement), typeof(MobileParty) });
        Assert.IsNotNull(method, "SellGoodsForTradeAction.ApplyByVillagerTrade(Settlement, MobileParty) did not resolve.");
        return method!;
    }

    private static MethodInfo FoodTarget()
    {
        RequireGame();
        var method = AccessTools.Method(typeof(PartiesBuyFoodCampaignBehavior), "BuyFoodInternal", new[] { typeof(MobileParty), typeof(Settlement), typeof(int) });
        Assert.IsNotNull(method, "PartiesBuyFoodCampaignBehavior.BuyFoodInternal(MobileParty, Settlement, int) did not resolve.");
        return method!;
    }

    private static string[] Names(MethodInfo method) => method.GetParameters().Select(p => p.Name!).ToArray();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheSaleTarget_IsThePublicStaticVoidVillagerEntryPoint_InInstalledEngine()
    {
        var target = SaleTarget();

        Assert.IsTrue(target.IsStatic);
        Assert.IsTrue(target.IsPublic);
        Assert.AreEqual(typeof(void), target.ReturnType);
        CollectionAssert.AreEqual(new[] { "settlement", "villagerParty" }, Names(target));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheFoodTarget_IsThePrivateInstanceVoidBuyer_InInstalledEngine()
    {
        var target = FoodTarget();

        Assert.IsFalse(target.IsStatic);
        Assert.IsTrue(target.IsPrivate);
        Assert.AreEqual(typeof(void), target.ReturnType);
        CollectionAssert.AreEqual(new[] { "mobileParty", "settlement", "numberOfFoodItemsNeededToBuy" }, Names(target));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ThePatchMethods_DeclareTheNamesHarmonyInjectsBy()
    {
        var sale = typeof(HarvestRoadPatches.VillagerSalePatch);
        var food = typeof(HarvestRoadPatches.FoodPurchasePatch);

        CollectionAssert.AreEqual(new[] { "settlement", "villagerParty", "__state" }, Names(sale.GetMethod("Prefix")!));
        CollectionAssert.AreEqual(new[] { "settlement", "villagerParty", "__state" }, Names(sale.GetMethod("Postfix")!));
        CollectionAssert.AreEqual(new[] { "mobileParty", "settlement", "numberOfFoodItemsNeededToBuy", "__state" }, Names(food.GetMethod("Prefix")!));
        CollectionAssert.AreEqual(new[] { "mobileParty", "settlement", "__state" }, Names(food.GetMethod("Postfix")!));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheDispatcherEntryPoint_StillTakesPartyTownAndTheSignedLineList_InInstalledEngine()
    {
        RequireGame();
        var method = typeof(CampaignEventDispatcher).GetMethod("OnCaravanTransactionCompleted", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(method, "CampaignEventDispatcher.OnCaravanTransactionCompleted is gone: the notices have no entry point.");

        CollectionAssert.AreEqual(
            new[] { typeof(MobileParty), typeof(Town), typeof(List<(EquipmentElement, int)>) },
            method!.GetParameters().Select(p => p.ParameterType).ToArray());
        Assert.IsNotNull(typeof(CampaignEventDispatcher).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheEngineMembersThePatchBodiesName_StillExist_InInstalledEngine()
    {
        RequireGame();

        Assert.IsNotNull(typeof(MobileParty).GetProperty("IsMainParty"));
        Assert.IsNotNull(typeof(MobileParty).GetProperty("ItemRoster"));
        Assert.IsNotNull(typeof(Settlement).GetField("Town"), "Settlement.Town is a public field in v1.5.5.");
        Assert.IsNotNull(typeof(ItemRoster).GetProperty("Count"));
        Assert.IsNotNull(typeof(ItemRoster).GetMethod("GetElementCopyAtIndex", new[] { typeof(int) }));
        Assert.IsNotNull(typeof(ItemRosterElement).GetProperty("Amount"));
        Assert.IsNotNull(typeof(ItemRosterElement).GetProperty("EquipmentElement"));
        Assert.IsTrue(typeof(EquipmentElement).IsValueType);
        // The roster key: value equality over item and modifier, through the object overrides, so two modifiers of one item stay two lines.
        Assert.AreEqual(typeof(EquipmentElement), typeof(EquipmentElement).GetMethod("Equals", new[] { typeof(object) })!.DeclaringType);
        Assert.AreEqual(typeof(EquipmentElement), typeof(EquipmentElement).GetMethod("GetHashCode", Type.EmptyTypes)!.DeclaringType);
    }

    // Runs the engine's Equals, so it needs the real game DLLs: a reference assembly has `throw null` bodies.
    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheDiffKey_ComparesByValue_InInstalledEngine()
    {
        RequireGame();

        Assert.IsTrue(default(EquipmentElement).Equals((object)default(EquipmentElement)));
    }

    // The feature's premise: the only listener draws the pop-up for any party. A caravan check added there by an engine
    // update would silence every Harvest Road notice while the rest of this file stays green.
    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheNameplateListener_NeverAsksIsCaravan_InInstalledEngine()
    {
        RequireGame();
        var vm = AccessTools.TypeByName("SandBox.ViewModelCollection.Nameplate.NameplateNotifications.SettlementNotificationTypes.SettlementNameplateNotificationsVM");
        Assert.IsNotNull(vm, "SettlementNameplateNotificationsVM did not resolve.");
        var handler = AccessTools.Method(vm, "OnCaravanTransactionCompleted");
        Assert.IsNotNull(handler, "The nameplate no longer listens for caravan trades.");

        Assert.IsFalse(CallsByName(handler, "get_IsCaravan"), "The nameplate now filters to caravans: Harvest Road's notices would never show.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void PatchCategory_AppliedToTheInstalledEngine_AttachesOnePrefixAndOnePostfixToEachTarget_AndTheJitAccepts()
    {
        const string id = "taom.tests.harvestroad.jit";
        var targets = new[] { SaleTarget(), FoodTarget() };
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(HarvestRoadModule).Assembly).Apply(harmony, HarvestRoadModule.PatchCategory);

            foreach (var target in targets)
            {
                var info = Harmony.GetPatchInfo(target);
                Assert.IsNotNull(info, target.Name + " was not patched: the class is not in the category");
                Assert.AreEqual(1, info!.Prefixes.Count(p => p.owner == id), target.Name + " prefix count");
                Assert.AreEqual(1, info.Postfixes.Count(p => p.owner == id), target.Name + " postfix count");
                Assert.AreEqual(0, info.Finalizers.Count(p => p.owner == id), target.Name + " finalizer count");
                RuntimeHelpers.PrepareMethod(target.MethodHandle);
            }
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [DataTestMethod]
    [DataRow(typeof(HarvestRoadPatches.VillagerSalePatch))]
    [DataRow(typeof(HarvestRoadPatches.FoodPurchasePatch))]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void ThePatchBodies_StillCallTheDispatcher_AndTheSnapshotHelper(Type patch)
    {
        RequireGame();
        var postfix = patch.GetMethod("Postfix")!;
        var prefix = patch.GetMethod("Prefix")!;

        Assert.IsTrue(CallsByName(postfix, "RaiseTradeNotice"), patch.Name + " postfix no longer reaches the raise helper.");
        Assert.IsTrue(CallsByName(prefix, "SnapshotItems"), patch.Name + " prefix no longer takes a snapshot.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheRaiseHelper_CallsOnCaravanTransactionCompleted()
    {
        RequireGame();
        var raise = typeof(HarvestRoadPatches).GetMethod("RaiseTradeNotice", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;

        Assert.IsTrue(CallsByName(raise, "OnCaravanTransactionCompleted"));
    }

    private static bool CallsByName(MethodBase body, string name) =>
        PatchProcessor.GetOriginalInstructions(body).Any(ci => ci.operand is MethodInfo mi && mi.Name == name);
}
