using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CrownGuard;
using TAOM.Features.CrownGuard.Hooks;

namespace TAOM.Tests.Features.CrownGuard;

/// <summary>
/// Wiring regression guard for Crown Guard (#825). Drop the module line and the patch category is never applied;
/// drop the category on the hook and the postfix never attaches.
/// </summary>
[TestClass]
public class CrownGuardWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheCrownGuardModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<CrownGuardModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedCrownGuard_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new CrownGuardModule();

        Assert.AreEqual("CrownGuard", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AppliesPatch112AtGameInit_AndOnlyThat()
    {
        var decls = new CrownGuardModule().PatchCategories;

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch112_CrownGuard", decls[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, decls[0].Phase);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheHook_CarriesTheModulesPatchCategory_AndAPostfix()
    {
        var type = typeof(CampaignSceneNotificationHelper_GetBodyguardOfCulture_CrownGuard_Patch);

        var categories = type.GetCustomAttributes<HarmonyPatchCategory>().Select(c => c.info.category).ToList();

        CollectionAssert.AreEqual(new[] { "Patch112_CrownGuard" }, categories);
        Assert.IsTrue(type.GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static)!.GetCustomAttributes<HarmonyPostfix>().Any());
    }

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoModel()
    {
        var module = new CrownGuardModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void TheModule_RegistersTheConfigProviderAsASingleton()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IModLogger>());

        new CrownGuardModule().RegisterServices(container);

        Assert.AreSame(container.Resolve<CrownGuardConfigProvider>(), container.Resolve<CrownGuardConfigProvider>());
    }
}
