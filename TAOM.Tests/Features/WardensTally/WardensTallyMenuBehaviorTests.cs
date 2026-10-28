using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WardensTally;
using TAOM.Features.WardensTally.Hooks;

namespace TAOM.Tests.Features.WardensTally;

/// <summary>
/// The listener on the menu-opened event (#903): it overwrites the menu's intro variable only for the two dungeon menus, only
/// when vanilla's init set it, and a fault leaves vanilla's text and is logged once per instance. The intro variable is a
/// process-global text variable, so each test sets vanilla's sentence first and reads it back.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class WardensTallyMenuBehaviorTests
{
    private const string Variable = "PRISONER_INTRODUCTION";
    private const string VanillaSentence = "There is 1 prisoner here.";

    private readonly IPrisonerEscapeAdapter _adapter = Substitute.For<IPrisonerEscapeAdapter>();
    private readonly IModLogger _logger = Substitute.For<IModLogger>();
    private WardensTallyMenuBehavior _sut = null!;

    [TestInitialize]
    public void Init()
    {
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        var lord = new DungeonLord
        {
            Name = "Boromir",
            Snapshot = new PrisonerEscapeSnapshot { IsAlive = true, IsPrisoner = true, CanBeReleased = true, EscapeFactor = 1f },
        };
        _adapter.ReadDungeon("town_ES1").Returns(new DungeonReading
        {
            HeldRegulars = 5, HeldLords = 1, Capacity = 100, Lords = new List<DungeonLord> { lord },
        });
        _adapter.ReadDungeon("town_three").Returns(new DungeonReading
        {
            HeldRegulars = 10, HeldLords = 3, Capacity = 100, Lords = new List<DungeonLord> { lord, lord, lord },
        });
        var service = new WardensTallyMenuService(
            _adapter, new WardensTallyService(new WarEscapeService()), Substitute.For<IWarEffectService>(), coop,
            new WardensTallySettingsProvider(new TaomSettings()));
        _sut = new WardensTallyMenuBehavior(service, _logger);
        MBTextManager.SetTextVariable(Variable, VanillaSentence);
    }

    private void Run(bool vanillaSetIntro, string menuId, string settlementId) =>
        _sut.Guard(() => _sut.Apply(vanillaSetIntro, menuId, settlementId));

    private static string Intro() => new TextObject("{=!}{" + Variable + "}").ToString();

    [DataTestMethod]
    [DataRow("town_keep_dungeon")]
    [DataRow("castle_dungeon")]
    public void Apply_ADungeonMenuVanillaSetUp_AddsTheTallyUnderVanillasSentence(string menuId)
    {
        _sut.Apply(true, menuId, "town_ES1");

        StringAssert.StartsWith(Intro(), VanillaSentence + "\nHeld: 6 of 100, 1 of them a lord.");
    }

    [TestMethod]
    public void Apply_ThreeLords_PutsTheVanillaPluralFormInTheGlobalVariable()
    {
        _sut.Apply(true, "town_keep_dungeon", "town_three");

        StringAssert.StartsWith(Intro(), "There are 3 prisoners here.\nHeld: 13 of 100, 3 of them lords.");
    }

    [TestMethod]
    public void Apply_ADungeonWithNoLords_LeavesVanillasSentence()
    {
        _adapter.ReadDungeon("town_regulars").Returns(new DungeonReading { HeldRegulars = 25, Capacity = 100 });

        _sut.Apply(true, "town_keep_dungeon", "town_regulars");

        Assert.AreEqual(VanillaSentence, Intro());
    }

    [TestMethod]
    public void Apply_VanillaDidNotSetTheIntro_LeavesTheVariableAlone()
    {
        _sut.Apply(false, "town_keep_dungeon", "town_ES1");

        Assert.AreEqual(VanillaSentence, Intro());
        _adapter.DidNotReceiveWithAnyArgs().ReadDungeon(default!);
    }

    [TestMethod]
    public void Apply_AnotherMenu_LeavesTheVariableAlone()
    {
        _sut.Apply(true, "town_keep", "town_ES1");

        Assert.AreEqual(VanillaSentence, Intro());
    }

    [TestMethod]
    public void Apply_NothingToAdd_LeavesTheVariableAlone()
    {
        _sut.Apply(true, "town_keep_dungeon", "town_unknown");

        Assert.AreEqual(VanillaSentence, Intro());
    }

    [TestMethod]
    public void Guard_AFault_KeepsVanillasText_AndLogsOncePerInstance()
    {
        _adapter.ReadDungeon("town_ES1").Returns(_ => throw new System.InvalidOperationException("engine"));

        Run(true, "town_keep_dungeon", "town_ES1");
        Run(true, "town_keep_dungeon", "town_ES1");

        Assert.AreEqual(VanillaSentence, Intro());
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void OnMenuOpened_NullArgs_KeepsVanillasText()
    {
        _sut.OnMenuOpened(null!);

        Assert.AreEqual(VanillaSentence, Intro());
    }

    [TestMethod]
    public void OnMenuOpened_ArgsVanillaDidNotTitle_LeavesTheVariableAlone()
    {
        _sut.OnMenuOpened(new MenuCallbackArgs((TaleWorlds.CampaignSystem.GameState.MenuContext)null!, null!));

        Assert.AreEqual(VanillaSentence, Intro());
    }

    [TestMethod]
    public void SyncData_SavesNothing()
    {
        var store = Substitute.For<TaleWorlds.CampaignSystem.IDataStore>();

        _sut.SyncData(store);

        Assert.AreEqual(0, store.ReceivedCalls().Count());
    }
}
