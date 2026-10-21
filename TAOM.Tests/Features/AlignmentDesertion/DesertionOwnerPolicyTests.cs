using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.AlignmentDesertion;

/// <summary>
/// Under co-op a remote player's clan and any owner the registry cannot classify lose no troops to
/// alignment desertion; the local player follows "Apply To Player", the AI follows "Apply To AI".
/// Engine-free, so it runs without the game.
/// </summary>
[TestClass]
public class DesertionOwnerPolicyTests
{
    [DataTestMethod]
    [DataRow(PlayerControl.Unknown, false, true)]    // registry unreadable: skip
    [DataRow(PlayerControl.Unknown, true, true)]     // even for the local clan's slot
    [DataRow(PlayerControl.Player, false, true)]     // a remote co-op player's clan: skip
    [DataRow(PlayerControl.Player, true, false)]     // this peer's own clan: continue as the player
    [DataRow(PlayerControl.NotPlayer, false, false)] // an AI clan: continue as AI
    public void ShouldSkip_TruthTable(PlayerControl owner, bool isLocalPlayerClan, bool expected)
    {
        Assert.AreEqual(expected, DesertionOwnerPolicy.ShouldSkip(owner, isLocalPlayerClan));
    }

    [TestMethod]
    public void IsPlayerOwned_OnlyAPlayerOwner()
    {
        Assert.IsTrue(DesertionOwnerPolicy.IsPlayerOwned(PlayerControl.Player));
        Assert.IsFalse(DesertionOwnerPolicy.IsPlayerOwned(PlayerControl.NotPlayer));
    }
}
