using System;
using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.ArmyManagement;
using TaleWorlds.Localization;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.CulturalFeats.Models;
using TAOM.Features.RedArrow;
using TAOM.Features.RedArrow.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.RedArrow;

/// <summary>Wiring guard for the Red Arrow (#873): the module line, the patch category, an army-management model with no Red Arrow seam, and the engine members the two postfixes touch.</summary>
[TestClass]
public class RedArrowWiringTests
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [TestMethod]
    public void FeatureModules_ListTheRedArrowModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<RedArrowModule>().Count());
    }

    [TestMethod]
    public void TheModule_IsNamedRedArrow_IsNotParked_OwnsNoSaveData_AndDeclaresOnePatchCategoryAtGameInit()
    {
        var module = new RedArrowModule();

        Assert.AreEqual("RedArrow", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count, "the feature adds no model");
        var category = module.PatchCategories.Single();
        Assert.AreEqual("Patch115_RedArrow", category.Category);
        Assert.AreEqual(ApplyPhase.GameInit, category.Phase);
    }

    [TestMethod]
    public void TheModule_RegistersABoundaryThatResolvesFromARealContainer()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        new RedArrowModule().RegisterServices(container);

        var boundary = container.Resolve<RedArrowArmyBoundary>();

        Assert.AreSame(boundary, container.Resolve<RedArrowArmyBoundary>(), "one shared instance");
        Assert.AreSame(container.Resolve<RedArrowService>(), container.Resolve<RedArrowService>());
        Assert.IsInstanceOfType(container.Resolve<IRedArrowSettingsProvider>(), typeof(RedArrowSettingsProvider));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TaomArmyManagementModel_DoesNotOverrideTheEligibilityCheck()
    {
        var overridden = typeof(TaomArmyManagementModel)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToList();

        // Only this one name is pinned here: the model's full override list belongs to KingsMusterWiringTests, so a later feature
        // that adds a method to the model does not have to edit this test.
        CollectionAssert.DoesNotContain(overridden, "CheckPartyEligibility",
            "the Red Arrow adds no model seam: the eligibility check stays vanilla's and the view-model postfix redoes it");
        Assert.AreEqual(typeof(DefaultArmyManagementCalculationModel), typeof(TaomArmyManagementModel).BaseType);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void BothPatches_ResolveTheirTarget_AndTheTextFieldBinds()
    {
        Assert.IsNotNull(AccessTools.Method(typeof(ArmyManagementItemVM), nameof(ArmyManagementItemVM.UpdateEligibility), Type.EmptyTypes),
            "UpdateEligibility() is public and has no overload");
        Assert.IsNotNull(AccessTools.Constructor(typeof(ArmyManagementItemVM), ConstructorArguments),
            "the four-argument constructor");
        Assert.IsNotNull(AccessTools.FieldRefAccess<ArmyManagementItemVM, TextObject>("_eligibilityReason"),
            "the private text the postfix rewrites");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void BothPatches_PrepareTrue_AgainstTheInstalledEngine()
    {
        Assert.IsTrue(ArmyManagementItemVM_UpdateEligibility_RedArrow_Patch.Prepare());
        Assert.IsTrue(ArmyManagementItemVM_Constructor_RedArrow_Patch.Prepare());
    }

    // Harmony binds postfix parameters by name: pin them (lessons/harmony-il.md, "Harmony binds prefix/postfix parameters by NAME").
    [DataTestMethod]
    [DataRow(typeof(ArmyManagementItemVM_UpdateEligibility_RedArrow_Patch))]
    [DataRow(typeof(ArmyManagementItemVM_Constructor_RedArrow_Patch))]
    [TestCategory("RequiresGame")]
    public void ThePostfix_BindsTheInstanceByHarmonysName_AndEachPatchIsClientOnlyInTheRedArrowCategory(Type patch)
    {
        var postfix = patch.GetMethod("Postfix")!;

        CollectionAssert.AreEqual(new[] { "__instance" }, postfix.GetParameters().Select(p => p.Name).ToArray());
        Assert.IsTrue(patch.IsDefined(typeof(ClientOnlyAttribute), false), "its target lives in a client-only assembly");
        var category = (HarmonyPatchCategory)patch.GetCustomAttributes(typeof(HarmonyPatchCategory), false).Single();
        Assert.AreEqual("Patch115_RedArrow", category.info.category);
    }

    private static readonly Type[] ConstructorArguments =
    {
        typeof(Action<ArmyManagementItemVM>), typeof(Action<ArmyManagementItemVM>), typeof(Action<ArmyManagementItemVM>), typeof(MobileParty),
    };

    [TestMethod]
    public void TheSingleOwnerFiles_AndTheModel_NameNothingOfTheFeature()
    {
        var ioc = RepoPaths.ReadSource("Main/IoC.cs");
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs");
        var model = RepoPaths.ReadSource("Main/Features/CulturalFeats/Models/TaomArmyManagementModel.cs");

        Assert.IsFalse(ioc.Contains("RedArrow"), "IoC.cs is single-owner: the module registers through FeatureModules.cs");
        Assert.IsFalse(subModule.Contains("RedArrow"), "SubModule.cs is single-owner and the feature needs no line in it");
        Assert.IsFalse(model.Contains("RedArrow"), "the army-management model carries no Red Arrow seam");
    }
}
