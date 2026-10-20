using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// The engine facts the phase 2 tooltip design rests on (v1.5.5 at the time of writing), one pin each. A new caller, a new
/// path that evaluates a row function outside the brackets, or a reordered builder turns one of these red, and the design in
/// docs/features/hidden-hosts.md has to be read again. The SandBox types are resolved by name: the test project references
/// the TaleWorlds assemblies only.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HiddenHostsTooltipBindingTests
{
    private const BindingFlags AllMembers =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly string[] ScannedAssemblyNames =
    {
        "TaleWorlds.CampaignSystem", "TaleWorlds.CampaignSystem.ViewModelCollection", "TaleWorlds.Core.ViewModelCollection",
        "TaleWorlds.MountAndBlade", "TaleWorlds.MountAndBlade.ViewModelCollection", "TaleWorlds.MountAndBlade.GauntletUI",
        "SandBox", "SandBox.View", "SandBox.ViewModelCollection", "SandBox.GauntletUI",
        "StoryMode", "StoryMode.View", "StoryMode.ViewModelCollection", "StoryMode.GauntletUI",
    };

    private static bool _gameLoaded;
    private static Dictionary<string, SortedSet<string>>? _callers;
    private static int _scannedAssemblies;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Type Named(string fullName)
    {
        var type = AccessTools.TypeByName(fullName);
        Assert.IsNotNull(type, fullName + " did not resolve.");
        return type;
    }

    private static Type Refresher => Named("TaleWorlds.CampaignSystem.ViewModelCollection.TooltipRefresherCollection");

    private static Type TooltipPropertyType => Named("TaleWorlds.Core.ViewModelCollection.Information.TooltipProperty");

    private static Type PropertyTooltipType => Named("TaleWorlds.Core.ViewModelCollection.Information.PropertyBasedTooltipVM");

    private static List<CodeInstruction> Il(MethodBase method) => PatchProcessor.GetOriginalInstructions(method);

    private static IEnumerable<MethodBase> CalledMethods(MethodBase method) =>
        Il(method).Where(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Newobj) && ci.operand is MethodBase)
            .Select(ci => (MethodBase)ci.operand);

    private static IEnumerable<MethodBase> BodiesWithNested(Type type)
    {
        foreach (var m in type.GetMembers(AllMembers).OfType<MethodBase>().Where(m => m.GetMethodBody() != null))
            yield return m;
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            foreach (var m in BodiesWithNested(nested))
                yield return m;
    }

    private static MethodInfo Method(Type owner, string name, int parameterCount = -1) =>
        owner.GetMethods(AllMembers).Single(m => m.Name == name && (parameterCount < 0 || m.GetParameters().Length == parameterCount));

    private static string TopLevel(Type? type)
    {
        while (type?.DeclaringType != null) type = type.DeclaringType;
        return type?.Name ?? "?";
    }

    /// <summary>One pass over every method body of the engine assemblies the design names, recording who calls the members it cares about.</summary>
    private static Dictionary<string, SortedSet<string>> Callers()
    {
        if (_callers != null) return _callers;

        var result = new Dictionary<string, SortedSet<string>>
        {
            ["TooltipProperty.RefreshValue"] = new(),
            ["GetPartyNameplateText(MobileParty,bool)"] = new(),
            ["GetPartyNameplateText(PartyBase)"] = new(),
            ["GetArmyManCountTooltip"] = new(),
        };

        var assemblies = new List<Assembly>();
        foreach (var name in ScannedAssemblyNames)
        {
            var found = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name);
            if (found == null)
            {
                try { found = Assembly.Load(name); }
                catch (Exception) { /* not installed: the pin that needs it fails by name below */ }
            }
            if (found != null) assemblies.Add(found);
        }
        _scannedAssemblies = assemblies.Count;

        foreach (var assembly in assemblies)
        foreach (var method in IlCallScanner.EnumerateMethods(assembly))
        {
            byte[]? il;
            try { il = method.GetMethodBody()?.GetILAsByteArray(); }
            catch (Exception) { continue; }
            if (il == null) continue;

            foreach (var called in IlCallScanner.ExtractCalledMethods(method, il))
            {
                var owner = called.DeclaringType;
                string? key = null;
                if (called.Name == "RefreshValue" && owner?.Name == "TooltipProperty")
                    key = "TooltipProperty.RefreshValue";
                else if (called.Name == "GetArmyManCountTooltip" && owner?.Name == "CampaignUIHelper")
                    key = "GetArmyManCountTooltip";
                else if (called.Name == "GetPartyNameplateText" && owner?.Name == "CampaignUIHelper")
                {
                    var p = called.GetParameters();
                    key = p.Length == 2 && p[0].ParameterType.Name == "MobileParty"
                        ? "GetPartyNameplateText(MobileParty,bool)"
                        : p.Length == 1 && p[0].ParameterType.Name == "PartyBase" ? "GetPartyNameplateText(PartyBase)" : null;
                }

                if (key != null)
                    result[key].Add(TopLevel(method.DeclaringType) + "." + (method.DeclaringType!.IsNested ? "(nested)" : method.Name));
            }
        }

        _callers = result;
        return result;
    }

    // ---- F1: the row functions re-run outside the builders

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void OnPeriodicRefresh_RefreshesEveryRowsDefinitionAndValue()
    {
        RequireGame();
        var method = PropertyTooltipType.GetMethod("OnPeriodicRefresh", AllMembers)!;

        var calls = CalledMethods(method).Select(m => m.DeclaringType!.Name + "." + m.Name).ToList();

        CollectionAssert.IsSubsetOf(new[] { "TooltipProperty.RefreshDefinition", "TooltipProperty.RefreshValue" }, calls, string.Join(", ", calls));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void RefreshValue_IsCalledOnlyByOnPeriodicRefreshAndTheTooltipPropertyItself()
    {
        RequireGame();

        var callers = Callers()["TooltipProperty.RefreshValue"];

        Assert.IsTrue(_scannedAssemblies >= 8, "the scan covered " + _scannedAssemblies + " assemblies");
        Assert.IsTrue(callers.Contains("PropertyBasedTooltipVM.OnPeriodicRefresh"), "callers: " + string.Join(", ", callers));
        Assert.IsTrue(callers.All(c => c.StartsWith("TooltipProperty.") || c == "PropertyBasedTooltipVM.OnPeriodicRefresh"),
            "another path evaluates a row function outside the brackets: " + string.Join(", ", callers));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void TooltipBaseVM_HoldsTheInvokedTypeAndArgumentsTheBracketReads()
    {
        RequireGame();
        var tooltipBase = Named("TaleWorlds.Library.TooltipBaseVM");

        var type = tooltipBase.GetField("_invokedType", AllMembers);
        var args = tooltipBase.GetField("_invokedArgs", AllMembers);

        Assert.AreEqual(typeof(Type), type?.FieldType, "_invokedType");
        Assert.AreEqual(typeof(object[]), args?.FieldType, "_invokedArgs");
    }

    // ---- The builders' shapes

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void AddPartyTroopProperties_OnlyAppends_AndOpensWithABlankSpacerThenTheTitle()
    {
        RequireGame();
        var method = Method(Refresher, "AddPartyTroopProperties");

        var calls = CalledMethods(method).ToList();
        var addProperties = calls.Where(m => m.Name == "AddProperty").ToList();

        Assert.IsFalse(calls.Any(m => m.Name == "Insert" || m.Name == "RemoveAt" || m.Name == "Remove" || m.Name == "Clear"),
            "the trim assumes rows are only appended");
        Assert.IsTrue(addProperties.Count >= 2);
        Assert.AreEqual(typeof(string), addProperties[0].GetParameters()[1].ParameterType, "the first row is the blank spacer, a string row");
        Assert.AreEqual("Func`1", addProperties[1].GetParameters()[1].ParameterType.Name, "the second row is the title, whose value is a function");
        Assert.AreEqual(typeof(bool), method.GetParameters()[3].ParameterType);
        Assert.AreEqual("isInspected", method.GetParameters()[3].Name);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void EachAddEncounterParties_ReadsItsIsExtendedArgumentExactlyOnce()
    {
        RequireGame();
        var overloads = Refresher.GetMethods(AllMembers).Where(m => m.Name == "AddEncounterParties").ToList();
        Assert.AreEqual(2, overloads.Count);

        foreach (var method in overloads)
        {
            Assert.AreEqual("isExtended", method.GetParameters()[3].Name);
            var il = Il(method);
            int loads = il.Count(ci => ci.opcode == OpCodes.Ldarg_3
                                       || ((ci.opcode == OpCodes.Ldarg || ci.opcode == OpCodes.Ldarg_S || ci.opcode == OpCodes.Ldarga || ci.opcode == OpCodes.Ldarga_S)
                                           && ci.operand is int index && index == 3)
                                       || ((ci.opcode == OpCodes.Ldarg_S || ci.opcode == OpCodes.Ldarga_S) && ci.operand is byte b && b == 3));
            Assert.AreEqual(1, loads, method + ": isExtended gates only the per-troop rows block");
            Assert.IsFalse(il.Any(ci => ci.opcode == OpCodes.Starg || ci.opcode == OpCodes.Starg_S), "the argument is never reassigned");
        }
    }

    [DataTestMethod]
    [DataRow("RefreshMobilePartyTooltip")]
    [DataRow("RefreshArmyTooltip")]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PartyAndArmyBuilders_AddTheTitleFlaggedRowFirst(string builder)
    {
        RequireGame();
        var il = Il(Method(Refresher, builder));
        const int title = 0x1000;

        var firstTwoAdds = il.Select((ci, i) => (ci, i))
            .Where(x => x.ci.operand is MethodBase m && m.Name == "AddProperty" && m.GetParameters().Length == 4)
            .Take(2)
            .ToList();

        Assert.AreEqual(2, firstTwoAdds.Count, "the development-mode and the ordinary title rows");
        foreach (var (call, index) in firstTwoAdds)
        {
            var flag = il[index - 1];
            Assert.AreEqual(title, ToInt(flag), builder + ": the argument before the call is the Title flag");
        }
    }

    private static int ToInt(CodeInstruction ci)
    {
        if (ci.opcode == OpCodes.Ldc_I4 && ci.operand is int i) return i;
        if (ci.opcode == OpCodes.Ldc_I4_S && ci.operand is sbyte sb) return sb;
        return -1;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void SettlementBuilder_AddsADefendersRowWithAPlainStringValue_AndMirrorsTheMembersTheAdapterReads()
    {
        RequireGame();
        var builder = Method(Refresher, "RefreshSettlementTooltip");
        var il = Il(builder);

        int ldstr = il.FindIndex(ci => ci.opcode == OpCodes.Ldstr && (string)ci.operand == "str_map_tooltip_defenders");
        Assert.IsTrue(ldstr >= 0, "the defenders label is the game text id str_map_tooltip_defenders");
        var add = il.Skip(ldstr).Take(14).Select(ci => ci.operand).OfType<MethodBase>().First(m => m.Name == "AddProperty");
        Assert.AreEqual(typeof(string), add.GetParameters()[1].ParameterType, "the value is a string, so a periodic refresh never rewrites it");

        var called = BodiesWithNested(Refresher).SelectMany(CalledMethods).Select(m => m.DeclaringType!.Name + "." + m.Name).ToHashSet();
        foreach (var member in new[]
                 {
                     "DiplomacyHelper.IsSameFactionAndNotEliminated", "MobileParty.get_IsLordParty", "MobileParty.get_IsMilitia",
                     "MobileParty.get_IsGarrison", "PartyBase.get_NumberOfHealthyMembers",
                     "CampaignUIHelper.GetPartyNameplateText",
                 })
            Assert.IsTrue(called.Contains(member), member + " is read by the settlement builder: the adapter's defenders mirror follows it");

        // Town.InRebelliousState is a public field, not a property.
        Assert.IsTrue(il.Any(ci => ci.opcode == OpCodes.Ldfld && ci.operand is FieldInfo field && field.Name == "InRebelliousState" && field.DeclaringType!.Name == "Town"),
            "the militia rule reads Town.InRebelliousState");
    }

    // ---- Who calls what

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PartyNameplateText_IsCalledBy_TheSettlementBuilderAndTheArmyMenuOverlayOnly()
    {
        RequireGame();
        var callers = Callers();
        var mobile = callers["GetPartyNameplateText(MobileParty,bool)"];

        Assert.IsTrue(mobile.Contains("TooltipRefresherCollection.RefreshSettlementTooltip"), "callers: " + string.Join(", ", mobile));
        Assert.IsTrue(mobile.Any(c => c.StartsWith("ArmyMenuOverlayVM.")), "callers: " + string.Join(", ", mobile));
        Assert.IsTrue(mobile.All(c => c == "TooltipRefresherCollection.RefreshSettlementTooltip" || c.StartsWith("ArmyMenuOverlayVM.")),
            "a new caller of the MobileParty overload: " + string.Join(", ", mobile));
        Assert.IsTrue(callers["GetPartyNameplateText(PartyBase)"].All(c => c.StartsWith("MapInfoVM.")),
            "a new caller of the PartyBase overload: " + string.Join(", ", callers["GetPartyNameplateText(PartyBase)"]));
        Assert.IsTrue(callers["GetArmyManCountTooltip"].All(c => c.StartsWith("ArmyMenuOverlayVM.")),
            "a new caller of GetArmyManCountTooltip: " + string.Join(", ", callers["GetArmyManCountTooltip"]));
    }

    // ---- The number helper and the registration

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void GetPartySizeText_KeepsItsThreeParameterNames()
    {
        RequireGame();
        var helper = Named("Helpers.PartyBaseHelper");
        var method = helper.GetMethod("GetPartySizeText", new[] { typeof(int), typeof(int), typeof(bool) });

        Assert.IsNotNull(method);
        CollectionAssert.AreEqual(new[] { "healtyNumber", "woundedNumber", "isInspected" }, method!.GetParameters().Select(p => p.Name).ToArray(),
            "Harmony binds the postfix's parameters by these names");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void RegisterTooltipTypes_BindsTheFiveBuildersToTheirSubjectTypes()
    {
        RequireGame();
        var sub = Named("SandBox.View.SandBoxViewSubModule");
        var method = sub.GetMethod("RegisterTooltipTypes", AllMembers)!;

        var bound = new Dictionary<string, string>();
        string? lastBuilder = null;
        foreach (var ci in Il(method))
        {
            if (ci.opcode == OpCodes.Ldftn && ci.operand is MethodBase ftn) lastBuilder = ftn.Name;
            else if ((ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand is MethodInfo mi && mi.Name == "RegisterTooltip")
                bound[mi.GetGenericArguments()[0].Name] = lastBuilder ?? "?";
        }

        Assert.AreEqual("RefreshMobilePartyTooltip", bound["MobileParty"]);
        Assert.AreEqual("RefreshArmyTooltip", bound["Army"]);
        Assert.AreEqual("RefreshSettlementTooltip", bound["Settlement"]);
        Assert.AreEqual("RefreshMapEventTooltip", bound["MapEvent"]);
        Assert.AreEqual("RefreshSiegeEventTooltip", bound["SiegeEvent"]);
    }

    // ---- The one place a rebuild happens

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void TheConstructorAndOnIsExtendedChanged_BothRebuildThroughTheRegisteredRefresher()
    {
        RequireGame();
        var refresh = PropertyTooltipType.GetMethod("Refresh", AllMembers)!;
        var invoke = CalledMethods(refresh).Select(m => m.Name).ToList();

        CollectionAssert.Contains(invoke, "InvokeRefreshData");
        var ctorCalls = PropertyTooltipType.GetConstructors().SelectMany(CalledMethods).Select(m => m.Name).ToList();
        var changedCalls = CalledMethods(PropertyTooltipType.GetMethod("OnIsExtendedChanged", AllMembers)!).Select(m => m.Name).ToList();
        CollectionAssert.Contains(ctorCalls, "Refresh");
        CollectionAssert.Contains(changedCalls, "Refresh");
    }
}
