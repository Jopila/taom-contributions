using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Roster;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LordBanners;
using TAOM.Features.LordBanners.Models;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LordBanners;

/// <summary>
/// Wiring regression guard for lord banners (#823). Drop the module line and TAOM lords never get a banner again;
/// drop the model declaration and the map is never consulted. Methods that touch engine types carry
/// <c>RequiresGame</c>.
/// </summary>
[TestClass]
public class LordBannersWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheLordBannersModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<LordBannersModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedLordBanners_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new LordBannersModule();

        Assert.AreEqual("LordBanners", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoPatchCategory()
    {
        var module = new LordBannersModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.PatchCategories.Count);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_DeclaresTheBannerItemModel_ExtendingTheDefaultBannerItemModel()
    {
        var decl = new LordBannersModule().GameModels.Single(d => d.SlotType == typeof(BannerItemModel));

        Assert.AreEqual(ModelTarget.Campaign, decl.Target);
        Assert.AreEqual(typeof(BannerItemModel), decl.SlotType);
        Assert.AreEqual(typeof(TaomBannerItemModel), decl.ModelType);
        Assert.AreEqual(typeof(DefaultBannerItemModel), decl.ModelType.BaseType);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_DeclaresTheVassalRewardsModel_ExtendingTheDefaultOne_AndNothingElse()
    {
        var decls = new LordBannersModule().GameModels;
        var decl = decls.Single(d => d.SlotType == typeof(VassalRewardsModel));

        Assert.AreEqual(2, decls.Count);
        Assert.AreEqual(ModelTarget.Campaign, decl.Target);
        Assert.AreEqual(typeof(TaomVassalRewardsModel), decl.ModelType);
        Assert.AreEqual(typeof(DefaultVassalRewardsModel), decl.ModelType.BaseType);
    }

    private static readonly string[] ConditionalBranches =
    {
        "beq", "bge", "bgt", "ble", "blt", "bne.un", "bge.un", "bgt.un", "ble.un", "blt.un", "brtrue", "brfalse",
    };

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheVassalOverride_CallsBaseThenTheHelper_WithNoBranch()
    {
        var method = typeof(TaomVassalRewardsModel).GetMethod(nameof(TaomVassalRewardsModel.GetEquipmentRewardsForJoiningKingdom),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        var il = method.GetMethodBody()!.GetILAsByteArray()!;

        var calls = IlCallScanner.ExtractCalledMethods(method, il).ToList();
        var baseCall = calls.FindIndex(c => c.DeclaringType == typeof(DefaultVassalRewardsModel)
            && c.Name == nameof(DefaultVassalRewardsModel.GetEquipmentRewardsForJoiningKingdom));
        var helper = calls.FindIndex(c => c.DeclaringType == typeof(VassalBannerReward));

        Assert.IsTrue(baseCall >= 0, "the override must call the base roster first");
        Assert.IsTrue(helper > baseCall, "the helper runs on the base roster");
        var branches = IlCallScanner.Fingerprint(method, il).Where(tag => ConditionalBranches.Contains(tag)).ToList();
        Assert.AreEqual(0, branches.Count, "gamemodels.md rule 4: no branch in the override: " + string.Join(", ", branches));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheVassalHelper_ChecksTheRosterThenPicksFromTheServiceThenAddsOne_InThatOrder()
    {
        var method = typeof(VassalBannerReward).GetMethod("AddIfMissing", BindingFlags.NonPublic | BindingFlags.Static)!;
        var il = method.GetMethodBody()!.GetILAsByteArray()!;

        var calls = IlCallScanner.ExtractCalledMethods(method, il).ToList();
        var any = calls.FindIndex(c => c.DeclaringType == typeof(Enumerable) && c.Name == nameof(Enumerable.Any));
        var allowed = calls.FindIndex(c => c.DeclaringType == typeof(LordBannerService) && c.Name == nameof(LordBannerService.Allowed));
        var pick = calls.FindIndex(c => c.DeclaringType == typeof(TaleWorlds.Core.Extensions) && c.Name == "GetRandomElement");
        var add = calls.FindIndex(c => c.DeclaringType == typeof(ItemRoster) && c.Name == nameof(ItemRoster.AddToCounts));

        Assert.IsTrue(any >= 0, "the roster is checked for a banner");
        Assert.IsTrue(allowed > any, "the pool comes from LordBannerService.Allowed, after the roster check");
        Assert.IsTrue(pick > allowed, "one engine draw from the pool");
        Assert.IsTrue(add > pick, "the draw is added to the roster");
    }

    [TestMethod]
    public void TheModule_RegistersTheServicesAsSingletons()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IModLogger>());

        new LordBannersModule().RegisterServices(container);

        Assert.AreSame(container.Resolve<LordBannerService>(), container.Resolve<LordBannerService>());
        Assert.AreSame(container.Resolve<ILordBannerCulturesConfigProvider>(), container.Resolve<ILordBannerCulturesConfigProvider>());
        Assert.IsInstanceOfType(container.Resolve<ILordBannerCulturesConfigProvider>(), typeof(LordBannerCulturesConfigProvider));
    }
}
