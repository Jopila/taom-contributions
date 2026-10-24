using System;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Clans;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.BondOfHire.UI;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BondOfHire;

/// <summary>
/// #868: when the contract line shows and what it says, run on bare engine objects (uninitialized, members set by reflection)
/// so no campaign is needed. The clan reaches the rules through a factory the test replaces, because a real adapter reads the
/// campaign clock.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MercenaryTermMixinTests
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const string Days30 = "These mercenaries agreed to serve for at least 30 more days";

    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static void Set(object target, string member, object? value)
    {
        var field = target.GetType().GetField(member, Any) ?? target.GetType().GetField("<" + member + ">k__BackingField", Any);
        Assert.IsNotNull(field, target.GetType().Name + "." + member + " is gone; the staging needs a new member name.");
        field!.SetValue(target, value);
    }

    private static KingdomClanItemVM Item(bool mercenary)
    {
        var clan = Bare<Clan>();
        Set(clan, "IsUnderMercenaryService", mercenary);
        var item = Bare<KingdomClanItemVM>();
        Set(item, "Clan", clan);
        return item;
    }

    private KingdomClanVM _vm = null!;
    private IModLogger _logger = null!;
    private IMercenaryClanAdapter _adapter = null!;
    private int _adapterReads;

    // The mixin holds its view model only through a WeakReference, so the test keeps the strong reference.
    private MercenaryTermMixin Mixin()
    {
        _vm = Bare<KingdomClanVM>();
        _logger = Substitute.For<IModLogger>();
        _adapter = Substitute.For<IMercenaryClanAdapter>();
        _adapter.RemainingStayDays.Returns(30f);
        return new MercenaryTermMixin(_vm, clan =>
        {
            _adapterReads++;
            return _adapter;
        }, _logger);
    }

    [TestMethod]
    public void ASelectedMercenaryClan_ShowsTheDaysLeft()
    {
        var mixin = Mixin();

        _vm.CurrentSelectedClan = Item(mercenary: true);

        Assert.IsTrue(mixin.IsMercenaryTermVisible);
        Assert.AreEqual(Days30, mixin.MercenaryTermText);
    }

    [TestMethod]
    public void OnRefresh_ForTheClanAlreadySelected_ShowsTheLine_AsTheConstructorsFirstSelectionHasNoEvent()
    {
        var mixin = Mixin();
        Set(_vm, "_currentSelectedClan", Item(mercenary: true));

        mixin.OnRefresh();

        Assert.IsTrue(mixin.IsMercenaryTermVisible);
        Assert.AreEqual(Days30, mixin.MercenaryTermText);
    }

    [TestMethod]
    public void AClanThatIsNotAMercenary_HidesTheLine()
    {
        var mixin = Mixin();
        _vm.CurrentSelectedClan = Item(mercenary: true);

        _vm.CurrentSelectedClan = Item(mercenary: false);

        Assert.IsFalse(mixin.IsMercenaryTermVisible);
        Assert.AreEqual("", mixin.MercenaryTermText);
    }

    [TestMethod]
    public void ThePlayersClan_HidesTheLine_EvenAsAMercenary()
    {
        var mixin = Mixin();
        _adapter.IsPlayerClan.Returns(true);

        _vm.CurrentSelectedClan = Item(mercenary: true);

        Assert.IsFalse(mixin.IsMercenaryTermVisible);
    }

    [TestMethod]
    public void UnderOneDayLeft_SaysItIsNotKnown()
    {
        var mixin = Mixin();
        _adapter.RemainingStayDays.Returns(0.4f);

        _vm.CurrentSelectedClan = Item(mercenary: true);

        Assert.IsTrue(mixin.IsMercenaryTermVisible);
        Assert.AreEqual("It is not known how long these mercenaries will serve", mixin.MercenaryTermText);
    }

    [TestMethod]
    public void ADatePastOrNeverSet_SaysItIsNotKnown()
    {
        var mixin = Mixin();
        _adapter.RemainingStayDays.Returns(-9000f);

        _vm.CurrentSelectedClan = Item(mercenary: true);

        Assert.AreEqual("It is not known how long these mercenaries will serve", mixin.MercenaryTermText);
    }

    [TestMethod]
    public void NoClanSelected_HidesTheLine()
    {
        var mixin = Mixin();
        _vm.CurrentSelectedClan = Item(mercenary: true);

        _vm.CurrentSelectedClan = null!;

        Assert.IsFalse(mixin.IsMercenaryTermVisible);
    }

    [TestMethod]
    public void AnotherPropertyChanging_DoesNotRecompute()
    {
        var mixin = Mixin();
        _vm.CurrentSelectedClan = Item(mercenary: true);
        var reads = _adapterReads;

        _vm.BannerText = "changed";
        _vm.ExpelActionText = "x";

        Assert.AreEqual(reads, _adapterReads, "only CurrentSelectedClan moves the line");
        Assert.IsTrue(mixin.IsMercenaryTermVisible);
    }

    [TestMethod]
    public void OnFinalize_Unsubscribes_SoALaterSelectionChangesNothing()
    {
        var mixin = Mixin();
        _vm.CurrentSelectedClan = Item(mercenary: true);

        mixin.OnFinalize();
        _vm.CurrentSelectedClan = Item(mercenary: false);

        Assert.IsTrue(mixin.IsMercenaryTermVisible, "the finalized mixin no longer follows the selection");
    }

    [TestMethod]
    public void AFaultReadingTheClan_HidesTheLine_LogsOnce_AndNeverEscapes()
    {
        var mixin = Mixin();
        _vm.CurrentSelectedClan = Item(mercenary: true);
        Assert.IsTrue(mixin.IsMercenaryTermVisible);
        var broken = true;
        _adapter.RemainingStayDays.Returns(_ => broken ? throw new InvalidOperationException("no clock") : 30f);

        _vm.CurrentSelectedClan = Item(mercenary: true);
        mixin.OnRefresh();

        Assert.IsFalse(mixin.IsMercenaryTermVisible);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[BondOfHire]") && s.Contains("no clock")));

        // Later selections still run once the fault is gone.
        broken = false;
        _vm.CurrentSelectedClan = Item(mercenary: true);
        Assert.IsTrue(mixin.IsMercenaryTermVisible);
        Assert.AreEqual(Days30, mixin.MercenaryTermText);
    }

    [TestMethod]
    public void TheMixin_IsAttachedToTheClansPanelsViewModel_ThroughRefreshValues()
    {
        var src = RepoPaths.ReadSource("Client/Features/BondOfHire/UI/MercenaryTermMixin.cs", stripComments: true);

        StringAssert.Contains(src, "[ViewModelMixin(\"RefreshValues\")]");
        StringAssert.Contains(src, "BaseViewModelMixin<KingdomClanVM>");
    }
}
