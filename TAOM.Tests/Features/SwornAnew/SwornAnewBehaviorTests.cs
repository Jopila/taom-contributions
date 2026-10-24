using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.SwornAnew;
using TAOM.Features.SwornAnew.Hooks;
using TAOM.Features.UncapturableHeroes;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SwornAnew;

/// <summary>
/// The behavior asks the adapter to commit the party screen left open under a conversation (the HardQuestions fix, adapters.md
/// "party-screen writes") once the conversation ends: the engine's own release of the lord for the new kingdom runs in another
/// listener of the same event, so the commit is queued on the conversation's end, not made inside the listener. Only a defection of
/// the partner's own clan into the player's kingdom queues it. A fault never escapes into the engine's event invoke, which has no
/// try of its own. Building a CampaignBehaviorBase runs engine code, hence the category.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SwornAnewBehaviorTests
{
    private ISwornAnewSettingsProvider _settings = null!;
    private ISwornAnewAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private SwornAnewBehavior _behavior = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ISwornAnewSettingsProvider>();
        _settings.Enabled.Returns(true);
        _adapter = Substitute.For<ISwornAnewAdapter>();
        _logger = Substitute.For<IModLogger>();
        var service = new SwornAnewService(_settings, Substitute.For<IAlignmentService>(), Substitute.For<IUncapturableRegistry>(),
            Substitute.For<ICoopSessionProvider>(), Substitute.For<IDedicatedServerProvider>());
        _behavior = new SwornAnewBehavior(service, _adapter, _logger);
    }

    [TestMethod]
    public void Handle_ADefectionOfThePartnersClan_QueuesTheCommitOnce()
    {
        _adapter.IsConversationPartnerJoiningPlayersKingdom(null, null).Returns(true);

        _behavior.Handle(true, null, null);

        _adapter.Received(1).CommitOpenPartyScreenWhenConversationEnds();
    }

    [TestMethod]
    public void Handle_NotADefection_NeverAsksTheAdapter_AndQueuesNothing()
    {
        _adapter.IsConversationPartnerJoiningPlayersKingdom(null, null).Returns(true);

        _behavior.Handle(false, null, null);

        _adapter.DidNotReceiveWithAnyArgs().IsConversationPartnerJoiningPlayersKingdom(default, default);
        _adapter.DidNotReceive().CommitOpenPartyScreenWhenConversationEnds();
    }

    [TestMethod]
    public void Handle_AClanThatIsNotThePartnersOrNotTheKingdom_QueuesNothing()
    {
        _adapter.IsConversationPartnerJoiningPlayersKingdom(null, null).Returns(false);

        _behavior.Handle(true, null, null);

        _adapter.DidNotReceive().CommitOpenPartyScreenWhenConversationEnds();
    }

    [TestMethod]
    public void Handle_TheSwitchOff_QueuesNothing()
    {
        _settings.Enabled.Returns(false);
        _adapter.IsConversationPartnerJoiningPlayersKingdom(null, null).Returns(true);

        _behavior.Handle(true, null, null);

        _adapter.DidNotReceive().CommitOpenPartyScreenWhenConversationEnds();
    }

    [TestMethod]
    public void Handle_AFaultInTheAdapterRead_IsLoggedAndNeverEscapes()
    {
        _adapter.IsConversationPartnerJoiningPlayersKingdom(null, null).Returns(_ => throw new InvalidOperationException("boom"));

        _behavior.Handle(true, null, null);

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[SwornAnew]") && s.Contains("boom")));
    }

    [TestMethod]
    public void Handle_AFaultWhileQueuingTheCommit_IsLoggedAndNeverEscapes()
    {
        _adapter.IsConversationPartnerJoiningPlayersKingdom(null, null).Returns(true);
        _adapter.When(a => a.CommitOpenPartyScreenWhenConversationEnds()).Do(_ => throw new InvalidOperationException("boom"));

        _behavior.Handle(true, null, null);

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[SwornAnew]") && s.Contains("boom")));
    }

    [TestMethod]
    public void OnClanChangedKingdom_AJoinByDefection_IsTheOnlyDetailThatReachesTheAdapter()
    {
        _adapter.IsConversationPartnerJoiningPlayersKingdom(null, null).Returns(true);

        _behavior.OnClanChangedKingdom(null, null, null, ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdomByDefection, false);

        _adapter.Received(1).IsConversationPartnerJoiningPlayersKingdom(null, null);
        _adapter.Received(1).CommitOpenPartyScreenWhenConversationEnds();
    }

    [TestMethod]
    public void OnClanChangedKingdom_EveryOtherDetail_AsksTheAdapterNothing()
    {
        _adapter.IsConversationPartnerJoiningPlayersKingdom(null, null).Returns(true);

        foreach (var detail in Enum.GetValues(typeof(ChangeKingdomAction.ChangeKingdomActionDetail))
                     .Cast<ChangeKingdomAction.ChangeKingdomActionDetail>()
                     .Where(d => d != ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdomByDefection))
            _behavior.OnClanChangedKingdom(null, null, null, detail, false);

        _adapter.DidNotReceiveWithAnyArgs().IsConversationPartnerJoiningPlayersKingdom(default, default);
        _adapter.DidNotReceive().CommitOpenPartyScreenWhenConversationEnds();
    }

    [TestMethod]
    public void TheListener_IsRegisteredOnTheOneEvent()
    {
        var src = RepoPaths.ReadSource("Main/Features/SwornAnew/Hooks/SwornAnewBehavior.cs", stripComments: true);

        StringAssert.Contains(src, "CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom)");
        Assert.AreEqual(1, Regex.Matches(src, @"AddNonSerializedListener\(").Count, "one event only");
        Assert.IsFalse(src.Contains("ConversationEnded"), "the commit is queued on the conversation manager, not listened for");
    }

    [TestMethod]
    public void TheBehavior_CallsNoKingdomAction_AndSavesNothing()
    {
        // enlistment.md: never call a kingdom action inside OnClanChangedKingdomEvent. This behavior only observes.
        var src = RepoPaths.ReadSource("Main/Features/SwornAnew/Hooks/SwornAnewBehavior.cs", stripComments: true);

        Assert.IsFalse(Regex.IsMatch(src, @"ChangeKingdomAction\.Apply"));
        Assert.IsFalse(Regex.IsMatch(src, @"dataStore\.SyncData"));
    }
}
