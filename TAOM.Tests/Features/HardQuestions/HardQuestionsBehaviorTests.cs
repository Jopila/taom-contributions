using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.HardQuestions;
using TAOM.Features.HardQuestions.Domain;
using TAOM.Features.HardQuestions.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HardQuestions;

/// <summary>
/// The state and the queue are process singletons, so a second campaign in one process, or a save made before
/// the feature existed, must start empty, and a real save must restore exactly what it held
/// (csharp-architecture.md, "Singleton Services Holding Per-Campaign State").
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class HardQuestionsBehaviorTests
{
    private HardQuestionsFixture _f = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new HardQuestionsFixture();
    }

    private HardQuestionsCampaignBehavior NewCampaignBehavior() =>
        new(_f.State, _f.Strip, _f.Coop, _f.Logger);

    [TestMethod]
    public void CampaignBehavior_Constructor_StartsFromAnEmptyState()
    {
        _f.State.MarkStripped("old_campaign_lord");
        _f.State.MarkAsked(HardQuestionTopic.Clan, "old_campaign_lord");

        NewCampaignBehavior();

        Assert.IsFalse(_f.State.IsStripped("old_campaign_lord"));
        Assert.IsFalse(_f.State.IsAsked(HardQuestionTopic.Clan, "old_campaign_lord"));
    }

    [TestMethod]
    public void SyncData_LoadingASaveWithoutTheKey_YieldsAnEmptyState()
    {
        var behavior = NewCampaignBehavior();
        _f.State.MarkStripped("leftover");

        behavior.SyncData(new EngineDataStore { Loading = true });

        Assert.IsFalse(_f.State.IsStripped("leftover"), "a key miss must not keep the previous state");
    }

    [TestMethod]
    public void SyncData_SaveThenLoadIntoAnotherCampaign_RestoresTheSavedState()
    {
        var first = NewCampaignBehavior();
        _f.State.MarkAsked(HardQuestionTopic.Kingdom, "lord1");
        _f.State.MarkStripped("lord2");
        var store = new EngineDataStore();
        first.SyncData(store);

        var second = NewCampaignBehavior();
        _f.State.MarkStripped("other_campaign");
        store.Loading = true;
        second.SyncData(store);

        Assert.IsTrue(_f.State.IsAsked(HardQuestionTopic.Kingdom, "lord1"));
        Assert.IsTrue(_f.State.IsStripped("lord2"));
        Assert.IsFalse(_f.State.IsStripped("other_campaign"));
    }

    [TestMethod]
    public void SyncData_DamagedEntries_AreDroppedWithAWarning()
    {
        var behavior = NewCampaignBehavior();
        var store = new EngineDataStore();
        var broken = new Dictionary<string, string> { ["v"] = "1", ["mystery|x"] = "1", ["used|ok"] = "4" };
        store.SyncData("_taom_hardQuestions", ref broken);
        store.Loading = true;

        behavior.SyncData(store);

        Assert.IsTrue(_f.State.IsStripped("ok"));
        _f.Logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("1 damaged")));
    }

    [TestMethod]
    public void DialogBehavior_Constructor_DropsAQueuedDeedFromTheLastCampaign()
    {
        _f.Captive();
        _f.Service.QueueStrip("lord1");

        _ = new HardQuestionsDialogBehavior(_f.Service, _f.Captives);
        _f.Service.CompletePending();

        _f.Actions.DidNotReceiveWithAnyArgs().LoseRenown(default);
    }

    [TestMethod]
    public void DialogBehavior_Conditions_DelegateToTheService()
    {
        _f.Captive(ransom: 1000);
        _f.Captives.ConversationPartnerId.Returns("lord1");
        var behavior = new HardQuestionsDialogBehavior(_f.Service, _f.Captives);

        Assert.IsTrue(behavior.CanAsk(HardQuestionTopic.Clan));
        Assert.IsTrue(behavior.CanStrip());
        Assert.IsTrue(behavior.RepliesAs(BribeReplyKind.Plain));
        behavior.Choose(HardQuestionTopic.Clan);
        Assert.IsTrue(behavior.PriceIsPayable(out var explanation));
        Assert.IsNull(explanation);

        _f.Player.Gold.Returns(1);
        Assert.IsFalse(behavior.PriceIsPayable(out explanation));
        Assert.IsNotNull(explanation);
    }

    [TestMethod]
    public void DialogBehavior_Consequences_DelegateToTheService()
    {
        _f.Captive();
        _f.Captives.ConversationPartnerId.Returns("lord1");
        var behavior = new HardQuestionsDialogBehavior(_f.Service, _f.Captives);
        behavior.Choose(HardQuestionTopic.Kingdom);

        behavior.PayBribe();
        _f.Actions.Received().PayGold("lord1", 3500);

        behavior.Strip();
        Assert.IsTrue(_f.State.IsStripped("lord1"));

        _f.State.ClearHero("lord1");
        behavior.Force();
        Assert.IsTrue(_f.State.IsAsked(HardQuestionTopic.Kingdom, "lord1"));
    }

    [TestMethod]
    public void CampaignBehavior_ReleaseOfAStrippedLord_GivesAFreshKit()
    {
        var behavior = NewCampaignBehavior();
        _f.State.MarkStripped("lord1");
        _f.Captives.IsAlive("lord1").Returns(true);
        _f.Equipment.GiveFreshKit("lord1", "torn_bandit_clothes").Returns(true);

        behavior.ReleaseHero("lord1");

        _f.Equipment.Received().GiveFreshKit("lord1", "torn_bandit_clothes");
    }

    [TestMethod]
    public void CampaignBehavior_ReleaseOnACoopClient_DefersToTheHost()
    {
        var behavior = NewCampaignBehavior();
        _f.State.MarkStripped("lord1");
        _f.Coop.ShouldDeferToHost.Returns(true);

        behavior.ReleaseHero("lord1");

        _f.Equipment.DidNotReceiveWithAnyArgs().GiveFreshKit(default!, default!);
        Assert.IsTrue(_f.State.IsStripped("lord1"));
    }

    [TestMethod]
    public void CampaignBehavior_ADeathInCaptivity_ClearsTheLordsRecord()
    {
        var behavior = NewCampaignBehavior();
        _f.State.MarkStripped("lord1");
        _f.State.MarkAsked(HardQuestionTopic.Clan, "lord1");

        behavior.HeroDied("lord1");

        Assert.IsFalse(_f.State.IsStripped("lord1"));
        Assert.IsFalse(_f.State.IsAsked(HardQuestionTopic.Clan, "lord1"));
        _f.Captives.DidNotReceiveWithAnyArgs().IsAlive(default!);
    }

    [TestMethod]
    public void CampaignBehavior_ADeathOnACoopClient_DefersToTheHost()
    {
        var behavior = NewCampaignBehavior();
        _f.State.MarkStripped("lord1");
        _f.Coop.ShouldDeferToHost.Returns(true);

        behavior.HeroDied("lord1");

        Assert.IsTrue(_f.State.IsStripped("lord1"));
    }
}
