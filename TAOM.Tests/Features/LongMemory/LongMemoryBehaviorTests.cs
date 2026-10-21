using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TAOM.Features.LongMemory.Hooks;
using static TAOM.Tests.Features.LongMemory.LongMemoryFixture;

namespace TAOM.Tests.Features.LongMemory;

/// <summary>
/// The listeners are thin boundaries. Each hands the service what it needs (the village facts the adapter read from the event's
/// component, whether the attackers won, or the selected option's id), and a fault never escapes into the engine's event invoke,
/// which has no try of its own. Building a CampaignBehaviorBase runs engine code, hence the category.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class LongMemoryBehaviorTests
{
    private LongMemoryFixture _f = null!;
    private LongMemoryBehavior _behavior = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new LongMemoryFixture();
        _behavior = new LongMemoryBehavior(_f.Service, _f.Village, _f.Logger);
    }

    // --- the raid listener -------------------------------------------------------------------------------------------------

    [TestMethod]
    public void HandleRaid_AFinishedRaid_ReachesTheService()
    {
        _f.Village.ReadMapEvent(null).Returns(Facts());

        _behavior.HandleRaid(null);

        Assert.AreEqual(3, _f.Grants.Count);
        _f.Notice.Received(1).ShowRaid("Dol Amroth Farm");
    }

    [TestMethod]
    public void HandleRaid_TheVillageCannotBeRead_GrantsNothingAndNeverFaults()
    {
        _behavior.HandleRaid(null);

        Assert.AreEqual(0, _f.Grants.Count);
        _f.Logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void HandleRaid_AFaultInTheService_IsLoggedAndNeverEscapes()
    {
        _f.Village.ReadMapEvent(null).Returns(Facts());
        _f.Settings.Get().Returns(_ => throw new InvalidOperationException("boom"));

        _behavior.HandleRaid(null);

        _f.Logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[LongMemory]") && s.Contains("raid") && s.Contains("boom")));
    }

    [TestMethod]
    public void HandleRaid_AFaultReadingTheEvent_IsLoggedAndNeverEscapes()
    {
        _f.Village.ReadMapEvent(null).Returns(_ => throw new InvalidOperationException("no event"));

        _behavior.HandleRaid(null);

        _f.Logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[LongMemory]") && s.Contains("no event")));
    }

    // --- the forced-taking listeners ---------------------------------------------------------------------------------------

    [TestMethod]
    public void HandleCoercion_TheAttackersWon_ReachesTheService()
    {
        _f.Village.ReadMapEvent(null).Returns(Facts());

        _behavior.HandleCoercion(true, null);

        Assert.AreEqual(3, _f.Grants.Count);
        _f.Notice.Received(1).ShowCoercion("Dol Amroth Farm");
    }

    [TestMethod]
    public void HandleCoercion_TheDefendersWon_GrantsNothing_AndNeverReadsTheEvent()
    {
        _f.Village.ReadMapEvent(null).Returns(Facts());

        _behavior.HandleCoercion(false, null);

        Assert.AreEqual(0, _f.Grants.Count);
        _f.Village.DidNotReceive().ReadMapEvent(Arg.Any<TaleWorlds.CampaignSystem.MapEvents.MapEventComponent?>());
    }

    [TestMethod]
    public void HandleCoercion_AFault_IsLoggedAndNeverEscapes()
    {
        _f.Village.ReadMapEvent(null).Returns(_ => throw new InvalidOperationException("boom"));

        _behavior.HandleCoercion(true, null);

        _f.Logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[LongMemory]") && s.Contains("forced taking") && s.Contains("boom")));
    }

    [DataTestMethod]
    [DataRow(BattleSideEnum.Attacker, 3)]
    [DataRow(BattleSideEnum.Defender, 0)]
    [DataRow(BattleSideEnum.None, 0)]
    public void OnForceSuppliesCompleted_PassesWhetherTheAttackersWon(BattleSideEnum side, int expectedGrants)
    {
        _f.Village.ReadMapEvent(null).Returns(Facts());

        _behavior.OnForceSuppliesCompleted(side, null!);

        Assert.AreEqual(expectedGrants, _f.Grants.Count);
    }

    [DataTestMethod]
    [DataRow(BattleSideEnum.Attacker, 3)]
    [DataRow(BattleSideEnum.Defender, 0)]
    [DataRow(BattleSideEnum.None, 0)]
    public void OnForceVolunteersCompleted_PassesWhetherTheAttackersWon(BattleSideEnum side, int expectedGrants)
    {
        _f.Village.ReadMapEvent(null).Returns(Facts());

        _behavior.OnForceVolunteersCompleted(side, null!);

        Assert.AreEqual(expectedGrants, _f.Grants.Count);
    }

    [TestMethod]
    public void OnRaidCompleted_PassesTheComponentToTheRead_WhoeverWon()
    {
        _f.Village.ReadMapEvent(null).Returns(Facts());

        _behavior.OnRaidCompleted(BattleSideEnum.Defender, null!);

        Assert.AreEqual(3, _f.Grants.Count, "a raid counts by its damage, not by the winner");
    }

    // --- the menu option listener ------------------------------------------------------------------------------------------

    [TestMethod]
    public void HandleMenuOption_ANoResistContinue_ReachesTheService()
    {
        _f.Village.ReadCurrentVillage().Returns(Facts());

        _behavior.HandleMenuOption("force_supplies_no_resist_continue");

        Assert.AreEqual(3, _f.Grants.Count);
    }

    [TestMethod]
    public void HandleMenuOption_AnyOtherOption_GrantsNothing()
    {
        _f.Village.ReadCurrentVillage().Returns(Facts());

        _behavior.HandleMenuOption("leave");

        Assert.AreEqual(0, _f.Grants.Count);
    }

    [TestMethod]
    public void HandleMenuOption_AFault_IsLoggedAndNeverEscapes()
    {
        _f.Village.ReadCurrentVillage().Returns(_ => throw new InvalidOperationException("boom"));

        _behavior.HandleMenuOption("force_volunteers_no_resist_continue");

        _f.Logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[LongMemory]") && s.Contains("menu") && s.Contains("boom")));
    }

    [TestMethod]
    public void OnGameMenuOptionSelected_NoOption_GrantsNothingAndNeverFaults()
    {
        _behavior.OnGameMenuOptionSelected(null!, null!);

        Assert.AreEqual(0, _f.Grants.Count);
        _f.Logger.DidNotReceive().LogError(Arg.Any<string>());
    }
}
