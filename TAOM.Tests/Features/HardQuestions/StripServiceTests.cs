using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.HardQuestions;
using TAOM.Features.HardQuestions.Domain;

namespace TAOM.Tests.Features.HardQuestions;

[TestClass]
public class StripServiceTests
{
    private HardQuestionsFixture _f = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new HardQuestionsFixture();
        _f.Captive();
        _f.Equipment.IsSlotEmpty(Arg.Any<string>(), Arg.Any<LordEquipmentSet>(), Arg.Any<int>()).Returns(true);
    }

    private void Wears(params LordEquipmentPiece[] pieces) =>
        _f.Equipment.ReadPieces("lord1").Returns(new List<LordEquipmentPiece>(pieces));

    private static LordEquipmentPiece Piece(LordEquipmentSet set, int slot, string item, string? modifier = null) =>
        new(set, slot, item, modifier);

    [TestMethod]
    public void Strip_LootsEveryPieceAndClearsItsSlot()
    {
        Wears(Piece(LordEquipmentSet.Battle, 0, "sword"), Piece(LordEquipmentSet.Battle, 5, "helm"),
            Piece(LordEquipmentSet.Battle, 10, "horse"));

        Assert.IsTrue(_f.Strip.Strip("lord1"));

        _f.Player.Received().AddPiece("sword", null, 1);
        _f.Player.Received().AddPiece("helm", null, 1);
        _f.Player.Received().AddPiece("horse", null, 1);
        _f.Equipment.Received().ClearSlot("lord1", LordEquipmentSet.Battle, 0);
        _f.Equipment.Received().ClearSlot("lord1", LordEquipmentSet.Battle, 5);
        _f.Equipment.Received().ClearSlot("lord1", LordEquipmentSet.Battle, 10);
    }

    [TestMethod]
    public void Strip_ThePlayersInventoryKeepsTheModifier()
    {
        Wears(Piece(LordEquipmentSet.Battle, 0, "sword", "sharp"));

        _f.Strip.Strip("lord1");

        _f.Player.Received().AddPiece("sword", "sharp", 1);
    }

    [TestMethod]
    public void Strip_NamedPiecesStayWithTheLord()
    {
        _f.Gate.GetClass("named_sword").Returns(ArmourClass.Named);
        _f.Gate.GetClass("helm").Returns(ArmourClass.Heavy);
        Wears(Piece(LordEquipmentSet.Battle, 0, "named_sword"), Piece(LordEquipmentSet.Battle, 5, "helm"));

        _f.Strip.Strip("lord1");

        _f.Player.DidNotReceive().AddPiece("named_sword", Arg.Any<string?>(), Arg.Any<int>());
        _f.Equipment.DidNotReceive().ClearSlot("lord1", LordEquipmentSet.Battle, 0);
        _f.Equipment.Received().ClearSlot("lord1", LordEquipmentSet.Battle, 5);
    }

    [TestMethod]
    public void Strip_APieceInSeveralSetsIsLootedOnceButClearedEverywhere()
    {
        Wears(Piece(LordEquipmentSet.Battle, 6, "tunic"), Piece(LordEquipmentSet.Civilian, 6, "tunic"),
            Piece(LordEquipmentSet.Stealth, 6, "tunic"), Piece(LordEquipmentSet.Battle, 7, "tunic", "worn"));

        _f.Strip.Strip("lord1");

        _f.Player.Received(1).AddPiece("tunic", null, 1);
        _f.Player.Received(1).AddPiece("tunic", "worn", 1);
        _f.Equipment.Received().ClearSlot("lord1", LordEquipmentSet.Battle, 6);
        _f.Equipment.Received().ClearSlot("lord1", LordEquipmentSet.Civilian, 6);
        _f.Equipment.Received().ClearSlot("lord1", LordEquipmentSet.Stealth, 6);
    }

    [TestMethod]
    public void Strip_TwoIdenticalStacksInOneSet_AreBothLooted()
    {
        Wears(Piece(LordEquipmentSet.Battle, 0, "javelin"), Piece(LordEquipmentSet.Battle, 1, "javelin"),
            Piece(LordEquipmentSet.Civilian, 0, "javelin"));

        _f.Strip.Strip("lord1");

        _f.Player.Received(1).AddPiece("javelin", null, 2);
        _f.Equipment.Received().ClearSlot("lord1", LordEquipmentSet.Battle, 0);
        _f.Equipment.Received().ClearSlot("lord1", LordEquipmentSet.Battle, 1);
        _f.Equipment.Received().ClearSlot("lord1", LordEquipmentSet.Civilian, 0);
    }

    [TestMethod]
    public void Strip_AGroupThePlayerCannotReceive_StaysWornInEverySlot()
    {
        _f.Player.AddPiece("odd", null, 2).Returns(false);
        Wears(Piece(LordEquipmentSet.Battle, 0, "odd"), Piece(LordEquipmentSet.Battle, 1, "odd"));

        _f.Strip.Strip("lord1");

        _f.Equipment.DidNotReceive().ClearSlot(Arg.Any<string>(), Arg.Any<LordEquipmentSet>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Strip_LeavesRagsInTheBodySlotOfTheBattleAndCivilianSetsOnly()
    {
        Wears();

        _f.Strip.Strip("lord1");

        _f.Equipment.Received().SetItem("lord1", LordEquipmentSet.Battle, 6, "torn_bandit_clothes");
        _f.Equipment.Received().SetItem("lord1", LordEquipmentSet.Civilian, 6, "torn_bandit_clothes");
        _f.Equipment.DidNotReceive().SetItem(Arg.Any<string>(), LordEquipmentSet.Stealth, Arg.Any<int>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Strip_ABodySlotStillHoldingANamedPiece_GetsNoRags()
    {
        Wears();
        _f.Equipment.IsSlotEmpty("lord1", LordEquipmentSet.Battle, 6).Returns(false);

        _f.Strip.Strip("lord1");

        _f.Equipment.DidNotReceive().SetItem("lord1", LordEquipmentSet.Battle, 6, Arg.Any<string>());
        _f.Equipment.Received().SetItem("lord1", LordEquipmentSet.Civilian, 6, "torn_bandit_clothes");
    }

    [TestMethod]
    public void BodySlot_IsTheEnginesBodyIndex()
    {
        Assert.AreEqual((int)EquipmentIndex.Body, StripService.BodySlot);
    }

    [TestMethod]
    public void Strip_PersonalRelations_EachClanLordTakesTheClanHit()
    {
        _f.Circle.ClanLords = HardQuestionsFixture.Lords("chief", "cousin");
        _f.Circle.KingdomLords = HardQuestionsFixture.Lords("chief", "cousin");
        Wears();

        _f.Strip.Strip("lord1");

        _f.Actions.Received(1).ChangeRelation("lord1", -100);
        _f.Actions.Received(1).ChangeRelation("chief", -20);
        _f.Actions.Received(1).ChangeRelation("cousin", -20);
        _f.Actions.Received(3).ChangeRelation(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Strip_VanillaMapping_ClanLordsMergeIntoTheVictimsOwner()
    {
        _f.Captive(relationId: "chief");
        _f.Circle.ClanLords = new List<LordRef> { HardQuestionsFixture.Lord("chief"), HardQuestionsFixture.Lord("cousin", "chief") };

        _f.Strip.Strip("lord1");

        _f.Actions.Received(1).ChangeRelation("chief", -100);
        _f.Actions.Received(1).ChangeRelation(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Strip_ChargesHonorRenownAndRelationsOncePerRelationOwner()
    {
        _f.Circle.KingdomLords = HardQuestionsFixture.Lords("uncle", "baron");
        _f.Circle.HonorPositiveLords = HardQuestionsFixture.Lords("baron", "stranger");
        Wears();

        Assert.IsTrue(_f.Strip.Strip("lord1"));

        _f.Traits.Received().AddTraitXp("Honor", -1000);
        _f.Actions.Received().LoseRenown(200);
        _f.Actions.Received().ChangeRelation("lord1", -100);
        _f.Actions.Received().ChangeRelation("uncle", -15);
        _f.Actions.Received().ChangeRelation("baron", -15);
        _f.Actions.Received().ChangeRelation("stranger", -10);
        _f.Actions.Received(4).ChangeRelation(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Strip_ChargesTheVictimsClanOnceAtMinusHundred()
    {
        _f.Captive(relationId: "chief");
        _f.Circle.KingdomLords = new List<LordRef> { HardQuestionsFixture.Lord("cousin", "chief") };
        Wears();

        _f.Strip.Strip("lord1");

        _f.Actions.Received(1).ChangeRelation("chief", -100);
        _f.Actions.Received(1).ChangeRelation(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Strip_ChargingThreeHitsOnTwoOwners_ShowsOneSummaryWithTwo()
    {
        _f.Circle.KingdomLords = new List<LordRef> { HardQuestionsFixture.Lord("uncle"), HardQuestionsFixture.Lord("cousin", "uncle") };
        Wears();

        _f.Strip.Strip("lord1");

        _f.Actions.ReceivedWithAnyArgs(1).ShowRelationsHurt(default);
        _f.Actions.Received(1).ShowRelationsHurt(2);
    }

    [TestMethod]
    public void Strip_ThatChargesNobody_ShowsNoSummary()
    {
        _f.Config.StripLordRelation = 0f;
        Wears();

        _f.Strip.Strip("lord1");

        _f.Actions.DidNotReceiveWithAnyArgs().ShowRelationsHurt(default);
    }

    [TestMethod]
    public void Strip_OnlyTheFirstThreeHonourableCompanionsReproachThePlayer()
    {
        _f.Circle.Companions = new List<CompanionRef>
        {
            new("c1", 0, 2), new("c2", 0, 1), new("c3", 0, 2), new("c4", 0, 1), new("rogue", 0, 0),
        };
        Wears();

        _f.Strip.Strip("lord1");

        _f.Actions.Received().ChangeRelation("c1", -10);
        _f.Actions.Received().ChangeRelation("c2", -5);
        _f.Actions.Received().ChangeRelation("c3", -10);
        _f.Actions.Received().ChangeRelation("c4", -5);
        _f.Actions.DidNotReceive().ChangeRelation("rogue", Arg.Any<int>());
        _f.Actions.Received().ShowReproach("c1");
        _f.Actions.Received().ShowReproach("c2");
        _f.Actions.Received().ShowReproach("c3");
        _f.Actions.DidNotReceive().ShowReproach("c4");
    }

    [TestMethod]
    public void Strip_OnADedicatedServer_ShowsNoReproach()
    {
        _f.Circle.Companions = new List<CompanionRef> { new("c1", 0, 2) };
        _f.Server.IsDedicatedServer.Returns(true);
        Wears();

        _f.Strip.Strip("lord1");

        _f.Actions.Received().ChangeRelation("c1", -10);
        _f.Actions.DidNotReceive().ShowReproach(Arg.Any<string>());
    }

    [TestMethod]
    public void Strip_TheLordIsNoLongerACaptive_TakesNothing()
    {
        _f.Captives.Circle("lord1").Returns((CaptiveCircle?)null);

        Assert.IsFalse(_f.Strip.Strip("lord1"));

        _f.Logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("strip was dropped")));
        _f.Equipment.DidNotReceiveWithAnyArgs().ReadPieces(default!);
        _f.Actions.DidNotReceiveWithAnyArgs().LoseRenown(default);
    }

    [TestMethod]
    public void OnReleased_AStrippedLivingLord_GetsAFreshKitAndForgetsHisTopics()
    {
        _f.State.MarkStripped("lord1");
        _f.State.MarkAsked(HardQuestionTopic.Clan, "lord1");
        _f.Captives.IsAlive("lord1").Returns(true);
        _f.Equipment.GiveFreshKit("lord1", "torn_bandit_clothes").Returns(true);

        _f.Strip.OnReleased("lord1");

        _f.Equipment.Received().GiveFreshKit("lord1", "torn_bandit_clothes");
        Assert.IsFalse(_f.State.IsStripped("lord1"));
        Assert.IsFalse(_f.State.IsAsked(HardQuestionTopic.Clan, "lord1"));
        _f.Logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void OnReleased_WhenTheEngineGivesNoKit_LogsAndStillForgets()
    {
        _f.State.MarkStripped("lord1");
        _f.Captives.IsAlive("lord1").Returns(true);
        _f.Equipment.GiveFreshKit("lord1", "torn_bandit_clothes").Returns(false);

        _f.Strip.OnReleased("lord1");

        _f.Logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("no fresh kit")));
        Assert.IsFalse(_f.State.IsStripped("lord1"));
    }

    [TestMethod]
    public void OnReleased_ADeadLord_GetsNoKit()
    {
        _f.State.MarkStripped("lord1");
        _f.Captives.IsAlive("lord1").Returns(false);

        _f.Strip.OnReleased("lord1");

        _f.Equipment.DidNotReceiveWithAnyArgs().GiveFreshKit(default!, default!);
        Assert.IsFalse(_f.State.IsStripped("lord1"));
    }

    [TestMethod]
    public void OnReleased_ALordNeverStripped_GetsNoKitButHisQuestionsReset()
    {
        _f.State.MarkAsked(HardQuestionTopic.Kingdom, "lord1");
        _f.Captives.IsAlive("lord1").Returns(true);

        _f.Strip.OnReleased("lord1");

        _f.Equipment.DidNotReceiveWithAnyArgs().GiveFreshKit(default!, default!);
        Assert.IsFalse(_f.State.IsAsked(HardQuestionTopic.Kingdom, "lord1"));
    }

    [TestMethod]
    public void OnDied_ForgetsTheLordWithoutAKitOrAnEngineLookup()
    {
        _f.State.MarkStripped("lord1");
        _f.State.MarkAsked(HardQuestionTopic.Clan, "lord1");

        _f.Strip.OnDied("lord1");

        Assert.IsFalse(_f.State.IsStripped("lord1"));
        Assert.IsFalse(_f.State.IsAsked(HardQuestionTopic.Clan, "lord1"));
        _f.Equipment.DidNotReceiveWithAnyArgs().GiveFreshKit(default!, default!);
        _f.Captives.DidNotReceiveWithAnyArgs().IsAlive(default!);
    }
}
