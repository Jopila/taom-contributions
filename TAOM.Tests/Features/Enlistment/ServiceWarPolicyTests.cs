using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Field report 5. The centre of gravity here is the DISCHARGE direction: ServeAsSoldier peaces out
/// of every war the player holds, which turns enlistment into a free universal peace button, and
/// these tests are what stop TAOM inheriting that.
/// </summary>
[TestClass]
public class ServiceWarPolicyTests
{
    private static List<string> L(params string[] ids) => new List<string>(ids);

    // ---- declaring ----

    [TestMethod]
    public void MirrorsTheCommandersEnemies()
    {
        var result = ServiceWarPolicy.FactionsToDeclareWarOn(L("empire", "sturgia"), L());

        CollectionAssert.AreEqual(L("empire", "sturgia"), result);
    }

    [TestMethod]
    public void SkipsWarsAlreadyHeld()
    {
        // Re-declaring an existing war is not harmless — DeclareWarAction fires campaign events and
        // resets diplomatic state that other systems read.
        var result = ServiceWarPolicy.FactionsToDeclareWarOn(L("empire", "sturgia"), L("empire"));

        CollectionAssert.AreEqual(L("sturgia"), result);
    }

    [TestMethod]
    public void PeacefulCommander_NothingToDeclare()
        => Assert.AreEqual(0, ServiceWarPolicy.FactionsToDeclareWarOn(L(), L("empire")).Count);

    [TestMethod]
    public void DuplicatesAndBlanksAreDropped()
        => CollectionAssert.AreEqual(
            L("empire"),
            ServiceWarPolicy.FactionsToDeclareWarOn(L("empire", "empire", "", null), L()));

    [TestMethod]
    public void NullInputsAreSafe()
        => Assert.AreEqual(0, ServiceWarPolicy.FactionsToDeclareWarOn(null, null).Count);

    // ---- discharging: the SAS bug ----

    [TestMethod]
    public void UnwindsOnlyTheWarsTheServiceCreated()
    {
        var result = ServiceWarPolicy.FactionsToPeaceOnDischarge(
            mirroredWars: L("empire", "sturgia"), enemiesAtOath: L());

        CollectionAssert.AreEqual(L("empire", "sturgia"), result);
    }

    [TestMethod]
    public void AWarThePlayerBroughtWithHimSurvivesDischarge()
    {
        // THE regression this policy exists for. SAS peaces out of everything, so a player at war
        // with the Empire before enlisting could launder that away with a single term of service.
        var result = ServiceWarPolicy.FactionsToPeaceOnDischarge(
            mirroredWars: L("empire", "sturgia"), enemiesAtOath: L("empire"));

        CollectionAssert.AreEqual(L("sturgia"), result);
    }

    [TestMethod]
    public void AWarDeclaredDuringServiceIsNotUnwoundEither()
        // Subtraction, not intersection: only what the mirror created comes back off. A war the
        // player picked himself mid-service is his to keep, exactly like one he arrived with — it
        // simply never enters the mirror.
        => CollectionAssert.AreEqual(
            L("sturgia"),
            ServiceWarPolicy.FactionsToPeaceOnDischarge(
                mirroredWars: L("sturgia"), enemiesAtOath: L()));

    [TestMethod]
    public void EveryEnemyPreDated_NothingToUnwind()
        => Assert.AreEqual(0, ServiceWarPolicy.FactionsToPeaceOnDischarge(
            L("empire"), L("empire", "sturgia")).Count);

    [TestMethod]
    public void NullMirror_NothingToUnwind()
        => Assert.AreEqual(0, ServiceWarPolicy.FactionsToPeaceOnDischarge(null, L("empire")).Count);

    // ---- mid-term diff (#832) ----

    [TestMethod]
    public void Diff_NewCommanderWar_IsDeclared()
    {
        var diff = ServiceWarPolicy.Diff(L("mordor"), L(), L());
        CollectionAssert.AreEqual(L("mordor"), diff.ToDeclare.ToList());
        Assert.AreEqual(0, diff.ToPeace.Count);
    }

    [TestMethod]
    public void Diff_StillAtWar_NothingToDo()
    {
        var diff = ServiceWarPolicy.Diff(L("mordor"), L(), L("mordor"));
        Assert.AreEqual(0, diff.ToDeclare.Count);
        Assert.AreEqual(0, diff.ToPeace.Count);
    }

    [TestMethod]
    public void Diff_EndedWar_IsPeaced()
    {
        var diff = ServiceWarPolicy.Diff(L(), L(), L("mordor"));
        CollectionAssert.AreEqual(L("mordor"), diff.ToPeace.ToList());
        Assert.AreEqual(0, diff.ToDeclare.Count);
    }

    [TestMethod]
    public void Diff_OathEnemiesAreNeverDeclaredNorPeaced()
    {
        // The player brought "isengard" with him. A commander war on it is not ours to declare, and a
        // stale mirror entry for it is not ours to peace.
        var diff = ServiceWarPolicy.Diff(L("isengard"), L("isengard"), L());
        Assert.AreEqual(0, diff.ToDeclare.Count);
        var stale = ServiceWarPolicy.Diff(L(), L("isengard"), L("isengard"));
        Assert.AreEqual(0, stale.ToPeace.Count);
    }

    [TestMethod]
    public void Diff_DedupesAndToleratesNulls()
    {
        var diff = ServiceWarPolicy.Diff(L("mordor", "mordor", null, ""), null, null);
        CollectionAssert.AreEqual(L("mordor"), diff.ToDeclare.ToList());
        var empty = ServiceWarPolicy.Diff(null, null, null);
        Assert.AreEqual(0, empty.ToDeclare.Count);
        Assert.AreEqual(0, empty.ToPeace.Count);
    }

    // ---- who may be mirrored mid-term (#832) ----

    [DataTestMethod]
    [DataRow("clan_1", "clan_1", "clan_1", true, DisplayName = "own clan, nobody else: mirror")]
    [DataRow("", "clan_1", "clan_1", false, DisplayName = "empty pin: no")]
    [DataRow(null, "clan_1", "clan_1", false, DisplayName = "null pin: no")]
    [DataRow("kingdom_1", "kingdom_1", "clan_1", false, DisplayName = "vassal: no")]
    [DataRow("clan_1", "kingdom_1", "clan_1", false, DisplayName = "identity changed: no")]
    [DataRow("clan_1", "", "clan_1", false, DisplayName = "no current faction: no")]
    public void ShouldMirrorMidTerm_OnlyForThePlayersOwnClanUnderTheOathIdentity(
        string pin, string current, string clan, bool expected)
        => Assert.AreEqual(expected, ServiceWarPolicy.ShouldMirrorMidTerm(pin, current, clan));
}
