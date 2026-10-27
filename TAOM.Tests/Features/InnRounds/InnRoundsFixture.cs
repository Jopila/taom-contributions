using System;
using System.Collections.Generic;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.InnRounds;
using TAOM.Features.InnRounds.Domain;
using TAOM.Features.Messengers;
using TAOM.Features.NotableServices;

namespace TAOM.Tests.Features.InnRounds;

/// <summary>
/// The service graph over substitutes: a tavern keeper of a Gondor town the player stands in, at peace, a party of 12 and 5 in
/// the tavern, wine at 20 a head, a purse of 1000, day 10, a draw of 0.5 and a single-player session.
/// </summary>
public sealed class InnRoundsFixture
{
    public const string TownId = "town_1";
    public const string PlayerId = "player1";

    public InnRoundsConfig Config { get; } = new();
    public NotableServicesConfig RevealConfig { get; } = new();
    public IInnRoundsConfigProvider ConfigProvider { get; } = Substitute.For<IInnRoundsConfigProvider>();
    public INotableServicesConfigProvider RevealConfigProvider { get; } = Substitute.For<INotableServicesConfigProvider>();
    public IInnRoundsSettingsProvider Settings { get; } = Substitute.For<IInnRoundsSettingsProvider>();
    public IInnRoundsAdapter Adapter { get; } = Substitute.For<IInnRoundsAdapter>();
    public IMapRevealAdapter Map { get; } = Substitute.For<IMapRevealAdapter>();
    public IHeroSkillXpAdapter Skills { get; } = Substitute.For<IHeroSkillXpAdapter>();
    public IHeroRenownAdapter Renown { get; } = Substitute.For<IHeroRenownAdapter>();
    public IRandomSource Random { get; } = Substitute.For<IRandomSource>();
    public IMessengerConversationState Messenger { get; } = Substitute.For<IMessengerConversationState>();
    public ICoopSessionProvider Coop { get; } = Substitute.For<ICoopSessionProvider>();
    public IDedicatedServerProvider Server { get; } = Substitute.For<IDedicatedServerProvider>();
    public IModLogger Logger { get; } = Substitute.For<IModLogger>();
    public TavernSnapshot Keeper { get; }
    public RoundLedger Ledger { get; } = new();
    public InnRoundsService Service { get; }

    public InnRoundsFixture()
    {
        ConfigProvider.GetConfig().Returns(Config);
        RevealConfigProvider.GetConfig().Returns(RevealConfig);
        Settings.Enabled.Returns(true);
        Settings.MoraleScale.Returns(1f);
        Settings.RenownScale.Returns(1f);
        Coop.IsAuthority.Returns(true);
        Keeper = new TavernSnapshot { IsTavernkeeper = true, InTown = true, SettlementId = TownId, CultureId = "gondor" };
        Adapter.Keeper().Returns(Keeper);
        Adapter.Today.Returns(10);
        Adapter.PlayerHeroId.Returns(PlayerId);
        Adapter.PlayerGold.Returns(1000);
        Adapter.PartyHeadcount().Returns(12);
        Adapter.TavernHeadcount(TownId).Returns(5);
        Adapter.DrinkPrice(TownId, "wine").Returns(20);
        Adapter.DrinkPrice(TownId, "beer").Returns(8);
        Adapter.PayTown(TownId, Arg.Any<int>()).Returns(true);
        Adapter.AddMorale(Arg.Any<float>()).Returns(call => call.Arg<float>());
        Skills.AddSkillXpLanded(PlayerId, Arg.Any<string>(), Arg.Any<float>()).Returns(call => call.Arg<float>());
        Renown.AddClanRenown(PlayerId, Arg.Any<int>()).Returns(true);
        Random.NextDouble().Returns(0.5);
        Map.MapDiagonal.Returns(1000f);
        Map.Track(Arg.Any<IReadOnlyList<string>>()).Returns(call => call.Arg<IReadOnlyList<string>>().Count);

        var holds = new HoldsRevealService(Map, RevealConfigProvider, Logger);
        Service = new InnRoundsService(Adapter, holds, ConfigProvider, Settings, Skills, Renown, Random, Messenger, Coop, Server, Logger);
    }

    /// <summary>Two towns and castles in reach of the keeper's town, both unmarked.</summary>
    public void HoldsAround(params RevealCandidate[] candidates) => Map.FortificationsAround(TownId).Returns(candidates);

    public void HoldsAreNear() => HoldsAround(new RevealCandidate("town_a", 40f, false), new RevealCandidate("town_b", 90f, false));

    /// <summary>One closed gate each, for the gate tests: the reason it is named by, and the way to close it.</summary>
    public static IEnumerable<(string Name, Action<InnRoundsFixture> Close)> Gates()
    {
        yield return ("switch off", f => f.Settings.Enabled.Returns(false));
        yield return ("co-op session", f => f.Coop.IsSessionActive.Returns(true));
        yield return ("defer to host", f => f.Coop.ShouldDeferToHost.Returns(true));
        yield return ("dedicated server", f => f.Server.IsDedicatedServer.Returns(true));
        yield return ("not the authority", f => f.Coop.IsAuthority.Returns(false));
        yield return ("messenger open", f => f.Messenger.IsOpen.Returns(true));
        yield return ("not a tavern keeper", f => f.Keeper.IsTavernkeeper = false);
        yield return ("not in a town", f => f.Keeper.InTown = false);
        yield return ("no conversation", f => f.Adapter.Keeper().Returns((TavernSnapshot?)null));
    }
}
