using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Composition;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.VoiceAccents;
using TAOM.Features.VoiceAccents.Models;

namespace TAOM.Tests.Features.VoiceAccents;

/// <summary>
/// Wiring regression guard for the dialogue voice map (#821). Drop the module line and every TAOM character
/// speaks a random vanilla voice again; drop the model declaration and the map is never consulted. Methods that
/// touch engine types carry <c>RequiresGame</c>.
/// </summary>
[TestClass]
public class VoiceAccentsWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheVoiceAccentsModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<VoiceAccentsModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedVoiceAccents_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new VoiceAccentsModule();

        Assert.AreEqual("VoiceAccents", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
    }

    [TestMethod]
    public void TheModule_AddsNoBehaviorAndNoPatchCategory()
    {
        var module = new VoiceAccentsModule();

        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.PatchCategories.Count);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_DeclaresOneCampaignModel_ExtendingTheDefaultVoiceOverModel()
    {
        var decl = new VoiceAccentsModule().GameModels.Single();

        Assert.AreEqual(ModelTarget.Campaign, decl.Target);
        Assert.AreEqual(typeof(VoiceOverModel), decl.SlotType);
        Assert.AreEqual(typeof(TaomVoiceOverModel), decl.ModelType);
        Assert.AreEqual(typeof(DefaultVoiceOverModel), decl.ModelType.BaseType);
    }

    [TestMethod]
    public void TheModule_RegistersTheServicesAsSingletons()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance<IRaceManager>(Substitute.For<IRaceManager>());
        container.RegisterInstance(Substitute.For<IModLogger>());

        new VoiceAccentsModule().RegisterServices(container);

        Assert.AreSame(container.Resolve<VoiceAccentService>(), container.Resolve<VoiceAccentService>());
        Assert.AreSame(container.Resolve<IVoiceAccentsConfigProvider>(), container.Resolve<IVoiceAccentsConfigProvider>());
        Assert.IsInstanceOfType(container.Resolve<IVoiceAccentsConfigProvider>(), typeof(VoiceAccentsConfigProvider));
    }
}
