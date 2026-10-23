using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.PatchClientReasonsFixtures;

// System.dll stands in for a client-only assembly (predicate: name == "System"), as in ServerLoadFixtureTests. Each class
// is the minimal shape of one detector in PatchClientReasons, so a detector that stops firing fails here, not silently
// in the gate. None carries a HarmonyPatchCategory, so no category index applies them.

[HarmonyPatch(typeof(Uri), "ToString")]
public static class ClassLevelTarget
{
    public static void Postfix() { }
}

public static class MethodLevelTarget
{
    [HarmonyPatch(typeof(Uri), "ToString")]
    public static void Postfix() { }
}

[HarmonyPatch(typeof(string), "ToString")]
public static class SignatureOnly
{
    public static void Postfix(ref List<Uri[]> __state) { }
}

[HarmonyPatch]
public static class TargetMethodBody
{
    public static MethodBase TargetMethod() => typeof(Uri).GetMethod("GetHashCode")!;

    public static void Postfix() { }
}

[HarmonyPatch]
public static class TargetMethodsBody
{
    public static IEnumerable<MethodBase> TargetMethods() => new MethodBase[] { typeof(string).GetMethod("ToString", Type.EmptyTypes)!, typeof(Uri).GetMethod("GetHashCode")! };

    public static void Postfix() { }
}

[HarmonyPatch]
public static class AttributedSelectorBody
{
    [HarmonyTargetMethod]
    public static MethodBase PickTheTarget() => typeof(Uri).GetMethod("GetHashCode")!;

    public static void Postfix() { }
}

[HarmonyPatch]
public static class AttributedSelectorsBody
{
    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> PickTheTargets() => new MethodBase[] { typeof(string).GetMethod("ToString", Type.EmptyTypes)!, typeof(Uri).GetMethod("GetHashCode")! };

    public static void Postfix() { }
}

[HarmonyPatch]
public static class SelectorTakingHarmony
{
    public static MethodBase TargetMethod(Harmony harmony) =>
        harmony == null ? throw new ArgumentNullException(nameof(harmony)) : typeof(Uri).GetMethod("GetHashCode")!;

    public static void Postfix() { }
}

[HarmonyPatch]
public static class AttributedSelectorTakingHarmony
{
    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> Many(Harmony harmony) =>
        harmony == null ? throw new ArgumentNullException(nameof(harmony)) : new MethodBase[] { typeof(Uri).GetMethod("GetHashCode")! };

    public static void Postfix() { }
}

[HarmonyPatch]
public static class SelectorTakingObject
{
    public static MethodBase TargetMethod(object harmony) =>
        harmony == null ? throw new ArgumentNullException(nameof(harmony)) : typeof(Uri).GetMethod("GetHashCode")!;

    public static void Postfix() { }
}

[HarmonyPatch]
public static class TargetMethodThrows
{
    public static MethodBase TargetMethod() => throw new InvalidOperationException("no such type");

    public static void Postfix() { }
}

[HarmonyPatch(typeof(string), "ToString")]
public static class CleanNegative
{
    public static void Postfix(ref string __result, object __instance) { }
}

[TestClass]
public class PatchClientReasonsFixtureTests
{
    private static bool StandIn(Assembly assembly) => assembly.GetName().Name == "System";

    private static (List<string> Reasons, List<string> Errors) Run(Type type)
    {
        var errors = new List<string>();
        return (PatchClientReasons.For(type, StandIn, errors), errors);
    }

    [TestMethod]
    public void For_ClassLevelTarget_ReportsATarget()
    {
        var (reasons, errors) = Run(typeof(ClassLevelTarget));

        CollectionAssert.AreEqual(new[] { "target System.Uri (System)" }, reasons);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void For_MethodLevelTarget_ReportsATarget() =>
        CollectionAssert.AreEqual(new[] { "target System.Uri (System)" }, Run(typeof(MethodLevelTarget)).Reasons);

    [TestMethod]
    public void For_SignatureType_ReportsItThroughGenericsArraysAndByRef() =>
        CollectionAssert.AreEqual(new[] { "signature System.Uri (System)" }, Run(typeof(SignatureOnly)).Reasons);

    [TestMethod]
    public void For_TargetMethodBody_ReportsTheReturnedMethodsDeclaringType() =>
        CollectionAssert.AreEqual(new[] { "TargetMethod System.Uri (System)" }, Run(typeof(TargetMethodBody)).Reasons);

    [TestMethod]
    public void For_TargetMethodsBody_ReportsOnlyTheClientMethod() =>
        CollectionAssert.AreEqual(new[] { "TargetMethod System.Uri (System)" }, Run(typeof(TargetMethodsBody)).Reasons);

    // Harmony 2.4.2 PatchTools.GetPatchMethod finds a selector by its attribute first, under any name.
    [TestMethod]
    public void For_SelectorNamedByAttribute_ReportsTheReturnedMethodsDeclaringType() =>
        CollectionAssert.AreEqual(new[] { "TargetMethod System.Uri (System)" }, Run(typeof(AttributedSelectorBody)).Reasons);

    [TestMethod]
    public void For_SelectorsNamedByAttribute_ReportsOnlyTheClientMethod() =>
        CollectionAssert.AreEqual(new[] { "TargetMethod System.Uri (System)" }, Run(typeof(AttributedSelectorsBody)).Reasons);

    // Harmony passes its own instance to a selector that declares a Harmony parameter (RunMethod, AccessTools.ActualParameters).
    [TestMethod]
    public void For_SelectorTakingHarmony_IsInvokedWithAnInstance()
    {
        var (reasons, errors) = Run(typeof(SelectorTakingHarmony));

        CollectionAssert.AreEqual(new[] { "TargetMethod System.Uri (System)" }, reasons);
        Assert.AreEqual(0, errors.Count);
    }

    // AccessTools.ActualParameters matches by assignability, so a selector declaring a base type still gets the instance.
    [TestMethod]
    public void For_SelectorTakingAHarmonyBaseType_IsInvokedWithAnInstance()
    {
        var (reasons, errors) = Run(typeof(SelectorTakingObject));

        CollectionAssert.AreEqual(new[] { "TargetMethod System.Uri (System)" }, reasons);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void For_AttributedSelectorTakingHarmony_IsInvokedWithAnInstance()
    {
        var (reasons, errors) = Run(typeof(AttributedSelectorTakingHarmony));

        CollectionAssert.AreEqual(new[] { "TargetMethod System.Uri (System)" }, reasons);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void For_TargetMethodThatThrows_IsAnErrorNotASilentMiss()
    {
        var (reasons, errors) = Run(typeof(TargetMethodThrows));

        Assert.AreEqual(0, reasons.Count);
        Assert.AreEqual(1, errors.Count);
        StringAssert.Contains(errors[0], "InvalidOperationException");
        StringAssert.Contains(errors[0], nameof(TargetMethodThrows));
    }

    [TestMethod]
    public void For_CleanClass_HasNoReason()
    {
        var (reasons, errors) = Run(typeof(CleanNegative));

        Assert.AreEqual(0, reasons.Count);
        Assert.AreEqual(0, errors.Count);
    }
}
