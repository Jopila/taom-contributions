using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Composition;
using TAOM.Features.PosternGate;
using TAOM.Features.PosternGate.Models;

namespace TAOM.Tests.Features.PosternGate;

/// <summary>Wiring guard for the Postern Gate (#855): the module line, its one model slot and its (empty) save footprint.</summary>
[TestClass]
public class PosternGateWiringTests
{
    [TestMethod]
    public void FeatureModules_ListThePosternGateModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<PosternGateModule>().Count());
    }

    [TestMethod]
    public void TheModule_IsNamedPosternGate_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new PosternGateModule();

        Assert.AreEqual("PosternGate", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "the building and its ids belong to TaomBuildings; this module persists nothing");
    }

    [TestMethod]
    public void TheModule_DeclaresNoPatchCategoryAndNoBehavior()
    {
        var module = new PosternGateModule();

        Assert.AreEqual(0, module.PatchCategories.Count, "the town-screen patches are Patch108, owned by TaomBuildings");
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_DeclaresOneCampaignModel_ExtendingTheDefaultTroopSacrificeModel()
    {
        var decl = new PosternGateModule().GameModels.Single();

        Assert.AreEqual(ModelTarget.Campaign, decl.Target, "a break-in or break-out exists only in a campaign");
        Assert.AreEqual(typeof(TroopSacrificeModel), decl.SlotType);
        Assert.AreEqual(typeof(TaomTroopSacrificeModel), decl.ModelType);
        Assert.AreEqual(typeof(DefaultTroopSacrificeModel), decl.ModelType.BaseType,
            "extend the model SandBox installs: the base number and any transpiler on it must keep running");
    }
}
