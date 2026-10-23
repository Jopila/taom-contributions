using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Headless gates. On the dedicated server the engine runs <c>Assembly.GetTypes()</c> on TAOM.dll, and a
/// type that needs a client-only assembly to be laid out makes that call throw, so TAOM does not load.
/// Reference-typed fields and method bodies do not fail GetTypes (26 field-only and 190 body-only types
/// loaded in run 46908), so the walk covers base chain, interfaces, value-typed fields, generic
/// constraints and cross-assembly override signatures
/// (see <c>ServerLoadScan</c>; its fixtures are <c>ServerLoadFixtureTests</c>). There is no allowlist since M1 (ADR-013): a failing type moves to TAOM.Client.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ServerLoadGateTests
{
    private const string StageEnvVar = "TAOM_DS_STAGE";
    private const string DefaultStageRoot = @"E:\repos\BannerlordCoop.DedicatedServer\.stage-win\engine";

    // Modules ahead of TAOM in the stock order; the Coop module is after it.
    private static readonly string[] ModulesLoadedBeforeTaom =
        { "Native", "SandBoxCore", "SandBox", "TAOM.Dependencies", "LOTRLOME_Armory", "TAOM_Map" };

    // The core classes that carry a class-level attribute from a client-only assembly ([GameStateScreen], in
    // TaleWorlds.MountAndBlade.View). The attribute type cannot load on a server, so PatchCategoryIndex.Build reports
    // each as a skipped class there. ADR-013 batch B7 moves both screens to TAOM.Client, which empties this list.
    internal static readonly string[] CoreClassesWithClientOnlyAttributes =
    {
        "TAOM.Features.CareerSystem.UI.GauntletCareerScreen",
        "TAOM.Features.SupplyLines.UI.GauntletSupplyOrderScreen",
    };

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) =>
        TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void EveryCoreType_LoadsWithoutClientAssemblies()
    {
        var failing = ServerLoadScan.FindClientDependentTypes(typeof(SubModule).Assembly.GetTypes(), ClientAssemblies.IsClientOnly)
            .Select(kv => $"{kv.Key.FullName} (from {kv.Value.Assembly}, shape {(int)kv.Value.Shape} {kv.Value.Shape})")
            .OrderBy(s => s, StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(new List<string>(), failing,
            "These TAOM.dll types would fail to load on the dedicated server (they need a client-only assembly to be "
            + "laid out: shape 1 base type or interface, 2 value-typed field, 3 generic constraint, 4 cross-assembly "
            + "override). Put them in TAOM.Client or break the dependency; there is no allowlist (ADR-013, M1).");
    }

    // Reading a class's attributes on a server resolves each attribute type, so a core class carrying one from a client-only
    // assembly throws there even though GetTypes() succeeds. The list is the known debt: this fails on a new one (move the
    // class to TAOM.Client) and on a healed one (remove it from CoreClassesWithClientOnlyAttributes).
    [TestMethod]
    public void CoreClassesWithAClientOnlyAttributeType_AreTheKnownList()
    {
        var found = typeof(SubModule).Assembly.GetTypes()
            .Where(t => CustomAttributeData.GetCustomAttributes(t)
                .Any(a => ClientAssemblies.IsClientOnly(a.AttributeType.Assembly.GetName().Name!)))
            .Select(t => t.FullName!).OrderBy(n => n, StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(CoreClassesWithClientOnlyAttributes.OrderBy(n => n, StringComparer.Ordinal).ToList(), found,
            "The TAOM.dll classes with a class-level attribute from a client-only assembly changed. A new one fails to read on "
            + "the dedicated server: put it in TAOM.Client. A healed one: remove it from CoreClassesWithClientOnlyAttributes.");
    }

    [TestMethod]
    public void EveryDependenciesType_LoadsWithoutClientAssemblies()
    {
        var failing = ServerLoadScan.FindClientDependentTypes(
                typeof(TAOM.Dependencies.SubModule).Assembly.GetTypes(), ClientAssemblies.IsClientOnly)
            .Select(kv => $"{kv.Key.FullName} (from {kv.Value.Assembly}, shape {(int)kv.Value.Shape} {kv.Value.Shape})")
            .OrderBy(s => s, StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(new List<string>(), failing,
            "TAOM.Dependencies.dll loads first on the dedicated server and must have no type that needs a client-only assembly");
    }

    // Gate 4 (ADR-013, B6): a dedicated server drops every [ClientOnly] class before it reads the class attributes, so the
    // marker must sit on exactly the patch classes that name a client-only assembly. Missing: the server indexes the class
    // and fails or aborts its category. Stale: the server silently loses a patch it could have applied. Three ways a class
    // names one: a [HarmonyPatch] target, a patch-method signature, and what a TargetMethod/TargetMethods returns (a string
    // resolved target is invisible to the first two, so each is invoked here, which needs the client assemblies loaded).
    // Bannerlord.UIExtenderEx resolves on the server but its submodule never loads there, so it counts as a patch target
    // only; it stays out of ClientAssemblies.Names, which the staged-server check asserts are all absent.
    [TestMethod]
    public void PatchClassesReferencingClientAssemblies_AreClientOnly()
    {
        foreach (var name in ClientAssemblies.Names)
        {
            // A framework assembly is not found by simple name on net472; the rest come through GameAssemblies' resolver.
            var display = name.StartsWith("System.", StringComparison.Ordinal)
                ? name + ", Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" : name;
            try { Assembly.Load(display); }
            catch (Exception ex) { Assert.Inconclusive($"Cannot load {name} ({ex.GetType().Name}); the TargetMethod half of the gate cannot run."); }
        }

        bool IsClient(Assembly assembly)
        {
            var name = assembly.GetName().Name!;
            return ClientAssemblies.IsClientOnly(name) || name == "Bannerlord.UIExtenderEx"
                || assembly == typeof(TAOM.Client.ClientSubModule).Assembly;
        }

        var previous = TAOM.Composition.ClientComposition.Current;
        TAOM.Composition.ClientComposition.Current = new TAOM.Client.TaomClientComposition();
        var problems = new List<string>();
        var patchClasses = 0;
        var withReason = 0;
        try
        {
            var core = TaomAssemblies.All[0];
            foreach (var type in TaomAssemblies.All.SelectMany(HarmonyLib.AccessTools.GetTypesFromAssembly))
            {
                var marked = type.IsDefined(typeof(ClientOnlyAttribute), false);
                var isPatchClass = type.Assembly == core && HarmonyLib.HarmonyMethodExtensions.GetFromType(type).Count > 0;
                if (marked && type.Assembly != core)
                    problems.Add($"MISPLACED {type.FullName}: [ClientOnly] is a core marker, TAOM.Client is never indexed on a server");
                else if (marked && !isPatchClass)
                    problems.Add($"MISPLACED {type.FullName}: [ClientOnly] on a class that is not a Harmony patch class");
                if (!isPatchClass) continue;

                patchClasses++;
                var errors = new List<string>();
                var reasons = PatchClientReasons.For(type, IsClient, errors);
                problems.AddRange(errors.Select(e => "TARGETMETHOD THREW " + e));
                if (reasons.Count > 0) withReason++;
                if (reasons.Count > 0 && !marked)
                    problems.Add($"MISSING {type.FullName}: {string.Join("; ", reasons)}");
                if (reasons.Count == 0 && marked && errors.Count == 0)
                    problems.Add($"STALE {type.FullName}: marked [ClientOnly] but names no client-only assembly");
            }
        }
        finally
        {
            TAOM.Composition.ClientComposition.Current = previous;
        }

        Assert.IsTrue(patchClasses >= 300 && withReason >= 100,
            $"the scan went vacuous: {patchClasses} patch classes (floor 300), {withReason} with a client reason (floor 100)");
        Assert.AreEqual(0, problems.Count,
            "Mark a patch class that names a client-only assembly [ClientOnly] (namespace TAOM), and remove the marker from "
            + "one that does not (ADR-013):" + Environment.NewLine
            + string.Join(Environment.NewLine, problems.OrderBy(p => p, StringComparer.Ordinal)));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void ClientOnlyList_AgainstStagedServer_ListedAbsentAndOtherReferencesPresent()
    {
        var root = Environment.GetEnvironmentVariable(StageEnvVar);
        if (string.IsNullOrEmpty(root))
            root = DefaultStageRoot;
        var appBin = Path.Combine(root, "bin", "Win64_Shipping_Server");
        if (!Directory.Exists(appBin))
            Assert.Inconclusive("No staged server at " + root + " (set " + StageEnvVar + ")");

        // What TAOM.dll can see when it loads: the app bin plus the bins of the modules ahead of TAOM in the
        // stock order. The Coop module loads after TAOM, so its copies of the client assemblies do not count.
        var dirs = new List<string> { appBin };
        dirs.AddRange(ModulesLoadedBeforeTaom
            .Select(m => Path.Combine(root, "Modules", m, "bin", "Win64_Shipping_Server")).Where(Directory.Exists));

        bool Visible(string name) =>
            dirs.Any(d => File.Exists(Path.Combine(d, name + ".dll")));

        var listedButPresent = ClientAssemblies.Names.Where(Visible).ToList();
        Assert.IsTrue(listedButPresent.Count == 0,
            "Listed as client-only but visible to TAOM.dll on the staged server (remove from ClientAssemblies): "
            + string.Join(", ", listedButPresent));

        var missing = typeof(SubModule).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("TaleWorlds.", StringComparison.Ordinal)
                        || n.StartsWith("SandBox", StringComparison.Ordinal))
            .Where(n => !ClientAssemblies.IsClientOnly(n))
            .Where(n => !Visible(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.IsTrue(missing.Count == 0,
            "TAOM.dll references these TaleWorlds/SandBox assemblies, not visible on the staged server and not in "
            + "ClientAssemblies: " + string.Join(", ", missing));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TaomClient_AbsentFromTheStagedServer()
    {
        var root = Environment.GetEnvironmentVariable(StageEnvVar);
        if (string.IsNullOrEmpty(root))
            root = DefaultStageRoot;
        var appBin = Path.Combine(root, "bin", "Win64_Shipping_Server");
        var moduleBin = Path.Combine(root, "Modules", "TAOM", "bin", "Win64_Shipping_Server");
        if (!Directory.Exists(appBin) && !Directory.Exists(moduleBin))
            Assert.Inconclusive("No staged server at " + root + " (set " + StageEnvVar + ")");

        var found = new[] { appBin, moduleBin }.Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, "TAOM.Client.*")).ToList();
        CollectionAssert.AreEqual(new List<string>(), found,
            "TAOM.Client is client-only (ADR-013); the server staging promotes a stray DLL into its app bin.");

        var manifest = Path.Combine(root, "Modules", "TAOM", "SubModule.xml");
        if (File.Exists(manifest))
        {
            var names = System.Xml.Linq.XDocument.Load(manifest).Descendants("SubModule")
                .Select(s => (string)s.Element("Name")?.Attribute("value")).ToList();
            CollectionAssert.DoesNotContain(names, "TAOM.Client", "The staged TAOM manifest must not list the client submodule.");
        }
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TaomClient_AbsentFromTheInstallServerMirror()
    {
        if (!TAOM.Tests.Migration.GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("No game install resolved.");
        var serverBin = Path.Combine(TAOM.Tests.Migration.GameAssemblies.GameDir, "Modules", "TAOM", "bin", "Win64_Shipping_Server");
        if (!Directory.Exists(serverBin))
            Assert.Inconclusive("No TAOM server bin at " + serverBin);

        var found = Directory.GetFiles(serverBin, "TAOM.Client.*").ToList();
        CollectionAssert.AreEqual(new List<string>(), found,
            "Main/TAOM.csproj's server mirror must never write TAOM.Client (ADR-013).");
    }
}
