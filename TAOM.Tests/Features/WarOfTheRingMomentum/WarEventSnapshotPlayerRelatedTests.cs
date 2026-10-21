using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

/// <summary>
/// Under co-op the War of the Ring meter is shared, so any player's party or kingdom counts as the
/// player's side. Unknown (registry unreadable) is not involved, so a stranger's wars cannot move it.
/// </summary>
[TestClass]
public class WarEventSnapshotPlayerRelatedTests
{
    [DataTestMethod]
    [DataRow(true, PlayerControl.NotPlayer, false, true)]    // the local main party
    [DataRow(false, PlayerControl.Player, false, true)]      // a remote player's party
    [DataRow(false, PlayerControl.NotPlayer, true, true)]    // a lord of any player's kingdom
    [DataRow(false, PlayerControl.Unknown, false, false)]    // registry unreadable: not involved
    [DataRow(false, PlayerControl.NotPlayer, false, false)]  // an unrelated AI party
    [DataRow(false, PlayerControl.Unknown, true, true)]      // Unknown never cancels a kingdom match
    public void IsPlayerRelated_TruthTable(bool isMain, PlayerControl control, bool sameKingdom, bool expected)
    {
        Assert.AreEqual(expected, WarEventSnapshotAdapter.IsPlayerRelated(isMain, control, sameKingdom));
    }

    private static string ReadAdapter()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        var path = Path.Combine(dir?.FullName ?? throw new FileNotFoundException("TAOM.sln"),
            "Main", "Adapters", "WarEventSnapshotAdapter.cs");
        return File.ReadAllText(path);
    }

    [TestMethod]
    public void FromMapEvent_ScansBothSidesForAPlayerParty()
    {
        var src = ReadAdapter();

        Assert.IsTrue(src.Contains("HasPlayerParty(mapEvent.AttackerSide)"), "attacker side is not scanned");
        Assert.IsTrue(src.Contains("HasPlayerParty(mapEvent.DefenderSide)"), "defender side is not scanned");
    }
}
