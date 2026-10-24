using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.KeepWardens;

/// <summary>
/// The adapter (Keep Wardens boundary, #888) needs engine cultures and buildings that a no-game test cannot build,
/// so most of it is a source pin; the null-party answer needs no engine object.
/// </summary>
[TestClass]
public class CastlePatrolAdapterTests
{
    private static string Source() => RepoPaths.ReadSource("Main/Adapters/CastlePatrolAdapter.cs", stripComments: true);

    [TestMethod]
    public void CastlePatrolGuardHouseLevel_ANullParty_IsTheSharedNotACastlePatrolConstant() =>
        Assert.AreEqual(CastlePatrolSnapshot.NotACastlePatrol, new CastlePatrolAdapter().CastlePatrolGuardHouseLevel(null));

    [TestMethod]
    public void TemplateFor_MapsStrength1To3_ToWeakModerateStrong()
    {
        var src = Source();
        var start = src.IndexOf("TemplateFor(Settlement settlement, int strength)", System.StringComparison.Ordinal);
        var body = src.Substring(start, src.IndexOf("CastlePatrolGuardHouseLevel", start, System.StringComparison.Ordinal) - start);

        var weak = body.IndexOf("strength == 1 ? culture?.SettlementPatrolPartyTemplateWeak", System.StringComparison.Ordinal);
        var moderate = body.IndexOf("strength == 2 ? culture?.SettlementPatrolPartyTemplateModerate", System.StringComparison.Ordinal);
        var strong = body.IndexOf("strength == 3 ? culture?.SettlementPatrolPartyTemplateStrong", System.StringComparison.Ordinal);

        Assert.IsTrue(weak >= 0 && weak < moderate && moderate < strong, "1 is weak, 2 is moderate, 3 is strong");
    }

    [TestMethod]
    public void Read_DerivesTheThreeTemplateFlags_FromTemplateFor_AndReadsTheCastleGuardHouse()
    {
        var src = Source();

        foreach (var strength in new[] { 1, 2, 3 })
            StringAssert.Contains(src, $"TemplateFor(settlement, {strength}) != null");
        StringAssert.Contains(src, "DefaultBuildingTypes.CastleGuardHouse");
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(src, @"DefaultBuildingTypes\.").Count,
            "the castle Guard House is the only building read; the town one is what vanilla reads");
    }
}
