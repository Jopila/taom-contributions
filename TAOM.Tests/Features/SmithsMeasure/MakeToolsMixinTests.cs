using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SmithsMeasure;
using TAOM.Features.SmithsMeasure.UI;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SmithsMeasure;

/// <summary>
/// #910: when the Make Tools card shows, when it is enabled, and what a press does, run on bare engine objects (uninitialized,
/// members set by reflection) so no campaign is needed. The hero reaches the workshop through a factory the test replaces,
/// because a real adapter reads the campaign, and the switch back to the refinement tab is a seam because the real call
/// runs UpdateAll, which needs a live screen.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MakeToolsMixinTests
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static void Set(object target, string member, object? value)
    {
        var field = target.GetType().GetField(member, Any) ?? target.GetType().GetField("<" + member + ">k__BackingField", Any);
        Assert.IsNotNull(field, target.GetType().Name + "." + member + " is gone; the staging needs a new member name.");
        field!.SetValue(target, value);
    }

    private CraftingVM _vm = null!;
    private IModLogger _logger = null!;
    private ISmithsMeasureSettingsProvider _settings = null!;
    private ICraftingWorkshopAdapter _workshop = null!;
    private ICraftingWorkshopAdapter? _workshopToReturn;
    private int _switches;

    // The mixin holds its view model only through a WeakReference, so the test keeps the strong reference.
    private MakeToolsMixin Mixin(bool refinementTab = true)
    {
        _vm = Bare<CraftingVM>();
        Set(_vm, "_isInRefinementMode", refinementTab);
        var hero = Bare<CraftingAvailableHeroItemVM>();
        Set(hero, "Hero", Bare<Hero>());
        Set(_vm, "_currentCraftingHero", hero);

        _logger = Substitute.For<IModLogger>();
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
        _workshopToReturn = _workshop;
        return new MakeToolsMixin(_vm, new MakeToolsService(_settings), _ => _workshopToReturn, vm => _switches++, _logger);
    }

    [TestMethod]
    public void OnTheRefinementTab_WithEverythingNeeded_TheCardShowsAndIsEnabled()
    {
        var mixin = Mixin();

        mixin.OnRefresh();

        Assert.IsTrue(mixin.IsMakeToolsVisible);
        Assert.IsTrue(mixin.IsMakeToolsEnabled);
        Assert.AreEqual("Make Tools", mixin.MakeToolsText);
        Assert.IsNotNull(mixin.MakeToolsHint);
    }

    [TestMethod]
    public void TheHint_IsOneViewModelForTheCardsLife_AndReadsTheLatestQuoteOnHover()
    {
        var mixin = Mixin();
        mixin.OnRefresh();
        var hint = mixin.MakeToolsHint;
        var text = (Func<string>)typeof(TaleWorlds.Core.ViewModelCollection.Information.BasicTooltipViewModel)
            .GetField("_hintProperty", Any)!.GetValue(hint)!;
        StringAssert.Contains(text(), "It costs 6 smithy stamina");
        Assert.IsFalse(text().Contains("You need"));

        _workshop.WoodCount.Returns(0);
        mixin.OnRefresh();

        Assert.AreSame(hint, mixin.MakeToolsHint, "a replaced child view model would need a DataSource rebind");
        StringAssert.Contains(text(), "You need one Hardwood.");
        StringAssert.Contains(text(), "It costs 6 smithy stamina");
    }

    [TestMethod]
    public void OnAnotherTab_TheCardIsHidden_AndTheWorkshopIsNotRead()
    {
        var mixin = Mixin(refinementTab: false);
        _workshop.ClearReceivedCalls();

        mixin.OnRefresh();

        Assert.IsFalse(mixin.IsMakeToolsVisible);
        Assert.IsFalse(mixin.IsMakeToolsEnabled);
        Assert.AreEqual(0, _workshop.ReceivedCalls().Count());
    }

    [TestMethod]
    public void WithNoWorkshopForTheHero_TheCardIsHidden()
    {
        var mixin = Mixin();
        _workshopToReturn = null;

        mixin.OnRefresh();

        Assert.IsFalse(mixin.IsMakeToolsVisible);
    }

    [TestMethod]
    public void WhenSwitchedOff_TheCardIsHidden()
    {
        var mixin = Mixin();
        _settings.MakeToolsEnabled.Returns(false);

        mixin.OnRefresh();

        Assert.IsFalse(mixin.IsMakeToolsVisible);
    }

    [DataTestMethod]
    [DataRow(0, 1, 50)]
    [DataRow(1, 0, 50)]
    [DataRow(1, 1, 2)]
    public void WithSomethingMissing_TheCardShowsButIsDisabled(int iron, int wood, int stamina)
    {
        var mixin = Mixin();
        _workshop.IronCount.Returns(iron);
        _workshop.WoodCount.Returns(wood);
        _workshop.Stamina.Returns(stamina);

        mixin.OnRefresh();

        Assert.IsTrue(mixin.IsMakeToolsVisible);
        Assert.IsFalse(mixin.IsMakeToolsEnabled);
    }

    [TestMethod]
    public void TheCard_FollowsTheTab_OnTheNextRefresh()
    {
        var mixin = Mixin();
        mixin.OnRefresh();
        Assert.IsTrue(mixin.IsMakeToolsVisible);

        Set(_vm, "_isInRefinementMode", false);
        mixin.OnRefresh();

        Assert.IsFalse(mixin.IsMakeToolsVisible);
    }

    [TestMethod]
    public void APress_WhenReady_MakesTheTools_AndRefreshesTheScreen()
    {
        var mixin = Mixin();

        mixin.ExecuteMakeTools();

        _workshop.Received(1).TakeInputs();
        _workshop.Received(1).GiveTools();
        Assert.AreEqual(1, _switches, "the screen is rebuilt through the view model's own tab switch, which refreshes the counts");
    }

    [TestMethod]
    public void APress_WhenNotReady_ChangesNothing_AndRefreshesTheCard()
    {
        var mixin = Mixin();
        _workshop.IronCount.Returns(0);

        mixin.ExecuteMakeTools();

        _workshop.DidNotReceive().TakeInputs();
        Assert.AreEqual(0, _switches);
        Assert.IsTrue(mixin.IsMakeToolsVisible);
        Assert.IsFalse(mixin.IsMakeToolsEnabled);
    }

    [TestMethod]
    public void APress_OnAnotherTab_DoesNothing()
    {
        var mixin = Mixin(refinementTab: false);

        mixin.ExecuteMakeTools();

        _workshop.DidNotReceive().TakeInputs();
        Assert.AreEqual(0, _switches);
    }

    [TestMethod]
    public void APress_WithNoWorkshop_DoesNothing()
    {
        var mixin = Mixin();
        _workshopToReturn = null;

        mixin.ExecuteMakeTools();

        Assert.AreEqual(0, _switches);
    }

    [TestMethod]
    public void AFaultReadingTheWorkshop_HidesTheCard_LogsOnce_AndNeverEscapes()
    {
        var mixin = Mixin();
        mixin.OnRefresh();
        Assert.IsTrue(mixin.IsMakeToolsVisible);
        var broken = true;
        _workshop.IronCount.Returns(_ => broken ? throw new InvalidOperationException("no roster") : 1);

        mixin.OnRefresh();
        mixin.OnRefresh();

        Assert.IsFalse(mixin.IsMakeToolsVisible);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[SmithsMeasure]") && s.Contains("no roster")));

        broken = false;
        mixin.OnRefresh();
        Assert.IsTrue(mixin.IsMakeToolsVisible, "later refreshes still run once the fault is gone");
    }

    [TestMethod]
    public void AFaultInAPress_DoesNotEscape_AndLogsOnce()
    {
        var mixin = Mixin();
        _workshop.When(w => w.TakeInputs()).Do(_ => throw new InvalidOperationException("roster locked"));

        mixin.ExecuteMakeTools();

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[SmithsMeasure]") && s.Contains("roster locked")));
        Assert.AreEqual(0, _switches);
    }

    [TestMethod]
    public void TheMixin_IsAttachedToTheCraftingViewModel_ThroughRefreshEnableMainAction()
    {
        var src = RepoPaths.ReadSource("Client/Features/SmithsMeasure/UI/MakeToolsMixin.cs", stripComments: true);

        StringAssert.Contains(src, "[ViewModelMixin(\"RefreshEnableMainAction\")]");
        StringAssert.Contains(src, "BaseViewModelMixin<CraftingVM>");
        StringAssert.Contains(src, "vm => vm.ExecuteSwitchToRefinement()");
    }

    [TestMethod]
    public void TheMixin_IsSuppressedUnderCoop_BecauseThePressWritesTheStoresFromThisClient()
    {
        Assert.IsTrue(Attribute.IsDefined(typeof(MakeToolsMixin), typeof(TAOM.Features.CoopInterop.CoopSuppressedUiAttribute)));
    }
}
