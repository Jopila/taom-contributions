using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.AnimationVariety;
using TAOM.Features.AnimationVariety.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.AnimationVariety;

[TestClass]
public class GaitVarietyWiringTests
{
    [TestMethod]
    public void Module_DeclaresTheGaitBehaviorBesideTheProbeBehavior()
    {
        var types = new AnimationVarietyModule().MissionBehaviors.Select(d => d.BehaviorType).ToArray();

        CollectionAssert.AreEquivalent(
            new[] { typeof(AnimVarietyMissionBehavior), typeof(GaitVarietyMissionBehavior) }, types);
    }

    [TestMethod]
    public void GaitBehavior_StaysUnderTheEntryPointLimit()
    {
        var path = RepoPaths.RepoPath("Main", "Features", "AnimationVariety", "Hooks", "GaitVarietyMissionBehavior.cs");

        Assert.IsTrue(File.ReadAllLines(path).Length < 150);
    }

    [TestMethod]
    public void GaitBehavior_OverridesNoLifecycleVirtualThatNeverFiresForATaomBehavior()
    {
        var text = File.ReadAllText(RepoPaths.RepoPath("Main", "Features", "AnimationVariety", "Hooks", "GaitVarietyMissionBehavior.cs"));

        Assert.IsFalse(text.Contains("OnBehaviorInitialize"), "never fires for a behavior added from OnMissionBehaviorInitialize (#606)");
    }

    [TestMethod]
    public void RegisterServices_ResolvesTheGaitPartsAndKeepsOneCatalogForTheProcess()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        new AnimationVarietyModule().RegisterServices(container);

        var first = container.Resolve<GaitVariantCatalog>();
        var second = container.Resolve<GaitVariantCatalog>();

        Assert.AreSame(first, second, "existence results and missing-variant warnings are per process");
        Assert.IsInstanceOfType(container.Resolve<IGaitVarietyAdapter>(), typeof(GaitVarietyAdapter));
        Assert.IsInstanceOfType(container.Resolve<IGaitVarietySettingsProvider>(), typeof(GaitVarietySettingsProvider));
        Assert.IsInstanceOfType(container.Resolve<IAnimVarietyAdapter>(), typeof(AnimVarietyAdapter));
        Assert.IsInstanceOfType(container.Resolve<IAnimVarietyProbeSettingsProvider>(), typeof(AnimVarietyProbeSettingsProvider));
    }

    [TestMethod]
    public void SettingsProvider_WithoutMcm_ReturnsTheShippedDefaults()
    {
        var provider = new GaitVarietySettingsProvider();

        Assert.IsTrue(provider.RandomStepSize);
        Assert.AreEqual(0.92f, provider.StepSizeMinFactor);
        Assert.IsTrue(provider.RandomGaitSet);
    }

    [TestMethod]
    public void TaomSettings_DeclaresTheThreeDefaultsTheProviderFallsBackTo()
    {
        var settings = new TaomSettings();

        Assert.IsTrue(settings.EnableRandomStepSize);
        Assert.AreEqual(GaitVarietyService.DefaultMinFactor, settings.GaitStepSizeMinFactor);
        Assert.IsTrue(settings.EnableRandomGaitSet);
    }

    [TestMethod]
    public void TaomSettings_SliderBoundsMatchTheServiceRange()
    {
        var attribute = typeof(TaomSettings).GetProperty(nameof(TaomSettings.GaitStepSizeMinFactor))!
            .GetCustomAttributes(false)
            .Single(a => a.GetType().Name == "SettingPropertyFloatingIntegerAttribute");
        var type = attribute.GetType();

        Assert.AreEqual(GaitVarietyService.MinFactorFloor, (float)(decimal)type.GetProperty("MinValue")!.GetValue(attribute)!);
        Assert.AreEqual(GaitVarietyService.MinFactorCeiling, (float)(decimal)type.GetProperty("MaxValue")!.GetValue(attribute)!);
    }

    [TestMethod]
    public void TaomSettings_StepSizeHint_StatesTheFactorRangeAgainstTheResultingStepSize()
    {
        var hint = AnimVarietyWiringTests.HintOf(nameof(TaomSettings.GaitStepSizeMinFactor));

        // The factor multiplies vanilla's step size, min(0.8 + 0.2 * Athletics / 300, 1), so the applied
        // value goes below 0.80: 0.92 * 0.80 = 0.736 and 0.80 * 0.80 = 0.64.
        StringAssert.Contains(hint, "Athletics");
        StringAssert.Contains(hint, "0.74");
        StringAssert.Contains(hint, "0.64");
        Assert.IsFalse(hint.Contains("stays inside what the game shows"), "the factor is not the final step size");
    }

    [TestMethod]
    public void GaitAgent_HorseSlot_IsReadByItsName()
    {
        var text = File.ReadAllText(RepoPaths.RepoPath("Main", "Adapters", "GaitVarietyAgent.cs"));

        StringAssert.Contains(text, "EquipmentIndex.Horse");
        Assert.IsFalse(text.Contains("ArmorItemEndSlot"));
    }

    [TestMethod]
    public void GaitBehavior_BuildsItsWarningDelegateOnce()
    {
        var text = File.ReadAllText(RepoPaths.RepoPath("Main", "Features", "AnimationVariety", "Hooks", "GaitVarietyMissionBehavior.cs"));

        StringAssert.Contains(text, "private readonly Action<string> _warn;");
        Assert.IsFalse(text.Contains("m => _logger.LogWarning(m)"), "a lambda per spawn would allocate on every OnAgentBuild");
    }
}
