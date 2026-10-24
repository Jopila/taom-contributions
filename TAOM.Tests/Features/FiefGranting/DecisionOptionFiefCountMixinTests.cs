using System;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FiefGranting.UI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;

namespace TAOM.Tests.Features.FiefGranting;

/// <summary>
/// #866: when the fief-count row shows and what it says, run on bare engine objects (uninitialized, members set by
/// reflection, the staging <see cref="FiefGrantFactsBuilderNamesTests"/> uses) so no campaign is needed.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class DecisionOptionFiefCountMixinTests
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static void Set(object target, string member, object? value)
    {
        var field = target.GetType().GetField(member, Any) ?? target.GetType().GetField("<" + member + ">k__BackingField", Any);
        Assert.IsNotNull(field, target.GetType().Name + "." + member + " is gone; the staging needs a new member name.");
        field!.SetValue(target, value);
    }

    private static SettlementClaimantDecision.ClanAsDecisionOutcome Outcome(Clan clan)
    {
        var outcome = Bare<SettlementClaimantDecision.ClanAsDecisionOutcome>();
        Set(outcome, "Clan", clan);
        return outcome;
    }

    private static SettlementClaimantDecision Claimant(Settlement contested)
    {
        var decision = Bare<SettlementClaimantDecision>();
        Set(decision, "Settlement", contested);
        return decision;
    }

    // The mixin holds its view model only through a WeakReference, so the test keeps the strong reference: a collection
    // between construction and OnRefresh would otherwise null it and let a test pass or fail by chance.
    private DecisionOptionVM? _vm;

    private DecisionOptionFiefCountMixin Mixin(DecisionOutcome? option, KingdomDecision? decision)
    {
        _vm = Bare<DecisionOptionVM>();
        Set(_vm, "Option", option);
        Set(_vm, "Decision", decision);
        return new DecisionOptionFiefCountMixin(_vm);
    }

    [TestMethod]
    public void AClaimantBallot_ShowsTheCountAndTheSortedNames()
    {
        var contested = FiefGrantFactsBuilderNamesTests.Town("Pelargir");
        var clan = FiefGrantFactsBuilderNamesTests.ClanHolding(
            FiefGrantFactsBuilderNamesTests.Town("minas Tirith"), FiefGrantFactsBuilderNamesTests.Village("Edge"), FiefGrantFactsBuilderNamesTests.Castle("Aldburg"));
        var mixin = Mixin(Outcome(clan), Claimant(contested));

        mixin.OnRefresh();

        Assert.IsTrue(mixin.ShowFiefCount);
        Assert.AreEqual("Current fiefs: 2", mixin.FiefCountText);
        Assert.AreEqual("Fiefs held\nAldburg\nminas Tirith", mixin.FiefCountHint.HintText.ToString());
    }

    [TestMethod]
    public void AClanWithNoFortification_ShowsZeroAndTheNoneLine()
    {
        var mixin = Mixin(Outcome(FiefGrantFactsBuilderNamesTests.ClanHolding()), Claimant(FiefGrantFactsBuilderNamesTests.Town("Pelargir")));

        mixin.OnRefresh();

        Assert.IsTrue(mixin.ShowFiefCount);
        Assert.AreEqual("Current fiefs: 0", mixin.FiefCountText);
        Assert.AreEqual("Fiefs held\nHolds no town or castle.", mixin.FiefCountHint.HintText.ToString());
    }

    [TestMethod]
    public void TheClanThatOwnsTheFiefUnderVote_GetsTheNotCountedLine()
    {
        var contested = FiefGrantFactsBuilderNamesTests.Town("Pelargir");
        var clan = FiefGrantFactsBuilderNamesTests.ClanHolding(FiefGrantFactsBuilderNamesTests.Town("Aldburg"), contested);
        Set(contested.Town, "_ownerClan", clan);
        var mixin = Mixin(Outcome(clan), Claimant(contested));

        mixin.OnRefresh();

        Assert.AreEqual("Current fiefs: 1", mixin.FiefCountText);
        Assert.AreEqual("Fiefs held\nAldburg\nNot counting Pelargir, the fief under vote", mixin.FiefCountHint.HintText.ToString());
    }

    [TestMethod]
    public void AnotherClan_GetsNoNotCountedLine()
    {
        var contested = FiefGrantFactsBuilderNamesTests.Town("Pelargir");
        Set(contested.Town, "_ownerClan", FiefGrantFactsBuilderNamesTests.ClanHolding(contested));
        var mixin = Mixin(Outcome(FiefGrantFactsBuilderNamesTests.ClanHolding(FiefGrantFactsBuilderNamesTests.Town("Aldburg"))), Claimant(contested));

        mixin.OnRefresh();

        Assert.IsTrue(mixin.ShowFiefCount);
        Assert.IsFalse(mixin.FiefCountHint.HintText.ToString().Contains("Not counting"));
    }

    [TestMethod]
    public void TheAbstainCard_HidesTheRow()
    {
        var mixin = Mixin(null, Claimant(FiefGrantFactsBuilderNamesTests.Town("Pelargir")));
        mixin.ShowFiefCount = true;

        mixin.OnRefresh();

        Assert.IsFalse(mixin.ShowFiefCount);
    }

    [TestMethod]
    public void ANonClaimantDecision_HidesTheRow_EvenForAClanOutcome()
    {
        var mixin = Mixin(Outcome(FiefGrantFactsBuilderNamesTests.ClanHolding()), Bare<DeclareWarDecision>());
        mixin.ShowFiefCount = true;

        mixin.OnRefresh();

        Assert.IsFalse(mixin.ShowFiefCount);
    }

    [TestMethod]
    public void AFault_HidesTheRow_AndDoesNotThrow()
    {
        var decision = Claimant(FiefGrantFactsBuilderNamesTests.Town("Pelargir"));
        var mixin = Mixin(Outcome(FiefGrantFactsBuilderNamesTests.ClanHolding(FiefGrantFactsBuilderNamesTests.Town("Aldburg"))), decision);
        mixin.OnRefresh();
        Assert.IsTrue(mixin.ShowFiefCount, "the row shows before the fault");

        Set(decision, "Settlement", null);
        mixin.OnRefresh();

        Assert.IsFalse(mixin.ShowFiefCount);
    }
}
