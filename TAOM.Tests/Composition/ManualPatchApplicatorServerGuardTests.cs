using TAOM.Tests.Migration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Composition;

/// <summary>
/// ADR-013 B6c: <c>ManualPatchApplicator.ApplyAll</c> runs on a dedicated server, so its own IL must name no
/// client-only type. The captain tooltip (OrderOfBattleHeroItemVM) and the four visual patches (SandBox.View,
/// TaleWorlds.MountAndBlade.View) sit in NoInlining helpers behind the server check; the settlement-guard patches
/// (SandBox.dll) apply everywhere, and the apply order is unchanged.
/// </summary>
[TestClass]
public class ManualPatchApplicatorServerGuardTests
{
    private const BindingFlags AllDeclared = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

    private static readonly string[] HelperNames = { "ApplyCaptainTooltip", "ApplyVisualPatches" };

    // SandBox and the other module assemblies are not in the test bin; the IL walk resolves them.
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    private static MethodInfo Method(string name) =>
        typeof(TAOM.ManualPatchApplicator).GetMethod(name, AllDeclared)
        ?? throw new AssertFailedException("Missing method " + name);

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ApplyAll_ReferencesNoClientAssembly()
    {
        Assert.AreEqual("", string.Join(", ", IlClientReferences.ClientReferences(Method("ApplyAll"))),
            "ApplyAll names a client-only type");
    }

    // A lambda compiles into a nested type and a non-capturing local function onto ManualPatchApplicator itself;
    // the walk of ApplyAll alone reads neither, and a server runs both. So every method of the class and its nested
    // types is walked, except the two helpers and the methods generated from them.
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void EveryMethodButTheHelpers_ReferencesNoClientAssembly()
    {
        var offenders = new List<string>();
        foreach (var method in AllMethods())
        {
            if (IsHelperOrFromHelper(method))
                continue;
            foreach (var name in IlClientReferences.ClientReferences(method))
                offenders.Add(method.DeclaringType!.Name + "." + method.Name + " -> " + name);
        }

        Assert.AreEqual("", string.Join("; ", offenders), "a method a server runs names a client-only type");
    }

    // Counted in IL, so a named argument, a second guard-free call or a call from a lambda all count; the source
    // test above pins that the one call sits behind the guard.
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void EachHelper_IsCalledOnceInIl_FromApplyAll()
    {
        foreach (var helper in HelperNames.Select(Method))
        {
            var callers = AllMethods()
                .Where(m => m.GetMethodBody() != null)
                .SelectMany(m => IlCallScanner.ExtractReferencedMembers(m, m.GetMethodBody()!.GetILAsByteArray()!)
                    .Where(r => r is MethodInfo mi && mi == helper)
                    .Select(_ => m.Name))
                .ToList();
            Assert.AreEqual("ApplyAll", string.Join(", ", callers), helper.Name + " must have exactly one call, in ApplyAll");
        }
    }

    private static bool IsHelperOrFromHelper(MethodBase method) =>
        HelperNames.Any(h => method.Name == h || method.Name.Contains("<" + h + ">"));

    private static IEnumerable<MethodBase> AllMethods()
    {
        foreach (var type in new[] { typeof(TAOM.ManualPatchApplicator) }.Concat(AllNested(typeof(TAOM.ManualPatchApplicator))))
            foreach (var method in type.GetMethods(AllDeclared | BindingFlags.Instance).Cast<MethodBase>()
                         .Concat(type.GetConstructors(AllDeclared | BindingFlags.Instance)))
                yield return method;
    }

    private static IEnumerable<Type> AllNested(Type type)
    {
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            yield return nested;
            foreach (var deeper in AllNested(nested))
                yield return deeper;
        }
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ApplyCaptainTooltip_HoldsTheClientReference()
    {
        CollectionAssert.Contains(IlClientReferences.ClientReferences(Method("ApplyCaptainTooltip")),
            "TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleHeroItemVM");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Helpers_AreNoInlining()
    {
        foreach (var name in HelperNames)
            Assert.IsTrue(Method(name).MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining),
                name + " must be NoInlining so its client references never land in ApplyAll");
    }

    [TestMethod]
    public void Source_GuardsTheCaptainTooltipBeforeAndTheVisualPatchesAfterTheSettlementGuards()
    {
        var src = RepoPaths.ReadSource("Main/ManualPatchApplicator.cs", stripComments: true);

        var captain = Regex.Matches(src, @"if \(!ClientComposition\.IsDedicatedServer\(\)\)\s*ApplyCaptainTooltip\(harmony\);");
        var visual = Regex.Matches(src, @"if \(!ClientComposition\.IsDedicatedServer\(\)\)\s*ApplyVisualPatches\(harmony\);");
        Assert.AreEqual(1, captain.Count, "the captain tooltip guard, once");
        Assert.AreEqual(1, visual.Count, "the visual patches guard, once");

        int guardsStart = src.IndexOf("IoC.Resolve<ISettlementGuardService>()", StringComparison.Ordinal);
        int guardsEnd = src.IndexOf("GuardsCampaignBehavior_InitializeGarrisonCharacters_Patch.Postfix", StringComparison.Ordinal);
        Assert.IsTrue(guardsStart >= 0 && guardsEnd > guardsStart, "a settlement-guard anchor moved");
        Assert.IsTrue(captain[0].Index < guardsStart, "the captain tooltip applies before the settlement guards");
        Assert.IsTrue(visual[0].Index > guardsEnd, "the visual patches apply after the settlement guards");
    }
}
