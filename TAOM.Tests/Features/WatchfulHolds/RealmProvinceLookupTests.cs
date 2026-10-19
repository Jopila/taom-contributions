using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.RealmBorders;
using TAOM.Features.WatchfulHolds;
using TAOM.Tests.Features.RealmBorders;

namespace TAOM.Tests.Features.WatchfulHolds;

[TestClass]
public class RealmProvinceLookupTests
{
    private static IRealmBordersSettings Borders(bool enabled)
    {
        var settings = Substitute.For<IRealmBordersSettings>();
        settings.Enabled.Returns(enabled);
        return settings;
    }

    private static RealmProvinceLookup Ready(bool bordersOn = true) => new RealmProvinceLookup(
        RealmTerritoryServiceTests.ReadyTerritory(
            new RealmTerritoryServiceTests.FakeTerrain(),
            RealmTerritoryServiceTests.MapWith(
                RealmTerritoryServiceTests.Town("west", 100, 100),
                RealmTerritoryServiceTests.Town("east", 300, 100))),
        Borders(bordersOn));

    [TestMethod]
    public void TryGetProvinceFief_ProvincesReady_NamesTheFortWhoseProvinceHoldsThePoint()
    {
        var sut = Ready();

        Assert.IsTrue(sut.TryGetProvinceFief(50f, 100f, out var west));
        Assert.IsTrue(sut.TryGetProvinceFief(350f, 100f, out var east));

        Assert.AreEqual("west", west);
        Assert.AreEqual("east", east);
    }

    [TestMethod]
    public void TryGetProvinceFief_ReadyTerritoryButBordersDisabled_HasNoAnswer()
    {
        var sut = Ready(bordersOn: false);

        Assert.IsFalse(sut.TryGetProvinceFief(50f, 100f, out var fief), "Realm Borders is off: distance only");
        Assert.AreEqual(string.Empty, fief);
    }

    [TestMethod]
    public void TryGetProvinceFief_PointOutsideTheMap_AnswersWithNoProvince()
    {
        var sut = Ready();

        Assert.IsTrue(sut.TryGetProvinceFief(-500f, -500f, out var fief), "the provinces exist, the point is in none");
        Assert.AreEqual(string.Empty, fief);
    }

    [TestMethod]
    public void TryGetProvinceFief_NanPoint_AnswersWithNoProvince()
    {
        var sut = Ready();

        Assert.IsTrue(sut.TryGetProvinceFief(float.NaN, float.NaN, out var fief));
        Assert.AreEqual(string.Empty, fief);
    }

    [TestMethod]
    public void TryGetProvinceFief_NotComputedYet_HasNoAnswer()
    {
        var territory = new RealmTerritoryService(
            new RealmTerritoryServiceTests.FakeTerrain(),
            RealmTerritoryServiceTests.MapWith(RealmTerritoryServiceTests.Town("west", 100, 100)),
            new RealmTerritoryServiceTests.SyncWorker(),
            Substitute.For<IModLogger>());
        var sut = new RealmProvinceLookup(territory, Borders(true));

        Assert.IsFalse(sut.TryGetProvinceFief(50f, 100f, out var fief), "Idle");
        Assert.AreEqual(string.Empty, fief);

        territory.Begin(new TAOM.Features.RealmBorders.Domain.PartitionSettings());
        Assert.IsFalse(sut.TryGetProvinceFief(50f, 100f, out _), "Sampling");
    }

    [TestMethod]
    public void TryGetProvinceFief_PartitionFailed_HasNoAnswer()
    {
        var territory = new RealmTerritoryService(
            new RealmTerritoryServiceTests.FakeTerrain { HasBounds = false },
            RealmTerritoryServiceTests.MapWith(RealmTerritoryServiceTests.Town("west", 100, 100)),
            new RealmTerritoryServiceTests.SyncWorker(),
            Substitute.For<IModLogger>());
        territory.Begin(new TAOM.Features.RealmBorders.Domain.PartitionSettings());

        Assert.AreEqual(TerritoryState.Failed, territory.State);
        Assert.IsFalse(new RealmProvinceLookup(territory, Borders(true)).TryGetProvinceFief(50f, 100f, out _), "distance only when the partition failed");
    }
}
