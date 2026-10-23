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
/// ADR-013 B6c: OnSubModuleLoad and OnGameInitializationFinished skip, on a dedicated server, every step that
/// reaches a client-only assembly. Each such step sits behind the engine's build constant (read through
/// <c>ClientComposition.IsDedicatedServer()</c>), and where the step names a client type, in a NoInlining helper a
/// server never compiles. The source rows match the exact condition, so a removed or inverted check fails (the B4c
/// Codex F1 lesson behind <see cref="SubModuleMissionServerGuardTests"/>).
/// </summary>
[TestClass]
public class SubModuleServerGuardTests
{
    private const string OffServer = @"if \(!ClientComposition\.IsDedicatedServer\(\)\)\s*";

    private static readonly string[] Helpers =
    {
        "RegisterHotKeyCategories",
        "InitializeFactionUi",
        "ApplyTooltipProbes",
        "InitializeCustomBattleHooks",
        "InitializeBannerEditorHook",
        "ApplyKingdomVoteDeadlockFix",
        "ApplyTournamentExitMovieRelease",
    };

    private const BindingFlags AllDeclared = BindingFlags.Instance | BindingFlags.Static
        | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    // SandBox, CustomBattle and the other module assemblies are not in the test bin; the IL walk resolves them.
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    private static string Source() => RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

    [TestMethod]
    [DataRow(OffServer + @"_timeAccelerationService = IoC\.Resolve<ITimeAccelerationService>\(\);")]
    [DataRow(OffServer + @"RegisterHotKeyCategories\(\);")]
    [DataRow(OffServer + @"InitializeFactionUi\(\);")]
    [DataRow(OffServer + @"ApplyTooltipProbes\(\);")]
    [DataRow(OffServer + @"InitializeCustomBattleHooks\(logger\);")]
    [DataRow(OffServer + @"InitializeBannerEditorHook\(bannerColorService, logger\);")]
    [DataRow(OffServer + @"Features\.MapPerf\.Hooks\.MapFrameProfilerInstaller\.OnGameInitialized\(")]
    [DataRow(OffServer + @"ApplyKingdomVoteDeadlockFix\(\);")]
    [DataRow(OffServer + @"ApplyTournamentExitMovieRelease\(\);")]
    [DataRow(@"if \(ClientComposition\.IsDedicatedServer\(\)\)\s*Features\.CoopInterop\.ServerAssemblyReport\.Log\(")]
    public void StartupStep_SitsBehindTheServerCheck_Once(string pattern)
    {
        Assert.AreEqual(1, Regex.Matches(Source(), pattern).Count, "expected exactly one match of " + pattern);
    }

    // The server report runs first in the once-per-process block, so a throw in the code below it (code a server
    // runs for the first time) cannot lose the line: after the latch, before the GameInitOnce stamp and the batch.
    [TestMethod]
    public void ServerAssemblyReport_IsFirstInTheOncePerProcessBlock()
    {
        var src = Source();
        int latch = src.IndexOf("_gameInitPatchesApplied = true;", StringComparison.Ordinal);
        int report = src.IndexOf("ServerAssemblyReport.Log(", StringComparison.Ordinal);
        int stamp = src.IndexOf("StartHook(\"GameInitOnce\"", StringComparison.Ordinal);
        Assert.IsTrue(latch >= 0 && stamp > latch, "an anchor moved");
        Assert.IsTrue(report > latch && report < stamp, "the report sits between the latch and the GameInitOnce stamp");
        Assert.AreEqual("", Regex.Replace(src.Substring(latch, report - latch),
                @"_gameInitPatchesApplied = true;|if \(ClientComposition\.IsDedicatedServer\(\)\)\s*Features\.CoopInterop\.|\s", ""),
            "nothing runs between the latch and the report");
    }

    [TestMethod]
    public void EachHelper_IsCalledOnce()
    {
        var src = Source();
        foreach (var name in Helpers)
            Assert.AreEqual(1, Regex.Matches(src, @"(?<!void )\b" + name + @"\(").Count, name + " must be called exactly once");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void EachHelper_IsNoInlining()
    {
        foreach (var name in Helpers)
        {
            var method = typeof(TAOM.SubModule).GetMethod(name, AllDeclared)
                ?? throw new AssertFailedException("Missing helper " + name);
            Assert.IsTrue(method.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining),
                name + " must be NoInlining so its client references never land in a caller a server compiles");
        }
    }

    /// <summary>
    /// A pin: the hooks reach client code only through callees, never in their own IL, so the guards above are
    /// enough. A client type named directly in a SubModule method (or a lambda's closure class) fails the JIT of
    /// that method on a server before the guard runs.
    /// </summary>
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void SubModuleMethods_ReferenceNoClientAssemblyDirectly()
    {
        var methods = TypeAndNested(typeof(TAOM.SubModule))
            .SelectMany(t => t.GetMethods(AllDeclared).Cast<MethodBase>().Concat(t.GetConstructors(AllDeclared)))
            .ToList();
        Assert.IsTrue(methods.Count > 20, "the walk found only " + methods.Count + " methods");

        var offenders = methods
            .Select(m => (Name: m.DeclaringType!.FullName + "." + m.Name, Refs: IlClientReferences.ClientReferences(m)))
            .Where(x => x.Refs.Count > 0)
            .Select(x => x.Name + " -> " + string.Join(", ", x.Refs)).ToList();
        Assert.AreEqual(0, offenders.Count, "SubModule methods naming a client-only type: " + string.Join("; ", offenders));
    }

    private static IEnumerable<Type> TypeAndNested(Type type)
    {
        yield return type;
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            foreach (var t in TypeAndNested(nested))
                yield return t;
    }
}
