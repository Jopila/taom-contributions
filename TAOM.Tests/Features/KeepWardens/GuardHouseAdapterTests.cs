using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.KeepWardens;

/// <summary>
/// The Guard House build adapter (#889) needs engine buildings that a no-game test cannot build, so it is a source pin: the
/// right building type per settlement kind, and vanilla's own eligibility filter for the next build.
/// </summary>
[TestClass]
public class GuardHouseAdapterTests
{
    private static string Source() => RepoPaths.ReadSource("Main/Adapters/GuardHouseAdapter.cs", stripComments: true);

    [TestMethod]
    public void Find_UsesTheCastleGuardHouseForACastle_AndTheSettlementGuardHouseForATown()
    {
        var src = Source();

        Assert.IsTrue(Regex.IsMatch(src, @"IsCastle\s*\?\s*DefaultBuildingTypes\.CastleGuardHouse\s*:\s*DefaultBuildingTypes\.SettlementGuardHouse"),
            "a castle reads the castle Guard House, anything else the town one");
        Assert.AreEqual(2, Regex.Matches(src, @"DefaultBuildingTypes\.").Count, "only the two Guard House types are read");
    }

    [TestMethod]
    public void Eligibility_MirrorsVanillasOwnFilterForTheNextBuilding()
    {
        // DefaultBuildingScoreCalculationModel.GetNextBuilding: !IsDailyProject && CurrentLevel < 3 && !BuildingsInProgress.Contains.
        var src = Source();

        StringAssert.Contains(src, "!building.BuildingType.IsDailyProject");
        StringAssert.Contains(src, "building.CurrentLevel < 3");
        StringAssert.Contains(src, "!town.BuildingsInProgress.Contains(building)");
    }

    [TestMethod]
    public void TheRoll_IsDrawnByTheAdapter_OnlyForAnEligibleGuardHouse()
    {
        var src = Source();
        var read = src.IndexOf("GuardHouseCandidate Read(", System.StringComparison.Ordinal);
        var eligible = src.IndexOf("IsEligible(", read, System.StringComparison.Ordinal);
        var roll = src.IndexOf("MBRandom.RandomFloat", read, System.StringComparison.Ordinal);

        Assert.IsTrue(read >= 0 && eligible > read && roll > eligible, "the eligibility check must come before the roll is drawn");
        Assert.AreEqual(1, Regex.Matches(src, @"MBRandom\.").Count, "one roll, nowhere else");
    }

    [TestMethod]
    public void TheAdapter_HasOneLookup_AndNoConditionalTwin()
    {
        var src = Source();

        StringAssert.Contains(src, "public Building? GuardHouse(Town town)");
        StringAssert.Contains(src, "var guardHouse = GuardHouse(town);");
        Assert.IsFalse(src.Contains("GuardHouseIf"), "the model decides with the service; the adapter only finds");
    }
}
