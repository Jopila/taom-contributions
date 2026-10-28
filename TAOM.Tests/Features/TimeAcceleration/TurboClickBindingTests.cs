using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features.TimeAcceleration;
using TAOM.Features.TimeAcceleration.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.TimeAcceleration;

/// <summary>
/// Patch124 (#1040) against the installed SandBox.View: the private target's shape, its one caller, the hotkey it
/// reads, a real Harmony apply plus JIT, and the category, marker and module wiring.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TurboClickBindingTests
{
    private const BindingFlags AllDeclared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Type MapScreenType()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        return AccessTools.TypeByName("SandBox.View.Map.MapScreen")!;
    }

    private static System.Collections.Generic.IEnumerable<Type> NestedTypesOf(Type t) =>
        t.GetNestedTypes(AllDeclared).SelectMany(n => NestedTypesOf(n).Prepend(n));

    private static bool CallsToken(byte[] il, byte[] token)
    {
        for (int i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] != OpCodes.Call.Value && il[i] != OpCodes.Callvirt.Value) continue;
            if (il[i + 1] == token[0] && il[i + 2] == token[1] && il[i + 3] == token[2] && il[i + 4] == token[3])
                return true;
        }
        return false;
    }

    private static MethodInfo Target() => AccessTools.DeclaredMethod(MapScreenType(), "IsParleyClickReleased");

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Target_IsOnePrivateParameterlessBoolInstanceMethod()
    {
        var m = Target();
        Assert.IsNotNull(m);
        Assert.IsTrue(m.IsPrivate);
        Assert.IsFalse(m.IsStatic);
        Assert.IsFalse(m.IsVirtual);
        Assert.AreEqual(typeof(bool), m.ReturnType);
        Assert.AreEqual(0, m.GetParameters().Length);
        Assert.AreEqual(1, MapScreenType().GetMethods(AllDeclared).Count(x => x.Name == "IsParleyClickReleased"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void Target_ReadsTheParleyModifierHotKey() =>
        Assert.IsTrue(PatchProcessor.ReadMethodBody(Target())
            .Any(p => p.Key == OpCodes.Ldstr && (p.Value as string) == "MapParleyModifier"));

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void Target_HasOneCaller_HandleMouse()
    {
        var target = Target();
        var candidates = NestedTypesOf(MapScreenType()).Prepend(MapScreenType())
            .SelectMany(t => t.GetMethods(AllDeclared).Cast<MethodBase>().Concat(t.GetConstructors(AllDeclared)))
            .ToList();
        // Open generic methods (MapScreen.AddMapView<T>) cannot go through ReadMethodBody, so scan their raw IL for a
        // call or callvirt carrying the target's metadata token. A hit fails the test instead of passing unseen.
        var token = BitConverter.GetBytes(target.MetadataToken);
        var skipped = candidates
            .Where(m => m.ContainsGenericParameters || m.DeclaringType!.ContainsGenericParameters)
            .Where(m => m.GetMethodBody() != null && CallsToken(m.GetMethodBody()!.GetILAsByteArray()!, token))
            .Select(m => m.DeclaringType!.FullName + "." + m.Name)
            .ToList();
        Assert.AreEqual(0, skipped.Count, string.Join(", ", skipped));
        var callers = candidates
            .Where(m => !m.ContainsGenericParameters && !m.DeclaringType!.ContainsGenericParameters)
            .Where(m => m.GetMethodBody() != null)
            .Where(m => PatchProcessor.ReadMethodBody(m)
                .Any(p => (p.Key == OpCodes.Call || p.Key == OpCodes.Callvirt) && Equals(p.Value, target)))
            .Select(m => m.DeclaringType!.FullName + "." + m.Name)
            .ToList();
        CollectionAssert.AreEqual(new[] { "SandBox.View.Map.MapScreen.HandleMouse" }, callers);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PatchClass_AppliedToTheInstalledEngine_AttachesAPostfixTheJitAccepts()
    {
        const string id = "taom.tests.timeacceleration.jit.turboclick";
        var harmony = new Harmony(id);
        var target = Target();
        try
        {
            harmony.CreateClassProcessor(typeof(MapScreen_IsParleyClickReleased_TurboClick_Patch)).Patch();

            var info = Harmony.GetPatchInfo(target);
            Assert.IsTrue(info.Postfixes.Any(p => p.owner == id), "the postfix is attached");
            Assert.AreEqual(0, info.Prefixes.Count(p => p.owner == id));
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(target.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PatchClass_IsClientOnly_InPatch124_DeclaredByItsModuleAtProcessLoad()
    {
        MapScreenType();
        var t = typeof(MapScreen_IsParleyClickReleased_TurboClick_Patch);
        Assert.IsTrue(t.IsDefined(typeof(ClientOnlyAttribute), false));
        CollectionAssert.AreEqual(new[] { "Patch124_TurboClick" },
            t.GetCustomAttributes<HarmonyPatchCategory>().Select(c => c.info.category).ToList());

        var decl = new TimeAccelerationModule().PatchCategories.Single();
        Assert.AreEqual("Patch124_TurboClick", decl.Category);
        Assert.AreEqual(ApplyPhase.ProcessLoad, decl.Phase);
        Assert.AreEqual(1, FeatureModules.All.OfType<TimeAccelerationModule>().Count());
    }
}
