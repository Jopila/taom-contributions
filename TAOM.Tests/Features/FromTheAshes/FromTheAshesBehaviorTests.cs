using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Execution;
using TAOM.Features.FromTheAshes;
using TAOM.Features.FromTheAshes.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FromTheAshes;

/// <summary>
/// The behavior is built fresh for every campaign start, before any event and before a load's SyncData, so its
/// constructor is the reset of the process-lifetime store. The save round trip goes through the faithful engine data
/// store into a FRESH behavior, so a dropped key cannot hide behind the old instance. The start is saved as a double of
/// hours, so the round trip is exact and needs no CampaignTime.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FromTheAshesBehaviorTests
{
    private IModLogger _logger = null!;
    private AshesJobStore _store = null!;
    private FromTheAshesService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new AshesJobStore(_logger);
        _service = new FromTheAshesService(Substitute.For<IFromTheAshesConfigProvider>(), Substitute.For<IFromTheAshesSettingsProvider>(),
            Substitute.For<IAshesWorldAdapter>(), Substitute.For<IEnlistmentStateQuery>(), Substitute.For<ICoopSessionProvider>(),
            Substitute.For<IDedicatedServerProvider>(), Substitute.For<IAlignmentService>(), Substitute.For<IHeroSkillXpAdapter>(), _store, _logger);
    }

    private FromTheAshesBehavior NewBehavior() => new(_service, _store, Substitute.For<IGameMenuAdapter>(), Substitute.For<IEncounterAdapter>(), _logger);

    [TestMethod]
    public void NewBehavior_StartsEmpty()
    {
        _store.Set(new AshesJob { SettlementId = "from_the_last_campaign", TotalHours = 24f });

        NewBehavior();

        Assert.IsNull(_store.Current);
    }

    [TestMethod]
    public void SaveThenLoad_IntoAFreshBehavior_RestoresTheJob()
    {
        var a = NewBehavior();
        _store.Set(new AshesJob { Kind = AshesJobKind.ProjectHelp, SettlementId = "town_A1", StartHours = 5000.25, TotalHours = 20f, HoursCredited = 6 });
        var data = new EngineDataStore { Loading = false };
        a.SyncData(data);

        data.Loading = true;
        var b = NewBehavior();
        Assert.IsNull(_store.Current, "precondition: the fresh behavior reset the store");
        b.SyncData(data);

        var job = _store.Current!;
        Assert.AreEqual(AshesJobKind.ProjectHelp, job.Kind);
        Assert.AreEqual("town_A1", job.SettlementId);
        Assert.AreEqual(5000.25, job.StartHours);
        Assert.AreEqual(20f, job.TotalHours);
        Assert.AreEqual(6, job.HoursCredited);
    }

    [TestMethod]
    public void SaveWithoutAJob_LoadsWithoutOne_AndWarnsOfNothing()
    {
        var a = NewBehavior();
        var data = new EngineDataStore { Loading = false };
        a.SyncData(data);

        data.Loading = true;
        NewBehavior().SyncData(data);

        Assert.IsNull(_store.Current);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void LoadOfASaveWithoutTheKeys_LoadsNoJob()
    {
        NewBehavior().SyncData(new EngineDataStore { Loading = true });

        Assert.IsNull(_store.Current);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Save_DoesNotChangeTheLiveJob()
    {
        var job = new AshesJob { SettlementId = "village_A1", StartHours = 12.5, TotalHours = 24f };
        var a = NewBehavior();
        _store.Set(job);

        a.SyncData(new EngineDataStore { Loading = false });

        Assert.AreSame(job, _store.Current);
    }
}
