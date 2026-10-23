using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.ArmyTargeting.HoldTheWalls;

/// <summary>
/// #871: the fort-hold adapter is engine-bound, so its IL is the testable surface. These pin the facts the plan's engine
/// checks (v1.5.5) depend on: the adapter keeps no engine object, its only writes are the two AI-pause calls the engine
/// itself makes on a capturing army leader, and its food reading does not use the engine's int-cast day count (a party that
/// eats nothing divides by zero and reads as int.MinValue there).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FortHoldAdapterIlTests
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly string[] ReadOnlyEngineCalls = { "Find", "GetTraitLevel" };

    private static IEnumerable<MethodBase> AdapterMethods() =>
        typeof(FortHoldAdapter).GetMethods(All).Cast<MethodBase>().Concat(typeof(FortHoldAdapter).GetConstructors(All))
            .Where(m => m.GetMethodBody() != null);

    private static MethodBase[] CallsOf(MethodBase method) =>
        IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToArray();

    private static MethodBase[] CallsOf(string methodName) => CallsOf(typeof(FortHoldAdapter).GetMethod(methodName, All)!);

    private static bool IsEngine(MethodBase m) =>
        m.DeclaringType?.Namespace != null && m.DeclaringType.Namespace.StartsWith("TaleWorlds", System.StringComparison.Ordinal);

    [TestMethod]
    public void TheAdapter_KeepsNoFieldAtAll_SoNoCampaignOutlivesItsSave()
    {
        var fields = typeof(FortHoldAdapter).GetFields(All);

        Assert.AreEqual(0, fields.Length, string.Join(", ", fields.Select(f => f.Name + ": " + f.FieldType.Name)));
    }

    [TestMethod]
    public void HoldForHours_CallsDisableForHours_AndResumeAi_CallsEnableAi()
    {
        Assert.IsTrue(CallsOf(nameof(FortHoldAdapter.HoldForHours)).Any(m => m.DeclaringType == typeof(MobilePartyAi) && m.Name == nameof(MobilePartyAi.DisableForHours)));
        Assert.IsTrue(CallsOf(nameof(FortHoldAdapter.ResumeAi)).Any(m => m.DeclaringType == typeof(MobilePartyAi) && m.Name == nameof(MobilePartyAi.EnableAi)));
    }

    [TestMethod]
    public void TheOnlyEngineCallsTheAdapterMakes_AreGetters_Lookups_AndTheTwoPauseCalls()
    {
        var offenders = AdapterMethods()
            .SelectMany(m => CallsOf(m).Select(c => (Method: m.Name, Call: c)))
            .Where(x => IsEngine(x.Call))
            .Where(x => !(x.Call.Name.StartsWith("get_", System.StringComparison.Ordinal) || ReadOnlyEngineCalls.Contains(x.Call.Name)
                          || (x.Call.DeclaringType == typeof(MobilePartyAi) && (x.Call.Name == "DisableForHours" || x.Call.Name == "EnableAi"))))
            .Select(x => x.Method + " calls " + x.Call.DeclaringType!.Name + "." + x.Call.Name)
            .Distinct()
            .ToList();

        Assert.AreEqual(0, offenders.Count, string.Join("; ", offenders));
    }

    [TestMethod]
    public void NoWriteIsMadeOutsideTheTwoPauseMethods()
    {
        var writers = AdapterMethods()
            .Where(m => CallsOf(m).Any(c => c.DeclaringType == typeof(MobilePartyAi) && (c.Name == "DisableForHours" || c.Name == "EnableAi")))
            .Select(m => m.Name)
            .OrderBy(n => n)
            .ToList();

        CollectionAssert.AreEqual(new[] { nameof(FortHoldAdapter.HoldForHours), nameof(FortHoldAdapter.ResumeAi) }, writers);
    }

    [TestMethod]
    public void NoEngineSetter_IsCalledAnywhere_SoNoCohesionFoodOrBehaviorObjectIsWritten()
    {
        var setters = AdapterMethods()
            .SelectMany(m => CallsOf(m))
            .Where(c => IsEngine(c) && c.Name.StartsWith("set_", System.StringComparison.Ordinal))
            .Select(c => c.DeclaringType!.Name + "." + c.Name)
            .Distinct()
            .ToList();

        Assert.AreEqual(0, setters.Count, string.Join(", ", setters));
    }

    [TestMethod]
    public void TheFoodReading_UsesTheFoodChangeAndTheRoster_NotTheEnginesIntCastDayCount()
    {
        var calls = AdapterMethods().SelectMany(m => CallsOf(m)).ToList();

        Assert.IsFalse(calls.Any(c => c.Name == nameof(MobileParty.GetNumDaysForFoodToLast)), "(int)infinity is int.MinValue");
        Assert.IsTrue(calls.Any(c => c.DeclaringType == typeof(MobileParty) && c.Name == "get_" + nameof(MobileParty.FoodChange)));
        Assert.IsTrue(calls.Any(c => c.Name == "get_TotalFood"));
    }

    [TestMethod]
    public void TheCapturingArmy_IsFoundThroughTheFortressesParties_NotThroughTheCapturerHero()
    {
        var calls = AdapterMethods().SelectMany(m => CallsOf(m)).ToList();

        Assert.IsTrue(calls.Any(c => c.DeclaringType == typeof(TaleWorlds.CampaignSystem.Settlements.Settlement) && c.Name == "get_Parties"));
        Assert.IsFalse(calls.Any(c => c.Name == "get_PartyBelongedTo"), "the owner change names a hero, which is not the army's leader");
    }

    [TestMethod]
    public void TheSide_IsBuiltByTheSharedPairBuilder()
    {
        var calls = CallsOf("Read");

        Assert.IsTrue(calls.Any(c => c.DeclaringType != null && c.DeclaringType.Name == "FactionSides" && c.Name == "Of"));
        Assert.IsTrue(calls.Any(c => c.DeclaringType == typeof(Army) && c.Name == "get_" + nameof(Army.Cohesion)));
    }
}
