using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Adapters;

/// <summary>
/// #849: <c>IsInForest(MobileParty)</c> reads the party's own face, the member vanilla's sight range and target ratio read. The live
/// read needs a campaign map (<c>Position.Face</c> is a computed getter), so the unit tests pin the ways it answers false and the SOURCE
/// of the read; that the face group index is the terrain type, and that vanilla reads the same member, are engine facts in
/// UnderTheEavesEngineFactsTests.
/// </summary>
[TestClass]
public class MapTerrainAdapterTests
{
    private const string SourcePath = "Main/Adapters/MapTerrainAdapter.cs";

    private static string IsInForestBody()
    {
        var src = RepoPaths.ReadSource(SourcePath, stripComments: true);
        var body = src.Substring(src.IndexOf("public bool IsInForest(", StringComparison.Ordinal));
        return body.Substring(0, body.IndexOf("public float HeightAt(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void IsInForest_NullParty_IsFalse() =>
        Assert.IsFalse(new MapTerrainAdapter().IsInForest(null!));

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void IsInForest_NoCampaign_IsFalse_WithoutReadingThePartysFace()
    {
        // A bare party: reading its CurrentNavigationFace would dereference Campaign.Current, which is null here.
        var party = (MobileParty)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MobileParty));

        Assert.IsFalse(new MapTerrainAdapter().IsInForest(party));
    }

    [TestMethod]
    public void IsInForest_ReadsThePartysOwnFace_AndComparesItsGroupWithForest_NotAFreshNativeLookup()
    {
        var body = IsInForestBody();

        StringAssert.Contains(body, "party.CurrentNavigationFace");
        StringAssert.Contains(body, "face.IsValid() && face.FaceGroupIndex == (int)TerrainType.Forest");
        Assert.IsFalse(Regex.IsMatch(body, @"new\s+CampaignVec2|GetFaceIndex\(|GetFaceTerrainType\("),
            "a new CampaignVec2 or a scene lookup would pay a native call per read that vanilla does not: " + body);
    }

    [TestMethod]
    public void IsInForest_ChecksNullParty_ThenTheCampaignMap_BeforeTouchingTheFace()
    {
        var body = IsInForestBody();

        int nullParty = body.IndexOf("party == null", StringComparison.Ordinal);
        int campaign = body.IndexOf("Campaign.Current?.MapSceneWrapper", StringComparison.Ordinal);
        int face = body.IndexOf("party.CurrentNavigationFace", StringComparison.Ordinal);

        Assert.IsTrue(nullParty >= 0 && campaign > nullParty && face > campaign,
            "CurrentNavigationFace is a computed getter that dereferences Campaign.Current: " + body);
    }
}
