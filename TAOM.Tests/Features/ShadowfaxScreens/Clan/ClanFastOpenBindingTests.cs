using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Core;
using TAOM.Features.ShadowfaxScreens.Clan.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.ShadowfaxScreens.Clan;

/// <summary>
/// Pins what Clan fast open depends on in the installed engine (v1.5.5 when written): the two <c>ClanPartiesVM</c> targets and
/// their patch parameters, the vanilla call chain that builds the party lists three times per open (the reason skipping the
/// inner build is safe and worth it), the header tail of the list build that the RefreshValues finalizer re-renders, and the
/// explicit <c>IGameStateListener.OnActivate</c> the timer binds through the interface map. The IL tests carry
/// <c>RequiresGameIL</c>; a red one after an engine update means the skip must be re-proven before it ships again.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ClanFastOpenBindingTests
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static MethodInfo PartiesMethod(string name) =>
        typeof(ClanPartiesVM).GetMethod(name, Any, null, Type.EmptyTypes, null)!;

    private static List<CodeInstruction> Il(MethodBase method) => PatchProcessor.GetOriginalInstructions(method).ToList();

    private static List<string> CallSequence(MethodBase method) => Il(method)
        .Where(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Newobj) && ci.operand is MethodBase)
        .Select(ci => ((MethodBase)ci.operand).DeclaringType!.Name + "." + ((MethodBase)ci.operand).Name)
        .ToList();

    private static List<string> Literals(MethodBase method) => Il(method)
        .Where(ci => ci.opcode == OpCodes.Ldstr).Select(ci => (string)ci.operand).ToList();

    private static int IndexOf(List<string> calls, string call, int from = 0) => calls.FindIndex(from, c => c == call);

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGame")]
    public void Targets_ClanPartiesVM_RefreshValues_IsAPublicOverrideAndRefreshPartiesListAPublicNonVirtual()
    {
        RequireGame();

        var refreshValues = PartiesMethod("RefreshValues");
        var refreshList = PartiesMethod("RefreshPartiesList");

        Assert.IsNotNull(refreshValues, "ClanPartiesVM.RefreshValues() is not declared on ClanPartiesVM");
        Assert.IsTrue(refreshValues.IsPublic && refreshValues.IsVirtual && !refreshValues.IsStatic);
        Assert.AreEqual(typeof(void), refreshValues.ReturnType);
        Assert.IsNotNull(refreshList, "ClanPartiesVM.RefreshPartiesList() is gone");
        Assert.IsTrue(refreshList.IsPublic && !refreshList.IsVirtual && !refreshList.IsStatic);
        Assert.AreEqual(typeof(void), refreshList.ReturnType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGame")]
    public void PatchMethods_UseNamesHarmonyBinds_AndTheSharedStateType()
    {
        RequireGame();

        var values = typeof(ClanPartiesVM_RefreshValues_ClanFastOpen_Patch);
        var list = typeof(ClanPartiesVM_RefreshPartiesList_ClanFastOpen_Patch);
        var activate = typeof(GauntletClanScreen_OnActivate_ClanFastOpen_Patch);

        Assert.AreEqual("__instance:Object,__state:Object&", Describe(values.GetMethod("Prefix")!));
        Assert.AreEqual("__instance:Object,__exception:Exception,__state:Object", Describe(values.GetMethod("Finalizer")!));
        Assert.AreEqual(typeof(bool), list.GetMethod("Prefix")!.ReturnType, "the list prefix is the one that may skip");
        Assert.AreEqual("__instance:Object,__state:Int64&", Describe(list.GetMethod("Prefix")!));
        Assert.AreEqual("__state:Int64", Describe(list.GetMethod("Finalizer")!));
        Assert.AreEqual("__state:ScreenOpenWindow&", Describe(activate.GetMethod("Prefix")!));
        Assert.AreEqual("__exception:Exception,__state:ScreenOpenWindow", Describe(activate.GetMethod("Finalizer")!));
        foreach (var m in new[] { values, list, activate }.SelectMany(t => new[] { t.GetMethod("Finalizer")! }))
            Assert.AreEqual(typeof(void), m.ReturnType, "a finalizer is void so Harmony keeps rethrow");
    }

    private static string Describe(MethodInfo m) =>
        string.Join(",", m.GetParameters().Select(p => p.Name + ":" + p.ParameterType.Name));

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void ClanPartiesVMCtor_CallsRefreshPartiesListThenRefreshValues()
    {
        RequireGame();

        var ctor = typeof(ClanPartiesVM).GetConstructors(Any).Single();
        var calls = CallSequence(ctor);

        var list = IndexOf(calls, "ClanPartiesVM.RefreshPartiesList");
        // A virtual call to the instance's own RefreshValues is emitted against the root declaration, ViewModel.RefreshValues.
        var values = IndexOf(calls, "ViewModel.RefreshValues");
        Assert.IsTrue(list >= 0 && values > list, "the constructor builds the list, then refreshes values: " + string.Join(" > ", calls));
        Assert.AreEqual(1, calls.Count(c => c == "ClanPartiesVM.RefreshPartiesList"));
        Assert.AreEqual(1, calls.Count(c => c == "ViewModel.RefreshValues"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void RefreshValues_SetsBothHeaderTexts_ThenBuildsTheListOnce()
    {
        RequireGame();

        var calls = CallSequence(PartiesMethod("RefreshValues"));

        var garrisons = IndexOf(calls, "ClanPartiesVM.set_GarrisonsText");
        var caravans = IndexOf(calls, "ClanPartiesVM.set_CaravansText");
        var list = IndexOf(calls, "ClanPartiesVM.RefreshPartiesList");
        Assert.IsTrue(garrisons >= 0 && caravans >= 0, "RefreshValues no longer sets the header texts: " + string.Join(" > ", calls));
        Assert.IsTrue(list > garrisons && list > caravans, "the list build comes after the bare header texts, so a skip leaves them bare");
        Assert.AreEqual(1, calls.Count(c => c == "ClanPartiesVM.RefreshPartiesList"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void RefreshPartiesList_EndsByRenderingTheCaravanAndGarrisonHeaders()
    {
        RequireGame();

        var method = PartiesMethod("RefreshPartiesList");
        var literals = Literals(method);
        var calls = CallSequence(method);

        CollectionAssert.IsSubsetOf(new[] { "str_clan_caravans", "str_clan_garrisons" }, literals);
        var caravans = literals.IndexOf("str_clan_caravans");
        var garrisons = literals.IndexOf("str_clan_garrisons");
        Assert.IsTrue(caravans < garrisons, "caravans first, then garrisons, as the finalizer renders them");
        var setCaravans = IndexOf(calls, "ClanPartiesVM.set_CaravansText");
        var setGarrisons = IndexOf(calls, "ClanPartiesVM.set_GarrisonsText");
        Assert.IsTrue(setCaravans >= 0 && setGarrisons > setCaravans);
        Assert.AreEqual(1, calls.Count(c => c == "ClanPartiesVM.set_CaravansText"));
        Assert.AreEqual(1, calls.Count(c => c == "ClanPartiesVM.set_GarrisonsText"));
        Assert.IsTrue(calls.Count(c => c == "GameTexts.SetVariable") >= 3,
            "CURRENT is set before the parties, caravans and garrisons headers: " + string.Join(" > ", calls));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGame")]
    public void HeaderProperties_ArePublicStringSettersOnClanPartiesVM()
    {
        RequireGame();

        foreach (var name in new[] { "CaravansText", "GarrisonsText" })
        {
            var property = typeof(ClanPartiesVM).GetProperty(name);
            Assert.IsNotNull(property, name);
            Assert.AreEqual(typeof(string), property!.PropertyType);
            Assert.IsNotNull(property.GetSetMethod(), name + " needs a public setter");
        }
        Assert.IsNotNull(typeof(ClanPartiesVM).GetProperty("Caravans")?.GetGetMethod());
        Assert.IsNotNull(typeof(ClanPartiesVM).GetProperty("Garrisons")?.GetGetMethod());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void ClanManagementVM_BuildsThePartiesThreeTimesPerOpen_ThroughTheCtorChain()
    {
        RequireGame();
        var vm = typeof(ClanManagementVM);

        var ctor = vm.GetConstructors(Any).OrderByDescending(c => c.GetParameters().Length).First();
        var ctorCalls = CallSequence(ctor);
        Assert.IsTrue(ctorCalls.Contains("ClanPartiesVM..ctor"), "the ClanManagementVM constructor creates the ClanPartiesVM");
        Assert.IsTrue(ctorCalls.Contains("ClanManagementVM.UpdateKingdomRelatedProperties"));
        Assert.IsTrue(IndexOf(ctorCalls, "ClanPartiesVM..ctor") < IndexOf(ctorCalls, "ClanManagementVM.UpdateKingdomRelatedProperties"),
            "the parties exist before the chain that refreshes them");

        var kingdom = CallSequence(vm.GetMethod("UpdateKingdomRelatedProperties", Any)!);
        Assert.IsTrue(kingdom.Contains("ClanManagementVM.UpdateBannerVisuals"), string.Join(" > ", kingdom));
        var banner = CallSequence(vm.GetMethod("UpdateBannerVisuals", Any)!);
        Assert.IsTrue(banner.Contains("ClanManagementVM.RefreshValues") || banner.Contains("ViewModel.RefreshValues"), string.Join(" > ", banner));

        // RefreshValues reaches the parties through the _clanParties field; the null-conditional call puts a few instructions
        // (dup, brtrue, pop) between the field load and the call.
        var il = Il(vm.GetMethod("RefreshValues", Any)!);
        var viaField = Enumerable.Range(0, il.Count)
            .Where(i => il[i].operand is FieldInfo f && f.Name == "_clanParties")
            .Count(i => il.Skip(i + 1).Take(6).Any(ci => ci.operand is MethodBase m && m.Name == "RefreshValues"));
        Assert.AreEqual(1, viaField, "ClanManagementVM.RefreshValues refreshes the parties once, through _clanParties");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void ClanManagementVM_RefreshCategoryValues_CallsRefreshPartiesListDirectly()
    {
        RequireGame();

        var calls = CallSequence(typeof(ClanManagementVM).GetMethod("RefreshCategoryValues", Any)!);

        Assert.IsTrue(calls.Contains("ClanPartiesVM.RefreshPartiesList"),
            "the category refresh is a direct caller the skip must never touch: " + string.Join(" > ", calls));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGame")]
    public void OnActivate_ResolvesThroughTheInterfaceMap_ToAnExplicitInstanceMethodOfGauntletClanScreen()
    {
        RequireGame();

        var target = GauntletClanScreen_OnActivate_ClanFastOpen_Patch.TargetMethod();

        Assert.IsNotNull(target, "IGameStateListener.OnActivate is not mapped on GauntletClanScreen");
        Assert.AreEqual(AccessTools.TypeByName("SandBox.GauntletUI.GauntletClanScreen"), target!.DeclaringType);
        Assert.IsFalse(target.IsStatic);
        Assert.IsTrue(target.IsPrivate, "an explicit implementation is private, which is why the name string cannot find it");
        Assert.AreEqual(0, target.GetParameters().Length);
        Assert.AreEqual(typeof(void), ((MethodInfo)target).ReturnType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void OnActivate_BuildsTheDataSource_ThenLoadsTheClanMovie()
    {
        RequireGame();

        var target = GauntletClanScreen_OnActivate_ClanFastOpen_Patch.TargetMethod()!;
        var calls = CallSequence(target);

        var create = IndexOf(calls, "GauntletClanScreen.CreateDataSource");
        var load = IndexOf(calls, "GauntletLayer.LoadMovie");
        Assert.IsTrue(create >= 0 && load > create, "the open window should span the VM build and the movie load: " + string.Join(" > ", calls));
        Assert.IsTrue(Literals(target).Contains("ClanScreen"));
    }
}
