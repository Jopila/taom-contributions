using System;
using System.Collections.Generic;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.MinorClanMigration;
using TAOM.Features.MinorClanMigration.Hooks;
using TAOM.Tests.Infrastructure;
using Arg = NSubstitute.Arg;

namespace TAOM.Tests.Features.MinorClanMigration;

/// <summary>Wiring for the minor-clan load migration: the module line, its empty save footprint, the container graph, the handler's guard.</summary>
[TestClass]
public class MinorClanMigrationWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<MinorClanMigrationModule>().Count());

    [TestMethod]
    public void TheModule_IsNotParked_OwnsNoSaveData_DeclaresOneBehaviorOnly()
    {
        var module = new MinorClanMigrationModule();

        Assert.AreEqual("MinorClanMigration", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "the engine saves the clan fields; the behavior persists nothing");
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void TheModulesServices_ResolveFromARealContainer()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        new MinorClanMigrationModule().RegisterServices(container);

        Assert.IsInstanceOfType(container.Resolve<IMinorClanMigrationAdapter>(), typeof(MinorClanMigrationAdapter));
        Assert.AreSame(container.Resolve<MinorClanMigrationService>(), container.Resolve<MinorClanMigrationService>());
    }

    [TestMethod]
    public void IoC_DoesNotRegisterTheFeatureByHand()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true) + RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("MinorClanMigration"), "the module registers and adds everything");
    }

    [TestMethod]
    public void TheBehavior_ListensOnlyToTheGameLoadedEvent()
    {
        var src = RepoPaths.ReadSource("Main/Features/MinorClanMigration/Hooks/MinorClanMigrationBehavior.cs", stripComments: true);

        Assert.IsTrue(src.Contains("CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener"));
        Assert.IsFalse(src.Contains("OnNewGameCreatedEvent"), "a new campaign already carries the shipped data");
        Assert.IsFalse(src.Contains("OnSessionLaunchedEvent"));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void OnGameLoaded_AFailingMigration_IsLoggedAndNeverThrows()
    {
        var adapter = Substitute.For<IMinorClanMigrationAdapter>();
        adapter.Snapshot(Arg.Any<IReadOnlyCollection<string>>()).Throws(new InvalidOperationException("boom"));
        var logger = Substitute.For<IModLogger>();
        var sut = new MinorClanMigrationBehavior(new MinorClanMigrationService(adapter, logger), logger);

        sut.OnGameLoaded(null!);

        logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("boom")));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void OnGameLoaded_RunsTheMigration()
    {
        var adapter = Substitute.For<IMinorClanMigrationAdapter>();
        adapter.Snapshot(Arg.Any<IReadOnlyCollection<string>>()).Returns(new List<MinorClanFacts>());
        var logger = Substitute.For<IModLogger>();
        var sut = new MinorClanMigrationBehavior(new MinorClanMigrationService(adapter, logger), logger);

        sut.OnGameLoaded(null!);

        adapter.Received(1).Snapshot(Arg.Any<IReadOnlyCollection<string>>());
    }
}
