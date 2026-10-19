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
/// The engine facts the nameplate finalizer's design rests on (v1.5.5 at the time of writing): <c>RefreshBinding</c> is the
/// one method of the nameplate assembly that reads <c>_countBind</c>, <c>_extraInfoTextBind</c> and <c>_fullNameBind</c>,
/// and it copies each to its property through the setter. So a mask laid over the fields for that one call, and removed
/// when it returns, is seen by nothing else. A new reader (or a call that stops using a setter) fails here and the design
/// has to be read again. The SandBox types are resolved by name: the test project references the TaleWorlds assemblies only.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HiddenHostsBindingTests
{
    private const BindingFlags AllMembers =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly string[] Fields = { "_countBind", "_extraInfoTextBind", "_fullNameBind" };

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Type VmType()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName("SandBox.ViewModelCollection.Nameplate.PartyNameplateVM");
        Assert.IsNotNull(type, "PartyNameplateVM did not resolve.");
        return type;
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }

    // Generic definitions cannot be read through Harmony's instruction reader; none of them can touch a PartyNameplateVM instance field.
    private static IEnumerable<MethodBase> Bodies(Type type) =>
        type.IsGenericTypeDefinition
            ? Enumerable.Empty<MethodBase>()
            : type.GetMembers(AllMembers).OfType<MethodBase>().Where(m => !m.IsGenericMethodDefinition && m.GetMethodBody() != null);

    private static List<string> Calls(MethodBase method) =>
        PatchProcessor.GetOriginalInstructions(method)
            .Where(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand is MethodBase)
            .Select(ci => ((MethodBase)ci.operand).Name)
            .ToList();

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void RefreshBinding_IsTheOnlyReaderOfTheThreeBindFields_InTheNameplateAssembly()
    {
        var vm = VmType();

        var readers = LoadableTypes(vm.Assembly)
            .SelectMany(Bodies)
            .SelectMany(m => PatchProcessor.GetOriginalInstructions(m)
                .Where(ci => (ci.opcode == OpCodes.Ldfld || ci.opcode == OpCodes.Ldflda)
                             && ci.operand is FieldInfo f && f.DeclaringType == vm && Fields.Contains(f.Name))
                .Select(ci => ((FieldInfo)ci.operand).Name + " <- " + m.DeclaringType!.Name + "." + m.Name))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        CollectionAssert.AreEqual(
            new[] { "_countBind <- PartyNameplateVM.RefreshBinding", "_extraInfoTextBind <- PartyNameplateVM.RefreshBinding", "_fullNameBind <- PartyNameplateVM.RefreshBinding" },
            readers, "readers: " + string.Join(", ", readers));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void RefreshBinding_CopiesTheFieldsThroughTheCountNameAndExtraInfoSetters()
    {
        var method = AccessTools.Method(VmType(), "RefreshBinding", Type.EmptyTypes);
        Assert.IsNotNull(method);

        var calls = Calls(method);

        foreach (var setter in new[] { "set_Count", "set_FullName", "set_ExtraInfoText" })
            Assert.AreEqual(1, calls.Count(c => c == setter), setter + " in: " + string.Join(", ", calls));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void RefreshBinding_IsVirtualAndPublic_SoThePrefixAndFinalizerBindToTheBaseBody()
    {
        var method = AccessTools.Method(VmType(), "RefreshBinding", Type.EmptyTypes);

        Assert.IsTrue(method.IsPublic);
        Assert.IsTrue(method.IsVirtual);
        Assert.AreEqual(VmType(), method.DeclaringType);
    }
}
