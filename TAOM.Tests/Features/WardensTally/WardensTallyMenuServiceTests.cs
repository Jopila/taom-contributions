using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WardensTally;

namespace TAOM.Tests.Features.WardensTally;

/// <summary>
/// The glue between the dungeon menu and the tally (docs/features/wardens-tally.md): which menus it answers, when it stays
/// out, and where the War Chronicle boost comes from. The boost line is shown only where the daily escape pass runs: the
/// pass is host-only (<c>WarChronicleBehavior.OnDailyTick</c>), so a co-op client's display omits the line.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class WardensTallyMenuServiceTests
{
    private readonly IPrisonerEscapeAdapter _adapter = Substitute.For<IPrisonerEscapeAdapter>();
    private readonly IWarEffectService _effects = Substitute.For<IWarEffectService>();
    private readonly ICoopSessionProvider _coop = Substitute.For<ICoopSessionProvider>();

    private static DungeonReading Reading(string kingdom = "gondor", bool playerHeld = false)
    {
        var lord = new DungeonLord
        {
            Name = "Boromir",
            Snapshot = new PrisonerEscapeSnapshot
            {
                HeroId = "lord_boromir",
                KingdomId = kingdom,
                IsAlive = true,
                IsPrisoner = true,
                CanBeReleased = true,
                EscapeFactor = 1f,
                PlayerHeld = playerHeld,
            },
        };
        return new DungeonReading { HeldRegulars = 31, HeldLords = 1, Capacity = 60, Lords = new List<DungeonLord> { lord } };
    }

    private WardensTallyMenuService Sut(bool enabled = true)
    {
        _coop.IsAuthority.Returns(true);
        _coop.IsCoopClient.Returns(false);
        _effects.GetMultiplier("gondor", WarEffectKind.PrisonerEscape).Returns(3f);
        _adapter.ReadDungeon("town_ES1").Returns(Reading());
        return new WardensTallyMenuService(
            _adapter, new WardensTallyService(new WarEscapeService()), _effects, _coop,
            new WardensTallySettingsProvider(new TaomSettings { WardensTallyEnabled = enabled }));
    }

    [DataTestMethod]
    [DataRow("town_keep_dungeon")]
    [DataRow("castle_dungeon")]
    public void BuildIntro_ADungeonMenu_AnswersWithVanillasSentenceAndTheTally(string menuId)
    {
        var text = Sut().BuildIntro(menuId, "town_ES1");

        Assert.IsNotNull(text);
        var shown = text!.ToString();
        StringAssert.StartsWith(shown, "There is 1 prisoner here.\nHeld: 32 of 60, 1 of them a lord.");
        StringAssert.Contains(shown, "Each lord's daily chance to escape: 4.0%");
    }

    [DataTestMethod]
    [DataRow("town_keep")]
    [DataRow("castle")]
    [DataRow("town_enemy_town_keep")]
    [DataRow("")]
    [DataRow(null)]
    public void BuildIntro_AnyOtherMenu_IsNull_AndReadsNothing(string? menuId)
    {
        Assert.IsNull(Sut().BuildIntro(menuId, "town_ES1"));

        _adapter.DidNotReceiveWithAnyArgs().ReadDungeon(default!);
    }

    [TestMethod]
    public void BuildIntro_SwitchOff_IsNull_AndReadsNothing()
    {
        Assert.IsNull(Sut(enabled: false).BuildIntro("town_keep_dungeon", "town_ES1"));

        _adapter.DidNotReceiveWithAnyArgs().ReadDungeon(default!);
    }

    [TestMethod]
    public void BuildIntro_NoReading_IsNull()
    {
        var sut = Sut();

        Assert.IsNull(sut.BuildIntro("town_keep_dungeon", "town_unknown"));
    }

    [TestMethod]
    public void BuildIntro_ACoopClientWithAnUnknownSettlement_IsNull()
    {
        var sut = Sut();
        _coop.IsAuthority.Returns(false);
        _coop.IsCoopClient.Returns(true);

        Assert.IsNull(sut.BuildIntro("town_keep_dungeon", "town_unknown"));
    }

    [TestMethod]
    public void BuildIntro_NoSettlement_IsNull()
    {
        Assert.IsNull(Sut().BuildIntro("town_keep_dungeon", null));
    }

    [TestMethod]
    public void BuildIntro_EmptyDungeon_IsNull()
    {
        var sut = Sut();
        _adapter.ReadDungeon("town_empty").Returns(new DungeonReading());

        Assert.IsNull(sut.BuildIntro("castle_dungeon", "town_empty"));
    }

    [TestMethod]
    public void BuildIntro_NoLords_IsNull_SoVanillasNoPrisonersSentenceStandsAlone()
    {
        var sut = Sut();
        _adapter.ReadDungeon("town_regulars").Returns(new DungeonReading { HeldRegulars = 25, Capacity = 100 });

        Assert.IsNull(sut.BuildIntro("town_keep_dungeon", "town_regulars"));
    }

    [TestMethod]
    public void BuildIntro_TheHostWithAPlayerHeldLord_ShowsTheHalvedChanceAndTheHalvedLine()
    {
        var sut = Sut();
        _adapter.ReadDungeon("town_ES1").Returns(Reading(playerHeld: true));

        var shown = sut.BuildIntro("town_keep_dungeon", "town_ES1")!.ToString();

        StringAssert.Contains(shown, "Each lord's daily chance to escape: 2.0%");
        StringAssert.Contains(shown, "Held by your clan: halved");
    }

    // The server rolls without the halving (its player clan is not this client's), so a client shows the chance it will get.
    [TestMethod]
    public void BuildIntro_ACoopClientWithAPlayerHeldLord_ShowsTheUnhalvedChance_AndNoHalvedLine()
    {
        var sut = Sut();
        _adapter.ReadDungeon("town_ES1").Returns(Reading(playerHeld: true));
        _coop.IsAuthority.Returns(false);
        _coop.IsCoopClient.Returns(true);

        var shown = sut.BuildIntro("town_keep_dungeon", "town_ES1")!.ToString();

        StringAssert.Contains(shown, "Each lord's daily chance to escape: 4.0%");
        Assert.IsFalse(shown.Contains("halved"), shown);
    }

    [TestMethod]
    public void BuildIntro_ABoostedKingdomOnTheHost_ShowsTheBoostLine()
    {
        var shown = Sut().BuildIntro("town_keep_dungeon", "town_ES1")!.ToString();

        StringAssert.Contains(shown, "Boromir: 11.7% a day with the war's escape boost.");
    }

    [TestMethod]
    public void BuildIntro_ACoopClient_OmitsTheBoostLine_AndNeverAsksTheRegistry()
    {
        var sut = Sut();
        _coop.IsAuthority.Returns(false);
        _coop.IsCoopClient.Returns(true);

        var shown = sut.BuildIntro("town_keep_dungeon", "town_ES1")!.ToString();

        StringAssert.Contains(shown, "Each lord's daily chance to escape: 4.0%");
        Assert.IsFalse(shown.Contains("boost"), shown);
        _effects.DidNotReceiveWithAnyArgs().GetMultiplier(default, default);
    }

    [TestMethod]
    public void BuildIntro_AClientThatIsNotTheAuthority_OmitsTheBoostLine()
    {
        var sut = Sut();
        _coop.IsAuthority.Returns(false);
        _coop.IsCoopClient.Returns(false);

        Assert.IsFalse(sut.BuildIntro("town_keep_dungeon", "town_ES1")!.ToString().Contains("boost"));
    }

    [TestMethod]
    public void BuildIntro_TheAuthorityReadsTheLiveSessionEachCall()
    {
        var sut = Sut();
        Assert.IsTrue(sut.BuildIntro("town_keep_dungeon", "town_ES1")!.ToString().Contains("boost"));

        _coop.IsAuthority.Returns(false);
        _coop.IsCoopClient.Returns(true);

        Assert.IsFalse(sut.BuildIntro("town_keep_dungeon", "town_ES1")!.ToString().Contains("boost"));
    }

    [TestMethod]
    public void BuildIntro_AnAdapterFault_Propagates_SoTheHookCanLogItOnce()
    {
        var sut = Sut();
        _adapter.ReadDungeon("town_ES1").Returns(_ => throw new System.InvalidOperationException("engine"));

        Assert.ThrowsException<System.InvalidOperationException>(() => sut.BuildIntro("town_keep_dungeon", "town_ES1"));
    }
}
