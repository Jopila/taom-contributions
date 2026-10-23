using System;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.HostReview;
using TAOM.Features.HostReview.Domain;
using TAOM.Features.HostReview.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HostReview;

/// <summary>
/// Wiring guard for Host Review (#891): the module is listed once, declares its behavior, and registers
/// everything the menu option needs. Drop a link and the camp menu silently loses the option. The source
/// pins read the shipped files because the menu, the mission and the launcher need the game to run.
/// </summary>
[TestClass]
public class HostReviewWiringTests
{
    private const string BehaviorPath = "Main/Features/HostReview/Hooks/HostReviewCampaignBehavior.cs";
    private const string LogicPath = "Main/Features/HostReview/Hooks/HostReviewMissionLogic.cs";
    private const string LauncherPath = "Main/Features/HostReview/Hooks/HostReviewMissionLauncher.cs";

    private static Container BuildContainer()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IEnlistmentStateQuery>());
        container.RegisterInstance(Substitute.For<IEncounterAdapter>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        new HostReviewModule().RegisterServices(container);
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheHostReviewModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<HostReviewModule>().Count(),
            "HostReviewModule must be listed exactly once in Main/Composition/FeatureModules.cs.");
    }

    [TestMethod]
    public void Module_RegistersTheNullOrderProviderRegistrar()
    {
        using var container = BuildContainer();

        Assert.IsInstanceOfType(container.Resolve<IHostReviewOrderProviderRegistrar>(), typeof(NullHostReviewOrderProviderRegistrar));
    }

    [TestMethod]
    public void OnPhase_ProcessLoad_RegistersTheOrderProviderOnce_AndNoOtherPhaseDoes()
    {
        using var container = BuildContainer();
        var registrar = Substitute.For<IHostReviewOrderProviderRegistrar>();
        container.RegisterInstance(registrar, IfAlreadyRegistered.Replace);
        var module = new HostReviewModule();

        foreach (var phase in Enum.GetValues(typeof(ApplyPhase)).Cast<ApplyPhase>().Where(p => p != ApplyPhase.ProcessLoad))
            module.OnPhase(phase, container);
        registrar.DidNotReceive().Register();

        module.OnPhase(ApplyPhase.ProcessLoad, container);
        registrar.Received(1).Register();
    }

    [TestMethod]
    public void Module_NamesNoClientOrderType()
    {
        var src = RepoPaths.ReadSource("Main/Features/HostReview/HostReviewModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("VisualOrderFactory"));
        Assert.IsFalse(src.Contains("HostReviewVisualOrderProvider"));
        Assert.IsFalse(src.Contains("Order.Visual"));
    }

    [TestMethod]
    public void Module_DeclaresTheCampaignBehavior()
    {
        var decls = new HostReviewModule().CampaignBehaviors;

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual(typeof(HostReviewCampaignBehavior), decls[0].BehaviorType);
    }

    [TestMethod]
    public void Module_RegistersTheServiceAsASingleton()
    {
        using var container = BuildContainer();

        Assert.AreSame(container.Resolve<HostReviewService>(), container.Resolve<HostReviewService>());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void BehaviorDecl_BuildsTheBehaviorOverTheService()
    {
        // Building a CampaignBehaviorBase runs engine code.
        using var container = BuildContainer();

        var behavior = new HostReviewModule().CampaignBehaviors.Single().Create(container);

        Assert.IsInstanceOfType(behavior, typeof(HostReviewCampaignBehavior));
    }

    [TestMethod]
    public void Behavior_AddsTheOptionAtIndexFiveOnTheCampSubMenu()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        StringAssert.Contains(src, "OptionId = \"taom_fc_review\"");
        var anchor = "FieldCampCampaignBehavior.CampSubMenuId, OptionId";
        var start = src.IndexOf(anchor, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "the option is no longer added to FieldCampCampaignBehavior.CampSubMenuId.");
        var end = src.IndexOf("AddGameMenuOption", start + anchor.Length, StringComparison.Ordinal);
        var call = src.Substring(start, (end < 0 ? src.Length : end) - start);
        StringAssert.Contains(call, "isLeave: false");
        StringAssert.Contains(call, "index: 5",
            "index 4 is the Refuge slot and the Leave option follows it; Host Review sits between them.");
    }

    [TestMethod]
    public void Behavior_AddsTheOptionAfterEverySessionLaunchListener()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        StringAssert.Contains(src, "CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener",
            "OnAfterSessionLaunched fires after every OnSessionLaunched listener, so index 5 lands after Refuge's option.");
        Assert.IsFalse(src.Contains("CampaignEvents.OnSessionLaunchedEvent"),
            "listener order inside OnSessionLaunched is not pinned, so the option must not be added there.");
    }

    [TestMethod]
    public void Behavior_ReasonTextNamesEveryBlockExceptNone()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        foreach (var name in Enum.GetNames(typeof(HostReviewBlock)).Where(n => n != nameof(HostReviewBlock.None)))
            StringAssert.Contains(src, "case HostReviewBlock." + name + ":",
                $"ReasonText has no explicit case for {name}; a new block would fall into the default text.");
    }

    [TestMethod]
    public void MissionLogic_SetsDisableDyingInsideOnAfterDeploymentFinished()
    {
        var src = RepoPaths.ReadSource(LogicPath, stripComments: true);

        var start = src.IndexOf("void OnAfterDeploymentFinished", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "OnAfterDeploymentFinished override is gone.");
        var end = src.IndexOf("public override", start + 10, StringComparison.Ordinal);
        var body = src.Substring(start, (end < 0 ? src.Length : end) - start);
        StringAssert.Contains(body, "Mission.DisableDying = true",
            "FinishDeployment resets DisableDying to false just before this callback, so it must be set here.");
    }

    [TestMethod]
    public void Launcher_BehaviorListContainsTheHostReviewMissionLogic()
    {
        var src = RepoPaths.ReadSource(LauncherPath, stripComments: true);

        StringAssert.Contains(src, "new HostReviewMissionLogic(",
            "without the logic the mission is neither harmless nor recognised as a review.");
    }
}
