using System;
using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.ManyHammers;
using TAOM.Features.ManyHammers.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.ManyHammers;

/// <summary>
/// A Harmony patch needs its target attribute, its category attribute and an apply line, or it is dead code with no error,
/// warning or log line (lessons/harmony-il.md: Patch39 shipped missing one). Many Hammers' apply line is the module's
/// <c>PatchCategoryDecl</c> in <c>FeatureModules.All</c>, not <c>SubModule.cs</c>. These pin all three for every Patch121
/// class, plus the bindings nothing else checks: the argument names Harmony injects by.
/// </summary>
[TestClass]
public class ManyHammersWiringTests
{
    private const string Category = "Patch121_ManyHammers";

    private static readonly Type[] PatchClasses =
    {
        typeof(Patch121_SmeltingVMTrySmeltingSelectedItems),
        typeof(Patch121_RefinementVMExecuteSelectedRefinement),
        typeof(Patch121_SmeltingVMRefreshList),
    };

    private static Container BuildContainer()
    {
        var container = new Container();
        new ManyHammersModule().RegisterServices(container);
        // What the module takes from other features, which IoC.Configure registers for every game.
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheManyHammersModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<ManyHammersModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        StringAssert.Contains(src, "new Features.ManyHammers.ManyHammersModule()");
    }

    [TestMethod]
    public void TheSingleOwnerFiles_AreNotEditedForThisFeature()
    {
        Assert.IsFalse(RepoPaths.ReadSource("Main/IoC.cs", stripComments: true).Contains("ManyHammers"));
        Assert.IsFalse(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true).Contains("ManyHammers"));
    }

    [TestMethod]
    public void TheModule_IsNotParked_OwnsNoSaveData_AndDeclaresNoBehaviorOrModel()
    {
        var module = new ManyHammersModule();

        Assert.AreEqual("ManyHammers", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "nothing of this feature is saved: it repeats the engine's own unit");
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count, "the smithing model is TaomSmithingModel's; this feature reads it per unit");
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void TheModule_AppliesTheCategoryAtGameInit()
    {
        var decls = new ManyHammersModule().PatchCategories;

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual(Category, decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    public void EveryServiceTheModuleDeclares_ResolvesFromItsOwnRegistrations()
    {
        using var container = BuildContainer();

        Assert.IsNotNull(container.Resolve<IManyHammersSettingsProvider>());
        Assert.IsNotNull(container.Resolve<ManyHammersService>());
    }

    [TestMethod]
    public void TheService_IsOneSharedInstance()
    {
        using var container = BuildContainer();

        Assert.AreSame(container.Resolve<ManyHammersService>(), container.Resolve<ManyHammersService>());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryPatchClass_HasOneTargetAndTheCategory()
    {
        foreach (var patch in PatchClasses)
        {
            Assert.AreEqual(1, patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length,
                patch.Name + " lost its [HarmonyPatch] target attribute.");
            var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
                .Cast<HarmonyPatchCategory>()
                .Select(c => c.info.category)
                .ToList();
            CollectionAssert.Contains(categories, Category,
                patch.Name + " lost its [HarmonyPatchCategory]: the module's apply call would skip it.");
        }
    }

    // The three classes live in TAOM.Client (ADR-013): SmeltingVM and RefinementVM are in the client-only ViewModelCollection
    // assembly. The category pass builds its index from the loaded TAOM assemblies, so on a client the category holds them;
    // on a server (no client) it holds none and applies nothing.
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Index_FromTheLoadedTaomAssemblies_HoldsTheThreeClientClasses()
    {
        // Classes that name SandBox.View types only load once the game assemblies resolve; alone, the index skips them.
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var previous = ClientComposition.Current;
        ClientComposition.Current = new TaomClientComposition();
        try
        {
            var index = PatchCategoryIndex.Build(ClientComposition.LoadedTaomAssemblies());
            var classes = index.ClassesOf(Category);

            Assert.AreEqual(0, index.SkippedClasses.Count,
                "A skipped class belongs to no category and applies nothing: "
                + string.Join("; ", index.SkippedClasses.Select(k => k.Key.FullName + " (" + k.Value.GetType().Name + ")")));
            CollectionAssert.AreEquivalent(PatchClasses, classes.ToArray());
            foreach (var type in classes)
                Assert.AreEqual(typeof(ClientSubModule).Assembly, type.Assembly, type.Name + " must be declared in TAOM.Client.");
        }
        finally
        {
            ClientComposition.Current = previous;
        }
    }

    [TestMethod]
    public void Index_EmptyClientSlot_HoldsNone()
    {
        var previous = ClientComposition.Current;
        ClientComposition.Current = null;
        try
        {
            Assert.AreEqual(0, PatchCategoryIndex.Build(ClientComposition.LoadedTaomAssemblies()).ClassesOf(Category).Count);
        }
        finally
        {
            ClientComposition.Current = previous;
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryPatchTarget_ResolvesToOneEngineMethod_AndEveryInjectedArgumentIsNamedLikeTheTargets()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        foreach (var patch in PatchClasses)
        {
            var info = patch.GetCustomAttributes<HarmonyPatch>(inherit: false).Single().info;
            var target = AccessTools.DeclaredMethod(info.declaringType, info.methodName, info.argumentTypes);
            Assert.IsNotNull(target, patch.Name + ": " + info.declaringType?.Name + "." + info.methodName + " did not resolve.");

            var patchMethods = patch.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttributes(typeof(HarmonyPrefix), false).Length + m.GetCustomAttributes(typeof(HarmonyPostfix), false).Length > 0)
                .ToList();
            Assert.AreEqual(1, patchMethods.Count, patch.Name + " must hold exactly one prefix or postfix.");

            // Harmony injects a patch argument by NAME and type; a rename in the engine would silently bind nothing.
            foreach (var parameter in patchMethods[0].GetParameters().Where(p => !p.Name!.StartsWith("__", StringComparison.Ordinal)))
            {
                var match = target!.GetParameters().SingleOrDefault(p => p.Name == parameter.Name);
                Assert.IsNotNull(match, patch.Name + ": the target has no parameter named " + parameter.Name);
                Assert.AreEqual(match!.ParameterType, parameter.ParameterType, patch.Name + ": " + parameter.Name + " type");
            }
        }
    }

    [TestMethod]
    public void EveryPatchBody_SitsInsideATryAndStaysUnderTheEntryPointLimit()
    {
        foreach (var patch in PatchClasses)
        {
            var path = "Client/Features/ManyHammers/Hooks/" + patch.Name + ".cs";
            var src = RepoPaths.ReadSource(path, stripComments: true);
            StringAssert.Contains(src, "catch (Exception", patch.Name + " has no try/catch: a throw would reach the smithy screen.");
            var lines = System.IO.File.ReadAllLines(RepoPaths.RepoPath(path.Split('/')));
            Assert.IsTrue(lines.Length < 150, patch.Name + " is " + lines.Length + " lines (ADR-002).");
        }
    }

    [TestMethod]
    public void BothPrefixes_AlwaysLetVanillaRun()
    {
        // The plan always reserves vanilla's last unit, so a prefix never skips the original.
        // A void prefix cannot skip the original.
        foreach (var patch in new[] { typeof(Patch121_SmeltingVMTrySmeltingSelectedItems), typeof(Patch121_RefinementVMExecuteSelectedRefinement) })
        {
            var prefix = patch.GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(prefix, patch.Name);
            Assert.AreEqual(typeof(void), prefix!.ReturnType, patch.Name + " must be a void prefix: vanilla does the last unit and every refresh.");
        }
    }

    [TestMethod]
    public void TheHooks_DoNotUseReflectionOnTheViewModels()
    {
        foreach (var file in System.IO.Directory.GetFiles(RepoPaths.RepoPath("Client", "Features", "ManyHammers"), "*.cs", System.IO.SearchOption.AllDirectories))
        {
            var src = RepoPaths.ReadSource(file.Substring(RepoPaths.RepoPath().Length + 1).Replace('\\', '/'), stripComments: true);
            Assert.IsFalse(src.Contains("AccessTools.Field") || src.Contains("GetField(") || src.Contains("___"),
                file + " reads a private view-model member; use the public surface.");
        }
    }
}
