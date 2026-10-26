using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.SmithsMeasure;

namespace TAOM.Tests.Features.SmithsMeasure;

/// <summary>
/// The Make Tools verdict, cost and experience (#910) on a fake workshop. Base values are the engine's own: tools 250, iron 60,
/// hardwood 25, so the value added is 165 and 30 percent of it is 49.5.
/// </summary>
[TestClass]
public class MakeToolsServiceTests
{
    private ISmithsMeasureSettingsProvider _settings = null!;
    private ICraftingWorkshopAdapter _workshop = null!;
    private MakeToolsService _service = null!;

    [TestInitialize]
    public void Init()
    {
        _settings = Substitute.For<ISmithsMeasureSettingsProvider>();
        _settings.MakeToolsEnabled.Returns(true);
        _workshop = Substitute.For<ICraftingWorkshopAdapter>();
        _workshop.IronCount.Returns(1);
        _workshop.WoodCount.Returns(1);
        _workshop.Stamina.Returns(50);
        _workshop.RefiningCost.Returns(6);
        _workshop.ToolsValue.Returns(250);
        _workshop.IronValue.Returns(60);
        _workshop.WoodValue.Returns(25);
        _service = new MakeToolsService(_settings);
    }

    [TestMethod]
    public void WithBothInputsAndEnoughStamina_TheVerdictIsReady()
    {
        var quote = _service.Quote(_workshop);

        Assert.AreEqual(MakeToolsVerdict.Ready, quote.Verdict);
        Assert.AreEqual(6, quote.StaminaCost);
        Assert.AreEqual(49.5f, quote.SkillXp, 0.0001f);
    }

    [TestMethod]
    public void NoIron_NeedsIron()
    {
        _workshop.IronCount.Returns(0);

        Assert.AreEqual(MakeToolsVerdict.NeedsIron, _service.Quote(_workshop).Verdict);
    }

    [TestMethod]
    public void NoHardwood_NeedsHardwood()
    {
        _workshop.WoodCount.Returns(0);

        Assert.AreEqual(MakeToolsVerdict.NeedsHardwood, _service.Quote(_workshop).Verdict);
    }

    [TestMethod]
    public void NeitherInput_NamesTheIronFirst()
    {
        _workshop.IronCount.Returns(0);
        _workshop.WoodCount.Returns(0);

        Assert.AreEqual(MakeToolsVerdict.NeedsIron, _service.Quote(_workshop).Verdict);
    }

    [TestMethod]
    public void ANegativeCount_CountsAsMissing()
    {
        _workshop.IronCount.Returns(-3);

        Assert.AreEqual(MakeToolsVerdict.NeedsIron, _service.Quote(_workshop).Verdict);
    }

    [TestMethod]
    public void StaminaOneUnderTheCost_IsTooTired()
    {
        _workshop.Stamina.Returns(5);

        Assert.AreEqual(MakeToolsVerdict.TooTired, _service.Quote(_workshop).Verdict);
    }

    [TestMethod]
    public void StaminaEqualToTheCost_AboveTheVanillaFloor_IsEnough()
    {
        _workshop.RefiningCost.Returns(12);
        _workshop.Stamina.Returns(12);

        Assert.AreEqual(MakeToolsVerdict.Ready, _service.Quote(_workshop).Verdict);
    }

    [TestMethod]
    public void StaminaOfTen_IsTooTired_EvenWhenTheCostIsLower()
    {
        _workshop.Stamina.Returns(10);

        Assert.AreEqual(MakeToolsVerdict.TooTired, _service.Quote(_workshop).Verdict, "vanilla allows a smithy action above 10 only");
    }

    [TestMethod]
    public void StaminaOfEleven_WithACostOfSix_IsReady()
    {
        _workshop.Stamina.Returns(11);

        Assert.AreEqual(MakeToolsVerdict.Ready, _service.Quote(_workshop).Verdict);
    }

    [TestMethod]
    public void MissingInputs_AreReportedBeforeTiredness()
    {
        _workshop.Stamina.Returns(0);
        _workshop.WoodCount.Returns(0);

        Assert.AreEqual(MakeToolsVerdict.NeedsHardwood, _service.Quote(_workshop).Verdict);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    [DataRow(int.MinValue)]
    public void ACostUnderOne_FromTheModel_IsFlooredAtOne(int modelCost)
    {
        _workshop.RefiningCost.Returns(modelCost);

        var quote = _service.Quote(_workshop);

        Assert.AreEqual(1, quote.StaminaCost);
        Assert.AreEqual(MakeToolsVerdict.Ready, quote.Verdict);
    }

    [TestMethod]
    public void ACostOfOne_IsKeptAtOne()
    {
        _workshop.RefiningCost.Returns(1);

        Assert.AreEqual(1, _service.Quote(_workshop).StaminaCost);
    }

    [TestMethod]
    public void TheCostFloor_IsWhatTheStaminaGateAsks()
    {
        _workshop.RefiningCost.Returns(-9);
        _workshop.Stamina.Returns(0);

        Assert.AreEqual(MakeToolsVerdict.TooTired, _service.Quote(_workshop).Verdict, "even a free craft costs one");
    }

    [TestMethod]
    public void TheExperience_IsThirtyPercentOfTheValueAdded()
    {
        _workshop.ToolsValue.Returns(1000);
        _workshop.IronValue.Returns(100);
        _workshop.WoodValue.Returns(100);

        Assert.AreEqual(240f, _service.Quote(_workshop).SkillXp, 0.0001f);
    }

    [DataTestMethod]
    [DataRow(250, 200, 100)]
    [DataRow(100, 100, 0)]
    [DataRow(0, 60, 25)]
    [DataRow(-10, 60, 25)]
    public void WhenTheInputsAreWorthAsMuchAsTheTools_TheExperienceIsZero_NeverNegative(int tools, int iron, int wood)
    {
        _workshop.ToolsValue.Returns(tools);
        _workshop.IronValue.Returns(iron);
        _workshop.WoodValue.Returns(wood);

        Assert.AreEqual(0f, _service.Quote(_workshop).SkillXp);
    }

    [TestMethod]
    public void HugeValues_DoNotOverflowTheExperience()
    {
        _workshop.ToolsValue.Returns(int.MaxValue);
        _workshop.IronValue.Returns(int.MinValue);
        _workshop.WoodValue.Returns(int.MinValue);

        var xp = _service.Quote(_workshop).SkillXp;

        Assert.IsTrue(xp > 0f && !float.IsInfinity(xp) && !float.IsNaN(xp));
    }

    [TestMethod]
    public void WhenSwitchedOff_TheVerdictIsDisabled_AndTheWorkshopIsNotRead()
    {
        _settings.MakeToolsEnabled.Returns(false);
        _workshop.ClearReceivedCalls();

        var quote = _service.Quote(_workshop);

        Assert.AreEqual(MakeToolsVerdict.Disabled, quote.Verdict);
        Assert.AreEqual(0, _workshop.ReceivedCalls().Count());
    }

    [TestMethod]
    public void TryMake_WhenReady_TakesTheInputs_GivesTheTools_SpendsStamina_AndPaysExperience()
    {
        var made = _service.TryMake(_workshop);

        Assert.IsTrue(made);
        Received.InOrder(() =>
        {
            _workshop.TakeInputs();
            _workshop.GiveTools();
        });
        _workshop.Received(1).SetStamina(44);
        _workshop.Received(1).AddCraftingXp(Arg.Is<float>(x => Math.Abs(x - 49.5f) < 0.0001f));
    }

    [TestMethod]
    public void TryMake_WithAFlooredCost_SpendsOne()
    {
        _workshop.RefiningCost.Returns(0);

        _service.TryMake(_workshop);

        _workshop.Received(1).SetStamina(49);
    }

    [TestMethod]
    public void TryMake_WithZeroExperience_StillMakesTheTools_AndGrantsNoExperience()
    {
        _workshop.ToolsValue.Returns(10);

        var made = _service.TryMake(_workshop);

        Assert.IsTrue(made);
        _workshop.DidNotReceive().AddCraftingXp(Arg.Any<float>());
        _workshop.Received(1).GiveTools();
    }

    [DataTestMethod]
    [DataRow(0, 1, 50)]
    [DataRow(1, 0, 50)]
    [DataRow(1, 1, 3)]
    public void TryMake_WhenNotReady_ChangesNothing(int iron, int wood, int stamina)
    {
        _workshop.IronCount.Returns(iron);
        _workshop.WoodCount.Returns(wood);
        _workshop.Stamina.Returns(stamina);

        var made = _service.TryMake(_workshop);

        Assert.IsFalse(made);
        _workshop.DidNotReceive().TakeInputs();
        _workshop.DidNotReceive().GiveTools();
        _workshop.DidNotReceive().SetStamina(Arg.Any<int>());
        _workshop.DidNotReceive().AddCraftingXp(Arg.Any<float>());
    }

    [TestMethod]
    public void TryMake_WhenSwitchedOff_ChangesNothing()
    {
        _settings.MakeToolsEnabled.Returns(false);

        Assert.IsFalse(_service.TryMake(_workshop));
        _workshop.DidNotReceive().TakeInputs();
    }
}
