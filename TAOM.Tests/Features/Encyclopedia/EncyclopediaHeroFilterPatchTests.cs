using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Encyclopedia.Pages;
using TaleWorlds.Localization;
using TAOM.Composition;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Encyclopedia;
using TAOM.Features.Encyclopedia.Adapters;
using TAOM.Features.Encyclopedia.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Encyclopedia;

/// <summary>
/// Patch119 (#907): the postfix on DefaultEncyclopediaHeroPage.InitializeFilterItems appends the Race and Age groups.
/// The co-op module postfixes the same method with a lazy AddItem, so the result is never cast to a List.
/// </summary>
[TestClass]
public class EncyclopediaHeroFilterPatchTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static EncyclopediaFilterGroup Group(string name) =>
        new(new List<EncyclopediaFilterItem>(), new TextObject(name));

    private static IEncyclopediaHeroFilterAdapter Adapter(params EncyclopediaFilterGroup[] groups)
    {
        var adapter = Substitute.For<IEncyclopediaHeroFilterAdapter>();
        adapter.BuildGroups().Returns(groups);
        return adapter;
    }

    private static IEnumerable<EncyclopediaFilterGroup> Throwing()
    {
        yield return Group("first");
        throw new InvalidOperationException("a later postfix's lazy source threw");
    }

    [TestMethod]
    public void Postfix_KeepsALazyConcatInputInOrder_AndAppendsTwo()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        var vanilla = new[] { Group("a"), Group("b") };
        var lazy = vanilla.Where(_ => true).Concat(new[] { Group("coop") });
        DefaultEncyclopediaHeroPageInitializeFilterItemsPatch.Initialize(Adapter(Group("race"), Group("age")), Substitute.For<IModLogger>());
        IEnumerable<EncyclopediaFilterGroup> result = lazy;

        DefaultEncyclopediaHeroPageInitializeFilterItemsPatch.Postfix(ref result);

        var names = result.Select(g => g.Name.ToString()).ToList();
        CollectionAssert.AreEqual(new[] { "a", "b", "coop", "race", "age" }, names);
    }

    [TestMethod]
    public void Postfix_NullResult_StillAppendsTheTwoGroups()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        DefaultEncyclopediaHeroPageInitializeFilterItemsPatch.Initialize(Adapter(Group("race"), Group("age")), Substitute.For<IModLogger>());
        IEnumerable<EncyclopediaFilterGroup> result = null!;

        DefaultEncyclopediaHeroPageInitializeFilterItemsPatch.Postfix(ref result);

        Assert.AreEqual(2, result!.Count());
    }

    [TestMethod]
    public void Postfix_WhenTheSourceThrows_LeavesTheResultUntouched()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        var logger = Substitute.For<IModLogger>();
        DefaultEncyclopediaHeroPageInitializeFilterItemsPatch.Initialize(Adapter(Group("race"), Group("age")), logger);
        var source = Throwing();
        var result = source;

        DefaultEncyclopediaHeroPageInitializeFilterItemsPatch.Postfix(ref result);

        Assert.AreSame(source, result);
        logger.Received().LogError(NSubstitute.Arg.Any<string>());
    }

    [TestMethod]
    public void Postfix_WhenTheAdapterThrows_LeavesTheResultUntouched()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        var adapter = Substitute.For<IEncyclopediaHeroFilterAdapter>();
        adapter.BuildGroups().Returns(_ => throw new InvalidOperationException("boom"));
        DefaultEncyclopediaHeroPageInitializeFilterItemsPatch.Initialize(adapter, Substitute.For<IModLogger>());
        IEnumerable<EncyclopediaFilterGroup> source = new[] { Group("a") };
        var result = source;

        DefaultEncyclopediaHeroPageInitializeFilterItemsPatch.Postfix(ref result);

        Assert.AreSame(source, result);
    }

    [TestMethod]
    public void Postfix_WithNoAdapter_LeavesTheResultUntouched()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        DefaultEncyclopediaHeroPageInitializeFilterItemsPatch.Initialize(null, null);
        IEnumerable<EncyclopediaFilterGroup> source = new[] { Group("a") };
        var result = source;

        DefaultEncyclopediaHeroPageInitializeFilterItemsPatch.Postfix(ref result);

        Assert.AreSame(source, result);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGame")]
    public void Target_IsTheProtectedOverrideReturningTheFilterGroups()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = AccessTools.Method(typeof(DefaultEncyclopediaHeroPage), "InitializeFilterItems");

        Assert.IsNotNull(target, "DefaultEncyclopediaHeroPage.InitializeFilterItems moved or was renamed");
        Assert.AreEqual(typeof(DefaultEncyclopediaHeroPage), target.DeclaringType, "the override must be declared on the hero page");
        Assert.AreEqual(typeof(IEnumerable<EncyclopediaFilterGroup>), target.ReturnType);
        Assert.AreEqual(0, target.GetParameters().Length);
        Assert.IsFalse(target.IsStatic);
        Assert.IsTrue(target.IsVirtual, "it overrides EncyclopediaPage.InitializeFilterItems");
    }

    [TestMethod]
    public void Postfix_TakesTheResultByRef_NamedUnderscoreUnderscoreResult()
    {
        var parameter = typeof(DefaultEncyclopediaHeroPageInitializeFilterItemsPatch)
            .GetMethod("Postfix")!.GetParameters().Single();

        Assert.AreEqual("__result", parameter.Name);
        Assert.IsTrue(parameter.ParameterType.IsByRef);
        Assert.AreEqual(typeof(IEnumerable<EncyclopediaFilterGroup>), parameter.ParameterType.GetElementType());
    }

    [TestMethod]
    public void PatchClass_IsInTheLiteralCategory()
    {
        var categories = typeof(DefaultEncyclopediaHeroPageInitializeFilterItemsPatch)
            .GetCustomAttributes<HarmonyPatchCategory>().Select(c => c.info.category).ToList();

        CollectionAssert.AreEqual(new[] { "Patch119_EncyclopediaHeroFilters" }, categories);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PatchClass_AppliedToTheInstalledEngine_PostfixesTheTargetAndTheJitAccepts()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        const string id = "taom.tests.encyclopediaherofilters.jit";
        var harmony = new Harmony(id);
        var target = AccessTools.Method(typeof(DefaultEncyclopediaHeroPage), "InitializeFilterItems");
        try
        {
            PatchCategoryIndex.Build(typeof(EncyclopediaFiltersModule).Assembly).Apply(harmony, EncyclopediaFiltersModule.PatchCategory);

            var info = Harmony.GetPatchInfo(target);
            Assert.IsNotNull(info, "InitializeFilterItems was not patched");
            Assert.IsTrue(info!.Postfixes.Any(p => p.owner == id), "no TAOM postfix");
            Assert.AreEqual(0, info.Prefixes.Count(p => p.owner == id), "the rule must be a postfix only");
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(target.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<EncyclopediaFiltersModule>().Count());

    [TestMethod]
    public void TheModule_IsNamed_NotParked_OwnsNoSaveData()
    {
        var module = new EncyclopediaFiltersModule();

        Assert.AreEqual("EncyclopediaFilters", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AppliesItsPatchAtProcessLoad_NotGameInit()
    {
        // SandBox builds the encyclopedia pages in its own OnGameInitializationFinished (SandBoxSubModule.cs:117,125),
        // which runs before TAOM's, so a GameInit patch would miss the first campaign.
        var decls = new EncyclopediaFiltersModule().PatchCategories.ToList();

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch119_EncyclopediaHeroFilters", decls[0].Category);
        Assert.AreEqual(ApplyPhase.ProcessLoad, decls[0].Phase);
    }

    [TestMethod]
    public void TheModule_RegistersTheProviderAndTheAdapter()
    {
        using var c = new Container();
        c.RegisterInstance(Substitute.For<IPathService>());
        c.RegisterInstance(Substitute.For<IModLogger>());
        c.RegisterInstance(Substitute.For<IRaceManager>());

        new EncyclopediaFiltersModule().RegisterServices(c);

        Assert.IsInstanceOfType(c.Resolve<IHeroFilterConfigProvider>(), typeof(HeroFilterConfigProvider));
        Assert.IsInstanceOfType(c.Resolve<IEncyclopediaHeroFilterAdapter>(), typeof(EncyclopediaHeroFilterAdapter));
    }

    [TestMethod]
    public void TheCategory_IsNotRegisteredByHandInIoCOrSubModule()
    {
        foreach (var file in new[] { "Main/IoC.cs", "Main/SubModule.cs" })
        {
            var src = RepoPaths.ReadSource(file, stripComments: true);
            Assert.IsFalse(src.Contains("Patch119") || src.Contains("EncyclopediaFilters"), file + " must not name the module");
        }
    }
}
