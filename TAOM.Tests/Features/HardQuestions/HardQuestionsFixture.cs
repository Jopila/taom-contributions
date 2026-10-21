using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.HardQuestions;
using TAOM.Features.HardQuestions.Domain;
using TAOM.Features.InitialChildGeneration;

namespace TAOM.Tests.Features.HardQuestions;

/// <summary>The service graph over substitutes, with the alignment rule neutral and relations passed through.</summary>
internal sealed class HardQuestionsFixture
{
    public HardQuestionsState State { get; } = new();
    public HardQuestionsConfig Config { get; } = new();
    public IHardQuestionsConfigProvider ConfigProvider { get; } = Substitute.For<IHardQuestionsConfigProvider>();
    public IHardQuestionsSettingsProvider Settings { get; } = Substitute.For<IHardQuestionsSettingsProvider>();
    public ICoopSessionProvider Coop { get; } = Substitute.For<ICoopSessionProvider>();
    public IDedicatedServerProvider Server { get; } = Substitute.For<IDedicatedServerProvider>();
    public ICaptiveLordAdapter Captives { get; } = Substitute.For<ICaptiveLordAdapter>();
    public IArmouryPlayerAdapter Player { get; } = Substitute.For<IArmouryPlayerAdapter>();
    public IHeroSkillXpAdapter Skills { get; } = Substitute.For<IHeroSkillXpAdapter>();
    public IHardQuestionsActionAdapter Actions { get; } = Substitute.For<IHardQuestionsActionAdapter>();
    public IMapRevealAdapter Reveal { get; } = Substitute.For<IMapRevealAdapter>();
    public IRandomSource Random { get; } = Substitute.For<IRandomSource>();
    public IAlignmentService Alignment { get; } = Substitute.For<IAlignmentService>();
    public IExecutionRelationService Relations { get; } = Substitute.For<IExecutionRelationService>();
    public IPlayerTraitAdapter Traits { get; } = Substitute.For<IPlayerTraitAdapter>();
    public IArmourGateService Gate { get; } = Substitute.For<IArmourGateService>();
    public ILordEquipmentAdapter Equipment { get; } = Substitute.For<ILordEquipmentAdapter>();
    public IModLogger Logger { get; } = Substitute.For<IModLogger>();
    public HardQuestionsPenaltyApplier Penalties { get; }
    public StripService Strip { get; }
    public InterrogationService Service { get; }

    public HardQuestionsFixture()
    {
        ConfigProvider.GetConfig().Returns(Config);
        Settings.IsEnabled.Returns(true);
        Player.HeroId.Returns("main_hero");
        Player.KingdomId.Returns("k_player");
        Player.CultureId.Returns("c_player");
        Player.Side.Returns(new SideIds("k_player", "c_player"));
        Player.Gold.Returns(100000);
        Player.AddPiece(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>()).Returns(true);
        Actions.PayGold(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        Actions.ChangeRelation(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        Relations.GetRelationModifier(Arg.Any<SideIds>(), Arg.Any<SideIds>(),
            Arg.Any<SideIds>(), Arg.Any<int>(), Arg.Any<bool>())
            .Returns(call => new ExecutionRelationResult(call.ArgAt<int>(3), false));
        Alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(FactionSide.Neutral);

        Penalties = new HardQuestionsPenaltyApplier(Alignment, Relations, Player, Actions, Traits, ConfigProvider, Server);
        Strip = new StripService(State, ConfigProvider, Captives, Equipment, Player, Gate, Actions, Penalties, Server, Logger);
        Service = new InterrogationService(State, ConfigProvider, Settings, Coop, Captives, Player, Skills, Actions,
            Reveal, Random, Penalties, Strip, Logger);
    }

    /// <summary>The circle last stubbed by <see cref="Captive"/>.</summary>
    public CaptiveCircle Circle { get; private set; } = null!;

    /// <summary>A captive lord held by the player's clan, found by the captive adapter under his id.</summary>
    public CaptiveSnapshot Captive(string id = "lord1", int ransom = 1000, string? kingdom = "k_lord1", string? relationId = null)
    {
        var lord = new LordRef(id, kingdom, new SideIds(kingdom ?? string.Empty, "c_" + id), relationId);
        var snapshot = new CaptiveSnapshot(lord)
        {
            ClanId = "clan_" + id,
            CaptorIsPlayerClan = true,
            RansomValue = ransom,
        };
        Captives.Snapshot(id).Returns(snapshot);
        Circle = new CaptiveCircle(lord);
        Captives.Circle(id).Returns(Circle);
        return snapshot;
    }

    public static LordRef Lord(string id, string? relationId = null) =>
        new(id, "k_" + id, new SideIds("k_" + id, "c_" + id), relationId);

    public static List<LordRef> Lords(params string[] ids) => ids.Select(id => Lord(id)).ToList();
}
