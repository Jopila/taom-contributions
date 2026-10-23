using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Pins <c>PatchCategoryIndex</c>: one patch class whose attributes cannot be read (a
/// <c>[HarmonyPatch]</c> naming a type the engine no longer has) must cost only that class. Harmony
/// 2.4.2 indexes categories once per assembly with no catch and never caches a failed build, so
/// through <c>Harmony.PatchCategory</c> that one class failed every category of the assembly.
/// The probe assembly is emitted at run time because a compiled attribute cannot name a missing
/// type, and a broken class in this test assembly would break every other category test here.
/// </summary>
[TestClass]
public class PatchCategoryIndexTests
{
    internal const string BrokenCategory = "Test_IndexProbe_Broken";
    internal const string HealthyCategory = "Test_IndexProbe_Healthy";
    private const string MixedCategory = "Test_IndexProbe_Mixed";
    private const string SecondCategory = "Test_IndexProbe_Second";
    private const string OrderedCategory = "Test_IndexProbe_Ordered";
    private const string ClientOnlyCategory = "Test_IndexProbe_ClientOnly";
    private const string ClientOnlyMixedCategory = "Test_IndexProbe_ClientOnlyMixed";
    private const string ClientOnlyMissingClassName = "TaomIndexProbe.ClientOnlyMissingTypeProbe";
    private const string ClientOnlyHealthyClassName = "TaomIndexProbe.ClientOnlyHealthyProbe";
    private const string UnresolvableAttributeClassName = "TaomIndexProbe.UnresolvableAttributeProbe";
    private const string BrokenClassName = "TaomIndexProbe.BrokenAttributeProbe";
    private const string HealthyClassName = "TaomIndexProbe.HealthyProbe";
    private const string UncategorisedClassName = "TaomIndexProbe.UncategorisedProbe";
    private const string MixedHealthyClassName = "TaomIndexProbe.MixedHealthyProbe";
    private const string MixedUnresolvableClassName = "TaomIndexProbe.MixedUnresolvableProbe";

    private static readonly Lazy<Assembly> ProbeAssembly = new(EmitProbeAssembly);

    private static readonly MethodInfo Target =
        typeof(PatchCategoryIndexProbeTarget).GetMethod(nameof(PatchCategoryIndexProbeTarget.Probe))!;

    private static readonly MethodInfo MixedTarget =
        typeof(PatchCategoryIndexProbeTarget).GetMethod(nameof(PatchCategoryIndexProbeTarget.MixedProbe))!;

    private static readonly MethodInfo SecondTarget =
        typeof(PatchCategoryIndexProbeTarget).GetMethod(nameof(PatchCategoryIndexProbeTarget.SecondProbe))!;

    private static readonly MethodInfo OrderedTarget =
        typeof(PatchCategoryIndexProbeTarget).GetMethod(nameof(PatchCategoryIndexProbeTarget.OrderedProbe))!;

    private static readonly MethodInfo ClientOnlyTarget =
        typeof(PatchCategoryIndexProbeTarget).GetMethod(nameof(PatchCategoryIndexProbeTarget.ClientOnlyProbe))!;

    private static readonly MethodInfo ClientOnlyMixedTarget =
        typeof(PatchCategoryIndexProbeTarget).GetMethod(nameof(PatchCategoryIndexProbeTarget.ClientOnlyMixedProbe))!;

    private IModLogger _logger = null!;
    private Harmony _harmony = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _harmony = new Harmony("taom.tests.patchcategoryindex");
    }

    [TestCleanup]
    public void Cleanup() => _harmony.UnpatchAll(_harmony.Id);

    // The premise: Harmony's own category apply fails a healthy category because a class in
    // another category has an unreadable attribute. If Harmony ever isolates this itself, this
    // fails and PatchCategoryIndex can go.
    [TestMethod]
    public void HarmonyPatchCategory_AnUnreadableAttributeInTheAssembly_ThrowsForAHealthyCategory()
    {
        Assert.ThrowsException<TypeLoadException>(
            () => _harmony.PatchCategory(ProbeAssembly.Value, HealthyCategory),
            "Harmony now indexes categories without throwing; PatchCategoryIndex may be redundant");
    }

    [TestMethod]
    public void Build_OneClassWithUnreadableAttributes_SkipsOnlyThatClass()
    {
        var index = PatchCategoryIndex.Build(ProbeAssembly.Value);

        CollectionAssert.AreEqual(new[] { BrokenClassName },
            index.SkippedClasses.Select(s => s.Key.FullName).ToArray());
        Assert.IsInstanceOfType(index.SkippedClasses[0].Value, typeof(TypeLoadException));
    }

    // The maintainer's decision (#653): one unreadable class is skipped and reported, and every
    // other category still applies.
    [TestCategory("RequiresGame")]
    [TestMethod]
    public void TryApply_ThroughTheIndex_AppliesTheHealthyCategory_AndReportsOnlyTheBrokenClass()
    {
        var index = PatchCategoryIndex.Build(ProbeAssembly.Value);
        var sut = new PatchCategoryApplier(category => index.Apply(_harmony, category), _logger);
        sut.RecordSkippedClasses(index.SkippedClasses);

        Assert.IsTrue(sut.TryApply(HealthyCategory), "the healthy category must apply");
        Assert.IsTrue(sut.TryApply(BrokenCategory), "the broken class is skipped, not retried");

        CollectionAssert.Contains(PostfixOwners(), _harmony.Id);
        _logger.Received(1).LogError(Arg.Is<string>(s =>
            s.Contains("[PatchApply]") && s.Contains(BrokenClassName) && s.Contains("TypeLoadException")));
        var groups = PatchCategoryApplierTests.Variable(sut.TakeFailureSummary(new TextObject("startup"))!, "GROUPS");
        Assert.AreEqual(BrokenClassName, groups);
    }

    // Parity with Harmony.PatchCategory: an unknown category name applies nothing, silently.
    [TestMethod]
    public void Apply_AnUnknownCategory_AppliesNothingAndDoesNotThrow()
    {
        PatchCategoryIndex.Build(ProbeAssembly.Value).Apply(_harmony, "Test_IndexProbe_NoSuchCategory");

        CollectionAssert.DoesNotContain(PostfixOwners(), _harmony.Id);
    }

    // Parity with Harmony.BuildCategoryCache: a [HarmonyPatch] class with no category is neither
    // indexed nor skipped. Without the null-category guard, Build would throw out of
    // OnSubModuleLoad (Dictionary.TryGetValue(null)), outside the per-class catch.
    [TestMethod]
    public void Build_AClassWithNoCategory_IsNeitherSkippedNorApplied()
    {
        var index = PatchCategoryIndex.Build(ProbeAssembly.Value);
        index.Apply(_harmony, HealthyCategory);

        CollectionAssert.DoesNotContain(
            index.SkippedClasses.Select(s => s.Key.FullName).ToArray(), UncategorisedClassName);
        Assert.AreEqual(1, PostfixOwners().Count(owner => owner == _harmony.Id),
            "only the healthy class patches the probe target; the uncategorised one is never applied");
    }

    // Parity with Harmony.PatchCategory, as Apply documents: the first class that cannot resolve
    // its target throws, and the classes before it in assembly order stay patched.
    [TestMethod]
    public void Apply_ACategoryWhoseSecondClassCannotResolve_ThrowsAndKeepsTheFirstClassPatched()
    {
        var index = PatchCategoryIndex.Build(ProbeAssembly.Value);

        Assert.ThrowsException<HarmonyException>(() => index.Apply(_harmony, MixedCategory));
        CollectionAssert.Contains(PostfixOwners(MixedTarget), _harmony.Id);
    }

    // ADR-013: TAOM's patch classes live in two assemblies (TAOM.dll and TAOM.Client.dll), and a category holds the
    // classes of both. The second probe assembly stands in for the client.
    [TestMethod]
    public void Build_TwoAssemblies_IndexesTheClassesOfBoth()
    {
        var second = EmitSmallAssembly("TAOM.Tests.PatchCategoryIndexProbe.Second",
            ("TaomIndexProbe.SecondAssemblyProbe", SecondCategory, nameof(PatchCategoryIndexProbeTarget.SecondProbe)));

        var index = PatchCategoryIndex.Build(ProbeAssembly.Value, second);
        index.Apply(_harmony, HealthyCategory);
        index.Apply(_harmony, SecondCategory);

        CollectionAssert.Contains(PostfixOwners(), _harmony.Id);
        CollectionAssert.Contains(PostfixOwners(SecondTarget), _harmony.Id);
        CollectionAssert.AreEqual(new[] { "TaomIndexProbe.SecondAssemblyProbe" },
            index.ClassesOf(SecondCategory).Select(t => t.FullName).ToArray());
        Assert.AreEqual(0, index.ClassesOf("Test_IndexProbe_NoSuchCategory").Count);
    }

    // A category applies the first assembly's classes first, then the second's, so a core class runs before a client one.
    [TestMethod]
    public void Build_TwoAssemblies_AppliesTheFirstAssemblysClassesFirst()
    {
        var healthy = EmitSmallAssembly("TAOM.Tests.PatchCategoryIndexProbe.OrderGood",
            ("TaomIndexProbe.OrderedHealthy", OrderedCategory, nameof(PatchCategoryIndexProbeTarget.OrderedProbe)));
        var unresolvable = EmitSmallAssembly("TAOM.Tests.PatchCategoryIndexProbe.OrderBad",
            ("TaomIndexProbe.OrderedUnresolvable", OrderedCategory, "NoSuchMethod"));

        Assert.ThrowsException<HarmonyException>(
            () => PatchCategoryIndex.Build(healthy, unresolvable).Apply(_harmony, OrderedCategory));
        CollectionAssert.Contains(PostfixOwners(OrderedTarget), _harmony.Id, "the healthy class came first, so it stays patched");

        _harmony.UnpatchAll(_harmony.Id);
        Assert.ThrowsException<HarmonyException>(
            () => PatchCategoryIndex.Build(unresolvable, healthy).Apply(_harmony, OrderedCategory));
        CollectionAssert.DoesNotContain(PostfixOwners(OrderedTarget), _harmony.Id, "the unresolvable class came first and threw");
    }

    // ADR-013: on a dedicated server a [ClientOnly] class is left out before its attributes are read, so a class whose
    // [HarmonyPatch] names a type the server cannot load is neither skipped nor reported as a failure.
    [TestMethod]
    public void Build_DedicatedServer_LeavesOutAClientOnlyClassBeforeReadingItsAttributes()
    {
        var assembly = EmitClientOnlyAssembly("TAOM.Tests.PatchCategoryIndexProbe.ClientOnlyMissing",
            ClientOnlyMissingClassName, missingTarget: true);

        var index = PatchCategoryIndex.Build(true, assembly);

        CollectionAssert.AreEqual(new[] { ClientOnlyMissingClassName },
            index.ClientOnlyClasses.Select(t => t.FullName).ToArray());
        Assert.AreEqual(0, index.SkippedClasses.Count, "a marked class is left out, not skipped as unreadable");
        Assert.AreEqual(0, index.ClassesOf(ClientOnlyCategory).Count);

        var onAClient = PatchCategoryIndex.Build(false, assembly);
        Assert.AreEqual(0, onAClient.ClientOnlyClasses.Count, "a client leaves nothing out");
        CollectionAssert.AreEqual(new[] { ClientOnlyMissingClassName },
            onAClient.SkippedClasses.Select(s => s.Key.FullName).ToArray(),
            "on a client the unreadable class is still skipped as before");
    }

    // The real server log (taom_debug_2026-10-23_09-23-19.log) shows both screens' attribute types cannot be resolved on a
    // server. IsDefined resolves the TYPE of every attribute on a class, so the first 6a shape, with the check outside the
    // per-class try, would have thrown out of Build (found by review, reproduced by this test). It must be skipped like
    // any unreadable class.
    [TestMethod]
    public void Build_DedicatedServer_AnUnmarkedClassWithAnAttributeTypeThatCannotLoad_IsSkippedNotThrown()
    {
        var assembly = GoneAttributeProbe.Emit(UnresolvableAttributeClassName);

        var index = PatchCategoryIndex.Build(true, assembly);

        CollectionAssert.AreEqual(new[] { UnresolvableAttributeClassName },
            index.SkippedClasses.Select(s => s.Key.FullName).ToArray());
        Assert.AreEqual(0, index.ClientOnlyClasses.Count);
    }

    [TestMethod]
    public void Build_OnAClient_IndexesAClientOnlyClassAsBefore()
    {
        var assembly = EmitClientOnlyAssembly("TAOM.Tests.PatchCategoryIndexProbe.ClientOnlyHealthy",
            ClientOnlyHealthyClassName, missingTarget: false);

        var index = PatchCategoryIndex.Build(false, assembly);
        index.Apply(_harmony, ClientOnlyCategory);

        Assert.AreEqual(0, index.ClientOnlyClasses.Count);
        CollectionAssert.Contains(PostfixOwners(ClientOnlyTarget), _harmony.Id);

        _harmony.UnpatchAll(_harmony.Id);
        PatchCategoryIndex.Build(true, assembly).Apply(_harmony, ClientOnlyCategory);
        CollectionAssert.DoesNotContain(PostfixOwners(ClientOnlyTarget), _harmony.Id, "a server applies nothing of it");
    }

    // The mixed-category case B6 plan decision D1 relies on: a server drops the marked class, so the category applies its
    // core class instead of aborting at the marked class's unresolvable target.
    [TestMethod]
    public void Build_DedicatedServer_AMixedCategory_AppliesItsCoreClassWithoutThrowing()
    {
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
            new AssemblyName("TAOM.Tests.PatchCategoryIndexProbe.ClientOnlyMixed"), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("TAOM.Tests.PatchCategoryIndexProbe.ClientOnlyMixed");
        EmitPatchClass(module, "TaomIndexProbe.MixedClientHalf", ClientOnlyMixedCategory,
            harmonyPatch: builder => builder.SetCustomAttribute(TargetAttribute("NoSuchMethod")), clientOnly: true);
        EmitPatchClass(module, "TaomIndexProbe.MixedCoreHalf", ClientOnlyMixedCategory,
            harmonyPatch: builder => builder.SetCustomAttribute(TargetAttribute(nameof(PatchCategoryIndexProbeTarget.ClientOnlyMixedProbe))));

        PatchCategoryIndex.Build(true, assembly).Apply(_harmony, ClientOnlyMixedCategory);
        CollectionAssert.Contains(PostfixOwners(ClientOnlyMixedTarget), _harmony.Id, "the core class applies on a server");

        _harmony.UnpatchAll(_harmony.Id);
        Assert.ThrowsException<HarmonyException>(
            () => PatchCategoryIndex.Build(false, assembly).Apply(_harmony, ClientOnlyMixedCategory),
            "control: on a client the marked class is indexed and aborts the category");
    }

    // Over the real TAOM.dll: the server drops exactly the marked classes, and nothing else fails to read.
    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Build_DedicatedServer_OverTaomDll_LeavesOutExactlyTheMarkedClasses()
    {
        TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();
        var core = typeof(IoC).Assembly;
        var marked = AccessTools.GetTypesFromAssembly(core)
            .Where(t => t.IsDefined(typeof(ClientOnlyAttribute), false)).Select(t => t.FullName!).OrderBy(n => n).ToArray();

        var index = PatchCategoryIndex.Build(true, core);

        Assert.IsTrue(marked.Length > 0, "no [ClientOnly] class in TAOM.dll: the marker pass is missing");
        CollectionAssert.AreEqual(marked, index.ClientOnlyClasses.Select(t => t.FullName!).OrderBy(n => n).ToArray());
        Assert.AreEqual(0, index.SkippedClasses.Count,
            "here the client assemblies load, so nothing is unreadable; the classes a real server skips are pinned by "
            + "ServerLoadGateTests.CoreClassesWithAClientOnlyAttributeType_AreTheKnownList: "
            + string.Join(", ", index.SkippedClasses.Select(s => s.Key.FullName)));
    }

    // Harmony keeps an empty patch record after an unpatch, so ask for owners, not for null.
    private static string[] PostfixOwners() => PostfixOwners(Target);

    private static string[] PostfixOwners(MethodInfo target)
        => Harmony.GetPatchInfo(target)?.Postfixes.Select(p => p.owner).ToArray() ?? Array.Empty<string>();

    private static Assembly EmitProbeAssembly()
    {
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
            new AssemblyName("TAOM.Tests.PatchCategoryIndexProbe"), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("TAOM.Tests.PatchCategoryIndexProbe");

        // A type the engine "no longer has": its assembly loads, the type is gone.
        var missingType = "TaleWorlds.RemovedByAnEngineUpdate.GoneType, " + typeof(PatchCategoryIndexTests).Assembly.FullName;
        EmitPatchClass(module, BrokenClassName, BrokenCategory, harmonyPatch: builder =>
            builder.SetCustomAttribute(
                typeof(HarmonyPatch).GetConstructor(new[] { typeof(Type) })!,
                TypeArgumentBlob(missingType)));
        EmitPatchClass(module, HealthyClassName, HealthyCategory, harmonyPatch: builder =>
            builder.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(HarmonyPatch).GetConstructor(new[] { typeof(Type), typeof(string) })!,
                new object[] { typeof(PatchCategoryIndexProbeTarget), nameof(PatchCategoryIndexProbeTarget.Probe) })));
        EmitPatchClass(module, UncategorisedClassName, category: null, harmonyPatch: builder =>
            builder.SetCustomAttribute(TargetAttribute(nameof(PatchCategoryIndexProbeTarget.Probe))));
        // Defined in this order, so the healthy class comes first in the category.
        EmitPatchClass(module, MixedHealthyClassName, MixedCategory, harmonyPatch: builder =>
            builder.SetCustomAttribute(TargetAttribute(nameof(PatchCategoryIndexProbeTarget.MixedProbe))));
        EmitPatchClass(module, MixedUnresolvableClassName, MixedCategory, harmonyPatch: builder =>
            builder.SetCustomAttribute(TargetAttribute("NoSuchMethod")));
        return assembly;
    }

    private static Assembly EmitSmallAssembly(string name, params (string Class, string Category, string Method)[] classes)
    {
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(name);
        foreach (var (className, category, method) in classes)
            EmitPatchClass(module, className, category, harmonyPatch: builder => builder.SetCustomAttribute(TargetAttribute(method)));
        return assembly;
    }

    // One [ClientOnly] class; missingTarget names a type in a missing assembly, as a client-only patch would on a server.
    private static Assembly EmitClientOnlyAssembly(string name, string className, bool missingTarget)
    {
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(name);
        EmitPatchClass(module, className, ClientOnlyCategory, harmonyPatch: builder =>
        {
            if (missingTarget)
                builder.SetCustomAttribute(
                    typeof(HarmonyPatch).GetConstructor(new[] { typeof(Type) })!,
                    TypeArgumentBlob("TaleWorlds.RemovedByAnEngineUpdate.GoneType, " + typeof(PatchCategoryIndexTests).Assembly.FullName));
            else
                builder.SetCustomAttribute(TargetAttribute(nameof(PatchCategoryIndexProbeTarget.ClientOnlyProbe)));
        }, clientOnly: true);
        return assembly;
    }

    private static CustomAttributeBuilder TargetAttribute(string method)
        => new(typeof(HarmonyPatch).GetConstructor(new[] { typeof(Type), typeof(string) })!,
            new object[] { typeof(PatchCategoryIndexProbeTarget), method });

    private static void EmitPatchClass(
        ModuleBuilder module, string name, string? category, Action<TypeBuilder> harmonyPatch, bool clientOnly = false)
    {
        var type = module.DefineType(name,
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed);
        if (category != null)
            type.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(HarmonyPatchCategory).GetConstructor(new[] { typeof(string) })!, new object[] { category }));
        if (clientOnly)
            type.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(ClientOnlyAttribute).GetConstructor(Type.EmptyTypes)!, Array.Empty<object>()));
        harmonyPatch(type);
        var postfix = type.DefineMethod("Postfix", MethodAttributes.Public | MethodAttributes.Static,
            typeof(void), Type.EmptyTypes);
        postfix.GetILGenerator().Emit(OpCodes.Ret);
        type.CreateType();
    }

    // ECMA-335 II.23.3 custom attribute blob for a single System.Type argument: prolog 0x0001,
    // the type's assembly-qualified name as a SerString, then zero named arguments. Writing the
    // blob by hand is the only way to name a type that does not exist.
    private static byte[] TypeArgumentBlob(string assemblyQualifiedName)
    {
        var name = Encoding.UTF8.GetBytes(assemblyQualifiedName);
        var blob = new List<byte> { 0x01, 0x00 };
        if (name.Length < 0x80)
            blob.Add((byte)name.Length);
        else
            blob.AddRange(new[] { (byte)(0x80 | (name.Length >> 8)), (byte)(name.Length & 0xFF) });
        blob.AddRange(name);
        blob.AddRange(new byte[] { 0x00, 0x00 });
        return blob.ToArray();
    }
}

/// <summary>The probe classes' patch targets. Nothing else patches them.</summary>
public static class PatchCategoryIndexProbeTarget
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Probe() => 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int MixedProbe() => 2;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int SecondProbe() => 3;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int OrderedProbe() => 4;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ClientOnlyProbe() => 5;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int ClientOnlyMixedProbe() => 6;
}

/// <summary>
/// An emitted assembly whose one class carries an attribute from an assembly that is not on disk, the shape of a
/// class-level [GameStateScreen] on a server. A reference cannot be left unresolvable in this process once the attribute
/// assembly has been loaded into it, so both assemblies are built and saved in a throwaway AppDomain; the attribute
/// assembly is then deleted, and only the probe is read back.
/// </summary>
internal static class GoneAttributeProbe
{
    private const string AttributeAssembly = "TaomIndexProbe.GoneAttributes";
    private const string ProbeAssembly = "TaomIndexProbe.UnresolvableAttribute";

    internal static Assembly Emit(string className)
    {
        var directory = Path.Combine(Path.GetTempPath(), "taom-index-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var child = AppDomain.CreateDomain("taom-index-probe", null,
            new AppDomainSetup { ApplicationBase = AppDomain.CurrentDomain.BaseDirectory });
        try
        {
            child.SetData("directory", directory);
            child.SetData("className", className);
            child.DoCallBack(BuildBothAssemblies);
        }
        finally
        {
            AppDomain.Unload(child);
        }

        try
        {
            File.Delete(Path.Combine(directory, AttributeAssembly + ".dll"));
            return Assembly.Load(File.ReadAllBytes(Path.Combine(directory, ProbeAssembly + ".dll")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void BuildBothAssemblies()
    {
        var directory = (string)AppDomain.CurrentDomain.GetData("directory");
        var className = (string)AppDomain.CurrentDomain.GetData("className");

        var attributeBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
            new AssemblyName(AttributeAssembly), AssemblyBuilderAccess.RunAndSave, directory);
        var attributeModule = attributeBuilder.DefineDynamicModule(AttributeAssembly, AttributeAssembly + ".dll");
        var attribute = attributeModule.DefineType("TaomIndexProbe.GoneAttribute", TypeAttributes.Public | TypeAttributes.Class, typeof(Attribute));
        attribute.DefineDefaultConstructor(MethodAttributes.Public);
        attribute.CreateType();
        attributeBuilder.Save(AttributeAssembly + ".dll");

        var ctor = Assembly.LoadFile(Path.Combine(directory, AttributeAssembly + ".dll"))
            .GetType("TaomIndexProbe.GoneAttribute")!.GetConstructor(Type.EmptyTypes)!;
        var probeBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(
            new AssemblyName(ProbeAssembly), AssemblyBuilderAccess.RunAndSave, directory);
        var probeModule = probeBuilder.DefineDynamicModule(ProbeAssembly, ProbeAssembly + ".dll");
        var type = probeModule.DefineType(className,
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed);
        type.SetCustomAttribute(new CustomAttributeBuilder(ctor, Array.Empty<object>()));
        type.CreateType();
        probeBuilder.Save(ProbeAssembly + ".dll");
    }
}
