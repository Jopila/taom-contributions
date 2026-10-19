using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.TaomBuildings;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.TaomBuildings;

[TestClass]
public class TaomBuildingSeedingServiceTests
{
    private static TaomBuildingSeedingService Service(
        ITownBuildingsAdapter? towns = null, IModLogger? logger = null, TaomBuildingDefinition? definition = null) =>
        new(new TaomBuildingCatalog(new[] { definition ?? TaomBuildingFixtures.Wall() }),
            towns ?? Substitute.For<ITownBuildingsAdapter>(), logger ?? Substitute.For<IModLogger>());

    [TestMethod]
    public void AddsCultureVariant_WhenFamilyAbsent()
    {
        var plan = Service().Plan(new[]
        {
            TaomBuildingFixtures.Fief("a", "lindon", "building_settlement_barracks"),
            TaomBuildingFixtures.Fief("b", "mordor"),
        });

        CollectionAssert.AreEqual(
            new[] { ("a", TaomBuildingFixtures.ElfId), ("b", TaomBuildingFixtures.DefaultId) },
            plan.Select(p => (p.SettlementId, p.VariantId)).ToArray());
    }

    [TestMethod]
    public void Skips_WhenAnyFamilyMemberPresent()
    {
        // A culture change after the building was seeded must not add a second family member.
        var plan = Service().Plan(new[]
        {
            TaomBuildingFixtures.Fief("a", "lindon", TaomBuildingFixtures.DefaultId),
            TaomBuildingFixtures.Fief("b", "mordor", TaomBuildingFixtures.ElfId),
        });

        Assert.AreEqual(0, plan.Count);
    }

    [TestMethod]
    public void Plan_VariantIdNull_Skips()
    {
        // The family has no default and lists only lindon: a mordor fief has no variant, so nothing is planned.
        var plan = Service(definition: TaomBuildingFixtures.Restricted()).Plan(new[] { TaomBuildingFixtures.Fief("a", "mordor") });

        Assert.AreEqual(0, plan.Count);
    }

    [TestMethod]
    public void Apply_OnCoopClient_ReadsNoFiefs()
    {
        var towns = Substitute.For<ITownBuildingsAdapter>();

        Service(towns).Apply(authority: false);

        towns.DidNotReceive().Fortifications();
        towns.DidNotReceiveWithAnyArgs().Add(default!, default!);
    }

    [TestMethod]
    public void Apply_AddsEachPlannedBuilding_ThroughTheAdapter()
    {
        var towns = Substitute.For<ITownBuildingsAdapter>();
        towns.Fortifications().Returns(new[] { TaomBuildingFixtures.Fief("a", "lindon") });

        Service(towns).Apply(authority: true);

        towns.Received(1).Add("a", TaomBuildingFixtures.ElfId);
    }

    [TestMethod]
    public void Apply_OneFiefThrows_AddsTheOthers_AndWarnsOnce()
    {
        // A throw out of an OnGameLoaded handler loops the loading screen, so seeding fails open per fief.
        var towns = Substitute.For<ITownBuildingsAdapter>();
        var logger = Substitute.For<IModLogger>();
        towns.Fortifications().Returns(new[]
        {
            TaomBuildingFixtures.Fief("a", "lindon"),
            TaomBuildingFixtures.Fief("b", "lindon"),
            TaomBuildingFixtures.Fief("c", "lindon"),
        });
        towns.When(t => t.Add("b", Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));

        Service(towns, logger).Apply(authority: true);

        towns.Received(1).Add("a", TaomBuildingFixtures.ElfId);
        towns.Received(1).Add("c", TaomBuildingFixtures.ElfId);
        logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("1")));
        logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Apply_ReadingTheFiefsThrows_DoesNotThrow_AndWarnsOnce()
    {
        var towns = Substitute.For<ITownBuildingsAdapter>();
        var logger = Substitute.For<IModLogger>();
        towns.Fortifications().Throws(new InvalidOperationException("no campaign"));

        Service(towns, logger).Apply(authority: true);

        logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void TheBehavior_GatesOnAuthority_NotOnTheSaveDataPolicy()
    {
        // Adding a building mutates shared world state: ICoopSessionProvider.IsAuthority is the gate for that.
        // MayWriteSaveBackedState is for TAOM's own SyncData keys.
        var source = RepoPaths.ReadSource("Main/Features/TaomBuildings/Hooks/TaomBuildingSeedingBehavior.cs", stripComments: true);

        StringAssert.Contains(source, "_coop.IsAuthority");
        Assert.IsFalse(source.Contains("MayWriteSaveBackedState"));
    }
}
