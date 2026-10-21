using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.BondsOfBattle;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Execution;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.LordCourtesies;
using TAOM.Features.LordCourtesies.Domain;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.LordCourtesies;

/// <summary>
/// The service graph over substitutes: a free, willing Free lord with a party beside the player, talking to a Free
/// player. The hand-over service is the real Bonds of Battle one over substitutes, so the gift is paid by its rule.
/// </summary>
internal sealed class LordCourtesiesFixture
{
    public const string LordId = "lord1";
    public const string OwnerId = "owner1";

    public LordCourtesiesConfig Config { get; } = new();
    public ILordCourtesiesConfigProvider ConfigProvider { get; } = Substitute.For<ILordCourtesiesConfigProvider>();
    public ILordCourtesiesSettingsProvider Settings { get; } = Substitute.For<ILordCourtesiesSettingsProvider>();
    public ILordCourtesyAdapter Lords { get; } = Substitute.For<ILordCourtesyAdapter>();
    public IArmouryPlayerAdapter Player { get; } = Substitute.For<IArmouryPlayerAdapter>();
    public IAlignmentService Alignment { get; } = Substitute.For<IAlignmentService>();
    public IMapRevealAdapter Reveal { get; } = Substitute.For<IMapRevealAdapter>();
    public ICoopSessionProvider Coop { get; } = Substitute.For<ICoopSessionProvider>();
    public IDedicatedServerProvider Server { get; } = Substitute.For<IDedicatedServerProvider>();
    public IModLogger Logger { get; } = Substitute.For<IModLogger>();
    public IRelationGrantService Grants { get; } = Substitute.For<IRelationGrantService>();
    public IBondsNoticeAdapter Notice { get; } = Substitute.For<IBondsNoticeAdapter>();
    public IBondsOfBattleSettingsProvider BondsSettings { get; } = Substitute.For<IBondsOfBattleSettingsProvider>();
    public TroopHandoverService Handover { get; }
    public LordCourtesySnapshot Lord { get; }
    public LordCourtesiesService Service { get; }

    /// <summary>The action handed to <c>RunAfterConversation</c>, or null.</summary>
    public Action? Queued { get; private set; }

    /// <summary>The engine-commit callback handed to <c>OpenGiftScreen</c>, or null.</summary>
    public Action? OnDone { get; private set; }

    public LordCourtesiesFixture()
    {
        ConfigProvider.GetConfig().Returns(Config);
        Settings.TroopGiftsEnabled.Returns(true);
        Settings.DomainRevealEnabled.Returns(true);
        Player.KingdomId.Returns("k_player");
        Player.CultureId.Returns("c_player");
        Player.Side.Returns(new SideIds("k_player", "c_player"));
        Player.ChangeRelationWith(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        Lord = new LordCourtesySnapshot(LordId)
        {
            ClanId = "clan_lord1",
            KingdomId = "k_lord",
            Side = new SideIds("k_lord", "c_lord"),
            LeadsOwnParty = true,
            IsWithPlayer = true,
            HasRoom = true,
            PlayerHasTroops = true,
            Relation = 10,
            Holdings = 4,
            Fortifications = 3,
        };
        Lords.ConversationPartnerId.Returns(LordId);
        Lords.Snapshot(LordId).Returns(Lord);
        Lords.LordName(LordId).Returns("Lord One");
        Lords.RelationOwnerId(LordId).Returns(OwnerId);
        Lords.GiftInfluence(Arg.Any<IReadOnlyList<TroopCount>>()).Returns(50);
        Lords.When(l => l.RunAfterConversation(Arg.Any<Action>())).Do(c => Queued = c.Arg<Action>());
        Lords.When(l => l.OpenGiftScreen(Arg.Any<string>(), Arg.Any<Action>())).Do(c => OnDone = c.ArgAt<Action>(1));
        Sides(FactionSide.Free, FactionSide.Free);

        Grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(call => call.ArgAt<int>(1));
        BondsSettings.Get().Returns(new BondsOfBattleSettings(true, 1, false, 5, true, 25f, true, 15));
        var random = Substitute.For<IRandomSource>();
        random.Next(-1, 2).Returns(0);
        Handover = new TroopHandoverService(Grants, BondsSettings, Substitute.For<IEnlistmentStateQuery>(), random, Notice, Coop);

        Service = new LordCourtesiesService(Lords, Player, Alignment, Settings, ConfigProvider, Handover, Reveal, Coop, Server, Logger);
    }

    public void Sides(FactionSide player, FactionSide lord)
    {
        Alignment.ResolveSide("k_player", "c_player").Returns(player);
        Alignment.ResolveSide("k_lord", "c_lord").Returns(lord);
    }

    /// <summary>The lord's roster readings in order: before the screen opens, then at each commit.</summary>
    public void Rosters(params IReadOnlyList<TroopCount>[] readings)
    {
        Lords.MemberTroops(LordId).Returns(readings[0], readings.Skip(1).ToArray());
    }
}
