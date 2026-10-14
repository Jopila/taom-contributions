using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ArmyTargeting;

namespace TAOM.Tests.Features.ArmyTargeting;

/// <summary>
/// The War Chronicle's army focus (#765, M4): during a siege window the attackers' Besieger armies and the
/// defender's Defender armies lean toward the target. The registry is read on the AI scoring path, so every
/// entry is validated when it is written and the read is a plain array scan.
/// </summary>
[TestClass]
public class ArmyTargetFocusRegistryTests
{
    private static ArmyTargetFocus Hornburg(float multiplier = 1.4f) =>
        new ArmyTargetFocus("town_V2", new[] { "isengard", "empire" }, "vlandia", multiplier);

    private static ArmyTargetFocusRegistry With(params ArmyTargetFocus[] focuses)
    {
        var registry = new ArmyTargetFocusRegistry();
        registry.Replace(focuses);
        return registry;
    }

    [TestMethod]
    public void GetMultiplier_EmptyRegistry_ReturnsOne()
    {
        var sut = new ArmyTargetFocusRegistry();

        Assert.IsTrue(sut.IsEmpty);
        Assert.AreEqual(1f, sut.GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", "isengard"));
    }

    [DataTestMethod]
    [DataRow("isengard")]
    [DataRow("empire")]
    public void GetMultiplier_BesiegerOfAnAttacker_ReturnsTheFocus(string faction)
    {
        Assert.AreEqual(1.4f, With(Hornburg()).GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", faction), 0.0001f);
    }

    [DataTestMethod]
    [DataRow("vlandia")]
    [DataRow("empire_w")]
    public void GetMultiplier_BesiegerOfANonAttacker_ReturnsOne(string faction)
    {
        Assert.AreEqual(1f, With(Hornburg()).GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", faction));
    }

    [TestMethod]
    public void GetMultiplier_DefenderOfTheDefender_ReturnsTheFocus()
    {
        Assert.AreEqual(1.4f, With(Hornburg()).GetMultiplier(ArmyTargetingMission.Defender, "town_V2", "vlandia"), 0.0001f);
    }

    [TestMethod]
    public void GetMultiplier_DefenderOfAnAttacker_ReturnsOne()
    {
        Assert.AreEqual(1f, With(Hornburg()).GetMultiplier(ArmyTargetingMission.Defender, "town_V2", "isengard"));
    }

    [DataTestMethod]
    [DataRow(ArmyTargetingMission.Raider)]
    [DataRow(ArmyTargetingMission.Other)]
    public void GetMultiplier_OtherMissions_ReturnOne(ArmyTargetingMission mission)
    {
        Assert.AreEqual(1f, With(Hornburg()).GetMultiplier(mission, "town_V2", "isengard"));
        Assert.AreEqual(1f, With(Hornburg()).GetMultiplier(mission, "town_V2", "vlandia"));
    }

    [TestMethod]
    public void GetMultiplier_AnotherSettlement_ReturnsOne()
    {
        Assert.AreEqual(1f, With(Hornburg()).GetMultiplier(ArmyTargetingMission.Besieger, "town_V1", "isengard"));
    }

    [TestMethod]
    public void GetMultiplier_NullIds_ReturnOne()
    {
        var sut = With(Hornburg());

        Assert.AreEqual(1f, sut.GetMultiplier(ArmyTargetingMission.Besieger, null, "isengard"));
        Assert.AreEqual(1f, sut.GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", null));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(1f)]
    [DataRow(0.5f)]
    [DataRow(-2f)]
    public void Replace_MultiplierNotAboveOneOrNotFinite_SkipsTheEntry(float multiplier)
    {
        var sut = With(Hornburg(multiplier));

        Assert.IsTrue(sut.IsEmpty, "a focus that could lower, keep or poison a score is never stored");
        Assert.AreEqual(1f, sut.GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", "isengard"));
    }

    [TestMethod]
    public void Replace_MultiplierAboveTheCap_IsClampedToTheCap()
    {
        var sut = With(Hornburg(9f));

        Assert.AreEqual(ArmyTargetFocusRegistry.MaxMultiplier,
            sut.GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", "isengard"), 0.0001f);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void Replace_BlankSettlementId_SkipsTheEntry(string settlementId)
    {
        Assert.IsTrue(With(new ArmyTargetFocus(settlementId, new[] { "isengard" }, "vlandia", 1.4f)).IsEmpty);
    }

    [TestMethod]
    public void Replace_NoAttackerAndNoDefender_SkipsTheEntry()
    {
        Assert.IsTrue(With(new ArmyTargetFocus("town_V2", new string[0], null, 1.4f)).IsEmpty);
    }

    [TestMethod]
    public void Replace_NullEntryOrNullList_IsIgnored()
    {
        var sut = new ArmyTargetFocusRegistry();
        sut.Replace(new ArmyTargetFocus[] { null! , Hornburg() });
        Assert.AreEqual(1, sut.Count);

        sut.Replace(null!);
        Assert.IsTrue(sut.IsEmpty);
    }

    [TestMethod]
    public void Replace_SwapsTheWholeSet()
    {
        var sut = With(Hornburg());

        sut.Replace(new[] { new ArmyTargetFocus("town_EW1", new[] { "empire_s" }, "empire_w", 1.4f) });

        Assert.AreEqual(1f, sut.GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", "isengard"));
        Assert.AreEqual(1.4f, sut.GetMultiplier(ArmyTargetingMission.Besieger, "town_EW1", "empire_s"), 0.0001f);
    }

    [TestMethod]
    public void Replace_CallerMutatesItsAttackerListAfterwards_RegistryIsUnchanged()
    {
        var attackers = new List<string> { "isengard" };
        var sut = With(new ArmyTargetFocus("town_V2", attackers, "vlandia", 1.4f));

        attackers[0] = "gundabad";

        Assert.AreEqual(1.4f, sut.GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", "isengard"), 0.0001f);
        Assert.AreEqual(1f, sut.GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", "gundabad"));
    }

    [TestMethod]
    public void Clear_EmptiesTheRegistry()
    {
        var sut = With(Hornburg());

        sut.Clear();

        Assert.IsTrue(sut.IsEmpty);
        Assert.AreEqual(1f, sut.GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", "isengard"));
    }

    [TestMethod]
    public void ResetForNewSession_DropsAnEarlierCampaignsFocus()
    {
        var sut = With(Hornburg());

        sut.ResetForNewSession();

        Assert.IsTrue(sut.IsEmpty);
    }

    [TestMethod]
    public void GetMultiplier_TwoFocusesOnOneTarget_ReturnsTheLarger()
    {
        var sut = With(Hornburg(1.2f), Hornburg(1.8f));

        Assert.AreEqual(1.8f, sut.GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", "isengard"), 0.0001f);
    }

    [TestMethod]
    public void Snapshot_ListsTheStoredFocuses()
    {
        var snapshot = With(Hornburg()).Snapshot();

        Assert.AreEqual(1, snapshot.Count);
        Assert.AreEqual("town_V2", snapshot[0].SettlementId);
        CollectionAssert.AreEqual(new[] { "isengard", "empire" }, new List<string>(snapshot[0].AttackerFactionIds));
    }
}
