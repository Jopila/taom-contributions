using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TAOM.Features.ShadowfaxScreens.Quests;

namespace TAOM.Tests.Features.ShadowfaxScreens.Quests;

/// <summary>How ShadowfaxScreensModule wires the Quest log's service: registration, the facade, the install at game init.</summary>
[TestClass]
public class QuestLogModuleWiringTests
{
    private Container _container = null!;
    private IQuestJournalAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private readonly ShadowfaxScreensModule _sut = new ShadowfaxScreensModule();

    [TestInitialize]
    public void Setup()
    {
        _container = new Container();
        _container.RegisterInstance(Substitute.For<IStampClock>());
        _logger = Substitute.For<IModLogger>();
        _container.RegisterInstance(_logger);
        _sut.RegisterServices(_container);
        _adapter = Substitute.For<IQuestJournalAdapter>();
        _container.RegisterInstance(_adapter, IfAlreadyRegistered.Replace);
    }

    [TestCleanup]
    public void Cleanup()
    {
        QuestLogDeferCalls.Initialize(null);
        ScreenOpenTiming.Initialize(null);
        _container.Dispose();
    }

    [TestMethod]
    public void RegisterServices_AdapterAndServiceResolveAsSingletons()
    {
        var fresh = new Container();
        fresh.RegisterInstance(Substitute.For<IStampClock>());
        fresh.RegisterInstance(Substitute.For<IModLogger>());
        _sut.RegisterServices(fresh);

        Assert.IsInstanceOfType(fresh.Resolve<IQuestJournalAdapter>(), typeof(QuestJournalAdapter));
        Assert.AreSame(fresh.Resolve<QuestLogDeferService>(), fresh.Resolve<QuestLogDeferService>());
        fresh.Dispose();
    }

    [TestMethod]
    public void InitializeStatics_WiresTheFacadeToTheService()
    {
        _sut.InitializeStatics(_container);

        Assert.AreSame(_container.Resolve<QuestLogDeferService>(), QuestLogDeferCalls.Service);
    }

    [TestMethod]
    public void OnPhase_GameInit_BindsTheAdapter()
    {
        _sut.OnPhase(ApplyPhase.GameInit, _container);

        _adapter.Received(1).Bind();
    }

    [TestMethod]
    public void OnPhase_OtherPhases_DoNotBind()
    {
        _sut.OnPhase(ApplyPhase.ProcessLoad, _container);
        _sut.OnPhase(ApplyPhase.MainMenu, _container);
        _sut.OnPhase(ApplyPhase.FirstMission, _container);

        _adapter.DidNotReceive().Bind();
    }

    [TestMethod]
    public void TheFacadesCategory_IsOneTheModuleDeclares()
    {
        Assert.IsTrue(_sut.PatchCategories.Any(d => d.Category == QuestLogDeferCalls.Category && d.Phase == ApplyPhase.GameInit));
    }
}
