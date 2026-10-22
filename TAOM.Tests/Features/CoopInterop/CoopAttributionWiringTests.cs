using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// Source pins for the co-op attribution call sites (#966 Batch B). The decisions are unit-tested where
/// they are pure; these pin that each caller still asks the right question and treats an unreadable
/// registry (Unknown) the way the design says.
/// </summary>
[TestClass]
public class CoopAttributionWiringTests
{
    private const string SpecResAdapter = "Main/Adapters/SpecialResourceEventAdapter.cs";
    private const string WarAdapter = "Main/Adapters/WarEventSnapshotAdapter.cs";
    private const string Desertion = "Main/Features/AlignmentDesertion/Hooks/AlignmentDesertionBehavior.cs";
    private const string Recruits = "Main/Features/SpecialResources/SpecialResourceEarningService.cs";
    private const string EnlistDialog = "Main/Features/Enlistment/Hooks/EnlistmentDialogBehavior.cs";
    private const string CampQuery = "Main/Features/FieldCamp/UI/MapScreenCampMenuActivationQuery.cs";

    [TestMethod]
    public void WarEventSnapshot_KingdomClause_CountsOnlyAKnownPlayerClan()
    {
        var src = ReadSource(WarAdapter, stripComments: true);

        StringAssert.Matches(src, new Regex(@"_playerControl\.OfClan\(clan\)\s*==\s*PlayerControl\.Player"));
    }

    [TestMethod]
    public void WarEventSnapshot_SideScan_CountsOnlyAKnownPlayerParty()
    {
        var src = ReadSource(WarAdapter, stripComments: true);

        StringAssert.Matches(src, new Regex(@"_playerControl\.OfParty\(mapEventParty\?\.Party\?\.MobileParty\)\s*==\s*PlayerControl\.Player"));
    }

    [TestMethod]
    public void WarEventSnapshot_NeverTreatsUnknownAsAPlayer()
    {
        var src = ReadSource(WarAdapter, stripComments: true);

        Assert.IsFalse(src.Contains("!= PlayerControl.NotPlayer"), "an Unknown owner would count as a player");
    }

    [TestMethod]
    public void AlignmentDesertion_AsksTheRegistryForTheClanAndAppliesTheCoopSkip()
    {
        var src = ReadSource(Desertion, stripComments: true);

        StringAssert.Contains(src, "_playerControl.OfClan(");
        StringAssert.Contains(src, "DesertionOwnerPolicy.ShouldSkip(");
    }

    [TestMethod]
    public void AlignmentDesertion_ResolvesTheOwnerInExactlyTheTwoHandlers()
    {
        var src = ReadSource(Desertion, stripComments: true);

        // The party and garrison handlers; the declaration (bool TryResolveOwner) is not a call.
        var calls = Regex.Matches(src, @"(?<!bool\s)\bTryResolveOwner\(").Count;

        Assert.AreEqual(2, calls, "party and garrison handlers must each resolve the owner once");
    }

    [TestMethod]
    public void SpecialResources_RecruitPayer_NamesTheFourBoolArguments()
    {
        var src = ReadSource(Recruits, stripComments: true);

        StringAssert.Matches(src, new Regex(
            @"RecruitChargePolicy\.ChoosePayer\(\s*isAuthority:[^;]*isDedicated:[^;]*sessionActive:[^;]*hasScope:"));
    }

    [TestMethod]
    public void SpecialResourceAdapter_PlayerSide_IsNeverReadUnguarded()
    {
        // MapEvent.PlayerSide dereferences MainParty with no guard (MapEvent.cs:104), and MainParty is
        // null on a dedicated co-op server. IsPlayerMapEvent is null-safe, so it gates every read.
        var src = ReadSource(SpecResAdapter, stripComments: true);

        Assert.IsFalse(Regex.IsMatch(src, @"PlayerSide\s*=\s*mapEvent\.PlayerSide"),
            "PlayerSide must be read only behind the IsPlayerMapEvent check");
        Assert.AreEqual(2, Regex.Matches(src, @"isPlayerEvent\s*\?\s*mapEvent\.PlayerSide\s*:\s*BattleSideEnum\.None").Count,
            "both the victory and the raid or hideout snapshot guard PlayerSide");
    }

    [TestMethod]
    public void EnlistmentDialog_PassesTheCoopGuestVerdictToTheOfferGate()
    {
        var src = ReadSource(EnlistDialog, stripComments: true);

        StringAssert.Matches(src, new Regex(
            @"ShowsOfferFor\(\s*_gate\.CanEnlistWith\(PartnerId\(\)\)\s*,\s*_coopSession\.ShouldDeferToHost\s*\)"));
    }

    [TestMethod]
    public void CampMenuActivationQuery_DefersToTheCoopSessionVerdict()
    {
        var src = ReadSource(CampQuery, stripComments: true);

        StringAssert.Matches(src, new Regex(@"DefersToCoopHost\s*=>\s*_coopSession\.ShouldDeferToHost"));
    }
}
