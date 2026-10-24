using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SettlementServices;

/// <summary>
/// Source pins for the engine side of Watch-Silver and Tidings of the Lists (#859). The adapter needs a running campaign, so what
/// matters about each engine call is pinned in the comment-stripped source (docs/features/settlement-services.md).
/// </summary>
[TestClass]
public class SettlementServicesAdapterPinTests
{
    private static string Adapter() => RepoPaths.ReadSource("Main/Adapters/SettlementServicesAdapter.cs", stripComments: true);

    private static string Method(string src, string signature)
    {
        var start = src.IndexOf(signature, System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, signature + " not found");
        var next = src.IndexOf("\n    public ", start + signature.Length, System.StringComparison.Ordinal);
        return next < 0 ? src.Substring(start) : src.Substring(start, next - start);
    }

    [TestMethod]
    public void ChargePlayer_ChecksTheGoldBeforeGiving_ToNobody_AndReturnsBeforeMinusAfter()
    {
        var body = Method(Adapter(), "public int ChargePlayer(");

        var check = body.IndexOf("hero.Gold < amount", System.StringComparison.Ordinal);
        var give = body.IndexOf("GiveGoldAction.ApplyBetweenCharacters(hero, null, amount)", System.StringComparison.Ordinal);
        Assert.IsTrue(check >= 0 && give > check, "gold is checked (gold >= amount) before the debit");
        StringAssert.Contains(body, "var before = hero.Gold;");
        StringAssert.Contains(body, "return before - hero.Gold;");
        Assert.AreEqual(1, Regex.Matches(Adapter(), @"ApplyBetweenCharacters\(hero, null,").Count, "one debit in the feature");
    }

    [TestMethod]
    public void Refund_GivesBackSilently_FromNobody()
        => StringAssert.Contains(Method(Adapter(), "public void Refund("), "GiveGoldAction.ApplyBetweenCharacters(null, hero, amount, disableNotification: true)");

    [TestMethod]
    public void SetLoyalty_AssignsAnAbsoluteValue_AndReadsItBack()
    {
        var body = Method(Adapter(), "public float SetLoyalty(");

        StringAssert.Contains(body, "town.Loyalty = value;");
        StringAssert.Contains(body, "return town.Loyalty;");
        Assert.IsFalse(Regex.IsMatch(body, @"Loyalty\s*[+\-*]="), "an absolute write, never a delta");
    }

    [TestMethod]
    public void ReadTournamentSites_WalksTheTownsOnly_AndMeasuresTheLandDistance()
    {
        var body = Method(Adapter(), "public IReadOnlyList<TournamentSiteSnapshot> ReadTournamentSites(");

        StringAssert.Contains(body, "Town.AllTowns");
        Assert.IsFalse(body.Contains("AllFiefs"), "tournaments are created for towns only");
        StringAssert.Contains(body, "MobileParty.NavigationType.Default");
        StringAssert.Contains(body, "manager.GetTournamentGame(town)");
        Assert.IsTrue(Regex.IsMatch(body, @"GetTournamentGame\(town\) == null\)\s*continue;[\s\S]*GetDistance\("), "a distance is measured only for a town that holds a tournament");
    }

    [TestMethod]
    public void TheCutoff_IsTwiceTheMapDiagonal_AndDisguiseReadsTheCampaign()
    {
        var src = Adapter();

        StringAssert.Contains(src, "Campaign.MapDiagonal * 2f");
        StringAssert.Contains(src, "Campaign.Current?.IsMainHeroDisguised == true");
    }

    [TestMethod]
    public void ReadGovernedFief_MapsTheEligibilityInOrder_OwnerClanThenGovernorThenPlayerPresence()
    {
        var body = Method(Adapter(), "public GovernedFiefSnapshot? ReadGovernedFief(");

        StringAssert.Contains(body, "Hero.OneToOneConversationHero");
        StringAssert.Contains(body, "hero?.GovernorOf");
        Assert.IsTrue(Regex.IsMatch(body,
            @"new GovernedFiefSnapshot\(\s*settlement\.StringId,\s*town\.OwnerClan == Clan\.PlayerClan,\s*hero\.CurrentSettlement == settlement,\s*Settlement\.CurrentSettlement == settlement,\s*town\.Loyalty,\s*settlement\.Militia\)"),
            "owner clan is the player's, the governor stands in the fief, the player stands in it, then the two numbers");
    }

    [TestMethod]
    public void TheRealmIsTheMapFaction_NotTheOwnerClansKingdom()
    {
        var src = Adapter();

        StringAssert.Contains(src, "town.MapFaction");
        StringAssert.Contains(src, "faction.IsAtWarWith(playerFaction)");
        Assert.IsFalse(src.Contains("OwnerClan.Kingdom"));
    }

    [TestMethod]
    public void TheFeature_NeverWritesWhatItMustNotWrite()
    {
        // Not used: Settlement.HasVisited and VisualTrackerManager are saved, synced state with no removal story; InRebelliousState
        // is recomputed by the engine's daily tick; a village is not a fief the feature touches.
        foreach (var file in new[] { Adapter() }.Concat(FeatureSources()))
        {
            Assert.IsFalse(file.Contains(".Village"), ".Village");
            Assert.IsFalse(file.Contains("HasVisited"), "HasVisited");
            Assert.IsFalse(file.Contains("VisualTrackerManager"), "VisualTrackerManager");
            Assert.IsFalse(file.Contains("InRebelliousState"), "InRebelliousState");
        }
    }

    private static System.Collections.Generic.IEnumerable<string> FeatureSources()
    {
        foreach (var file in Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "SettlementServices"), "*.cs", SearchOption.AllDirectories))
            yield return RepoPaths.StripComments(File.ReadAllText(file));
    }
}
