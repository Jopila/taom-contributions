using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Features.UnderTheEaves;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.UnderTheEaves;

/// <summary>
/// The contributor is the boundary of the sight seam: six gates, cheapest first. The main-party branches need a campaign
/// (<c>IsMainParty</c> reads <c>Campaign.Current</c>), so a unit test can pin every way the answer is zero without one and the ORDER of the
/// gates in the source; the factor itself is the rule's (UnderTheEavesRuleTests) and the sight in and out of a wood, a settlement
/// and with Keen Sight is the in-game check in docs/features/under-the-eaves.md.
/// </summary>
[TestClass]
public class ForestSightContributorTests
{
    private const string SourcePath = "Main/Features/UnderTheEaves/ForestSightContributor.cs";

    private IUnderTheEavesSettings _settings = null!;
    private IMapTerrainAdapter _terrain = null!;
    private ForestSightContributor _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _settings = Substitute.For<IUnderTheEavesSettings>();
        _settings.Enabled.Returns(true);
        _terrain = Substitute.For<IMapTerrainAdapter>();
        _terrain.IsInForest(Arg.Any<MobileParty>()).Returns(true);
        _sut = new ForestSightContributor(_settings, _terrain);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ToggleOff_GivesZero_AndAsksNoTerrain()
    {
        _settings.Enabled.Returns(false);
        // A bare party: its IsMainParty would throw without a campaign, so only the toggle's short circuit can answer.
        var party = (MobileParty)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MobileParty));

        Assert.AreEqual(0f, _sut.GetSpottingRangeBonusFactor(party));
        _terrain.DidNotReceiveWithAnyArgs().IsInForest(null!);
    }

    [TestMethod]
    public void NullParty_GivesZero_AndAsksNoTerrain()
    {
        Assert.AreEqual(0f, _sut.GetSpottingRangeBonusFactor(null!));
        _terrain.DidNotReceiveWithAnyArgs().IsInForest(null!);
    }

    [TestMethod]
    public void CapBonus_IsZero_ForAnyParty() =>
        Assert.AreEqual(0f, _sut.GetSpottingRangeCapBonus(null!), "a penalty never widens the engine's per-frame search");

    [TestMethod]
    public void TheBonusMember_IsExactlyTheTwoCheapReturns_ThenTheThreeFacts_ThenTheRule()
    {
        // The main-party branches need a campaign, so the decision is pinned as source, whitespace-normalised and comment-stripped:
        // the toggle, the null and main-party returns, then the three facts (each a cheaper read than the next, the perk only in forest)
        // and the rule's truth table (UnderTheEavesRuleTests) decides. A flipped polarity or a dropped fact changes this text.
        var src = RepoPaths.ReadSource(SourcePath, stripComments: true);
        var body = src.Substring(src.IndexOf("GetSpottingRangeBonusFactor", StringComparison.Ordinal));
        body = body.Substring(0, body.IndexOf("GetSpottingRangeCapBonus", StringComparison.Ordinal));
        var normalised = System.Text.RegularExpressions.Regex.Replace(body, @"\s+", " ");

        StringAssert.Contains(normalised,
            "{ if (!_settings.Enabled) return 0f; if (party == null || !party.IsMainParty) return 0f; "
            + "bool inSettlement = party.CurrentSettlement != null; "
            + "bool inForest = !inSettlement && _terrain.IsInForest(party); "
            + "bool keenSight = inForest && party.HasPerk(DefaultPerks.Scouting.KeenSight, out _); "
            + "return UnderTheEavesRule.Factor(inForest, inSettlement, keenSight); }");
        Assert.IsFalse(body.Contains("IoC.Resolve") || body.Contains(".Select(") || body.Contains(".Where(") || body.Contains("new "),
            "no service locator, LINQ or allocation on the per-query path");
    }
}
