using System;
using System.Collections.Generic;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.Messengers;
using TAOM.Features.NotableServices;
using TAOM.Features.NotableServices.Domain;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.NotableServices;

/// <summary>
/// The service graph over substitutes: a notable headman of a Free village the player stands in, at peace, an unmet
/// colleague or two, a purse of 1000 and a single-player session.
/// </summary>
public sealed class NotableServicesFixture
{
    public const string SpeakerId = "notable1";
    public const string SettlementId = "village_1";

    public NotableServicesConfig Config { get; } = new();
    public INotableServicesConfigProvider ConfigProvider { get; } = Substitute.For<INotableServicesConfigProvider>();
    public INotableServicesSettingsProvider Settings { get; } = Substitute.For<INotableServicesSettingsProvider>();
    public INotableServicesAdapter Adapter { get; } = Substitute.For<INotableServicesAdapter>();
    public IMapRevealAdapter Reveal { get; } = Substitute.For<IMapRevealAdapter>();
    public IAlignmentService Alignment { get; } = Substitute.For<IAlignmentService>();
    public IRelationGrantService Grants { get; } = Substitute.For<IRelationGrantService>();
    public IMessengerConversationState Messenger { get; } = Substitute.For<IMessengerConversationState>();
    public ICoopSessionProvider Coop { get; } = Substitute.For<ICoopSessionProvider>();
    public IDedicatedServerProvider Server { get; } = Substitute.For<IDedicatedServerProvider>();
    public IModLogger Logger { get; } = Substitute.For<IModLogger>();
    public NotableSnapshot Speaker { get; }
    public NotableServicesService Service { get; }

    /// <summary>The action handed to <c>RunAfterConversation</c>, or null.</summary>
    public Action? Queued { get; private set; }

    /// <summary>The settlement the gift screen was opened for, or null.</summary>
    public string? ScreenSettlement { get; private set; }

    /// <summary>The room the screen shows for the gift side, or 0 before a screen opened.</summary>
    public int MenLimit { get; private set; }

    public Func<GiftTransfer, bool>? CanTransfer { get; private set; }

    public Func<GiftTally, bool>? CanFinish { get; private set; }

    public Action<GiftTally>? OnDone { get; private set; }

    public NotableServicesFixture()
    {
        ConfigProvider.GetConfig().Returns(Config);
        Settings.Enabled.Returns(true);
        Coop.IsAuthority.Returns(true);
        Speaker = new NotableSnapshot(SpeakerId)
        {
            IsNotable = true,
            InPlayersSettlement = true,
            SettlementId = SettlementId,
            Kind = SettlementKind.Village,
            SettlementSide = new SideIds("k_free", "c_free"),
            Relation = 0f,
        };
        Adapter.ConversationPartnerId.Returns(SpeakerId);
        Adapter.Snapshot(SpeakerId).Returns(Speaker);
        Adapter.PlayerGold.Returns(1000);
        Adapter.PayNotable(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        Adapter.UnmetNotables(SettlementId).Returns(new List<string> { "notable2", "notable3" });
        Adapter.PlayerHasMenToGive().Returns(true);
        Adapter.MapDiagonal.Returns(1000f);
        Adapter.When(a => a.RunAfterConversation(Arg.Any<Action>())).Do(c => Queued = c.Arg<Action>());
        Adapter.When(a => a.OpenGiftScreen(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<Func<GiftTransfer, bool>>(), Arg.Any<Func<GiftTally, bool>>(), Arg.Any<Action<GiftTally>>()))
            .Do(c =>
            {
                ScreenSettlement = c.ArgAt<string>(0);
                MenLimit = c.ArgAt<int>(1);
                CanTransfer = c.ArgAt<Func<GiftTransfer, bool>>(2);
                CanFinish = c.ArgAt<Func<GiftTally, bool>>(3);
                OnDone = c.ArgAt<Action<GiftTally>>(4);
            });
        Alignment.ResolveSide("k_free", "c_free").Returns(FactionSide.Free);
        Alignment.ResolveSide("k_evil", "c_evil").Returns(FactionSide.Evil);
        Alignment.ResolveSide("k_neutral", "c_neutral").Returns(FactionSide.Neutral);
        Alignment.ResolveSide("", "c_free").Returns(FactionSide.Free);
        Alignment.ResolveSide("", "c_evil").Returns(FactionSide.Evil);
        Alignment.ResolveSide("", "c_neutral").Returns(FactionSide.Neutral);
        Grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(call => call.ArgAt<int>(1));

        Service = new NotableServicesService(Adapter, Reveal, Alignment, Grants, Settings, ConfigProvider, Messenger, Coop, Server, Logger);
    }

    /// <summary>The player's side of a troop or prisoner: a faction-less pair with the culture.</summary>
    public static SideIds Culture(string cultureId) => new("", cultureId);

    /// <summary>Queues the gift, runs the after-conversation step and returns the screen's callbacks (null when no screen opened).</summary>
    public Action<GiftTally>? OpenScreen()
    {
        Service.QueueGift();
        Queued?.Invoke();
        return OnDone;
    }

    public void SettlementIs(SettlementKind kind, SideIds side)
    {
        Speaker.Kind = kind;
        Speaker.SettlementSide = side;
    }
}
