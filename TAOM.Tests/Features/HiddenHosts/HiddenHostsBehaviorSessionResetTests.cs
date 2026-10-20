using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.HiddenHosts;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #846: the service is a process-lifetime singleton (its per-party latch is campaign state), so the behavior, built fresh
/// for every campaign start, resets it in its constructor. The one-time hint rides the save: a dictionary-backed store
/// round-trips the flag into a FRESH behavior, so a dropped SyncData key cannot hide behind the old instance. That the
/// module builds the behavior per campaign is pinned in <see cref="HiddenHostsWiringTests"/>.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HiddenHostsBehaviorSessionResetTests
{
    private IHiddenHostsSettings _settings = null!;
    private IHiddenHostsHintAdapter _hint = null!;
    private HiddenHostsService _service = null!;

    [TestInitialize]
    public void SetUp()
    {
        _settings = Substitute.For<IHiddenHostsSettings>();
        _settings.Enabled.Returns(true);
        _hint = Substitute.For<IHiddenHostsHintAdapter>();
        _hint.IsMapFree().Returns(true);
        _service = new HiddenHostsService(_settings);
    }

    private HiddenHostsBehavior NewBehavior() =>
        new(_service, _hint, Substitute.For<IModLogger>());

    private static PartyIntelSnapshot Snap(float ratio) => new()
    {
        HasData = true, StringId = "p1", HealthyCount = 7, DistanceToMain = ratio * 100f, MainSeeingRange = 100f,
        SpottingRatio = 1f,
    };

    [TestMethod]
    public void Constructor_ResetsTheRememberedCountsToo()
    {
        _service.Evaluate(Snap(0.05f));
        Assert.IsTrue(_service.Evaluate(Snap(0.5f)).Masked);
        Assert.IsTrue(_service.MemoryDrawn, "precondition: the previous campaign remembered the host");

        NewBehavior();

        Assert.IsFalse(_service.MemoryDrawn);
        Assert.AreEqual("?", _service.Evaluate(Snap(0.5f)).CountText, "no memory of the old host");
    }

    [TestMethod]
    public void Constructor_ResetsTheLatch_ForANewCampaign()
    {
        _service.Evaluate(Snap(0.20f));
        Assert.IsFalse(_service.Evaluate(Snap(0.30f)).Masked, "precondition: the previous campaign latched the digit");

        NewBehavior();

        Assert.IsTrue(_service.Evaluate(Snap(0.30f)).Masked);
    }

    [TestMethod]
    public void Tick_FeatureOnMapFree_ShowsTheHintOnce()
    {
        var behavior = NewBehavior();

        behavior.OnTick();
        behavior.OnTick();

        _hint.Received(1).ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Tick_ShowsTheHintWithTheNameplateSymbols()
    {
        NewBehavior().OnTick();

        _hint.Received(1).ShowSymbolHint("1??", "...");
    }

    [TestMethod]
    public void Tick_IsMapFreeThrows_LogsOnce_AndDoesNotRetry()
    {
        _hint.IsMapFree().Throws(new System.InvalidOperationException("no map"));
        var logger = Substitute.For<IModLogger>();
        var behavior = new HiddenHostsBehavior(_service, _hint, logger);

        behavior.OnTick();
        behavior.OnTick();
        behavior.OnTick();

        logger.Received(1).LogError(Arg.Any<string>());
        _hint.Received(1).IsMapFree();
        _hint.DidNotReceive().ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Tick_HooksStoppedByTheFaultBudget_ShowsNothing()
    {
        HiddenHostsCalls.Initialize(null, null, Substitute.For<IModLogger>());
        try
        {
            for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
                HiddenHostsCalls.ReportFault("hook", new System.InvalidOperationException());
            var behavior = NewBehavior();

            behavior.OnTick();

            _hint.DidNotReceive().IsMapFree();
            _hint.DidNotReceive().ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());
        }
        finally
        {
            HiddenHostsCalls.Initialize(null, null);
        }
    }

    [TestMethod]
    public void Tick_FeatureOff_ShowsNothing_AndKeepsTheHintOwed()
    {
        _settings.Enabled.Returns(false);
        var behavior = NewBehavior();
        behavior.OnTick();
        _hint.DidNotReceive().ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());

        _settings.Enabled.Returns(true);
        behavior.OnTick();

        _hint.Received(1).ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Tick_MapNotFree_WaitsForTheMap()
    {
        _hint.IsMapFree().Returns(false);
        var behavior = NewBehavior();
        behavior.OnTick();
        _hint.DidNotReceive().ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());

        _hint.IsMapFree().Returns(true);
        behavior.OnTick();

        _hint.Received(1).ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void HintFlag_SaveThenLoad_IntoAFreshBehavior_RoundTrips()
    {
        var a = NewBehavior();
        a.OnTick();
        var store = new EngineDataStore { Loading = false };
        a.SyncData(store);
        _hint.ClearReceivedCalls();

        store.Loading = true;
        var b = NewBehavior();
        b.SyncData(store);
        b.OnTick();

        _hint.DidNotReceive().ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void HintFlag_SavedBeforeTheHint_LoadsAsOwed()
    {
        var a = NewBehavior();
        var store = new EngineDataStore { Loading = false };
        a.SyncData(store);

        store.Loading = true;
        var b = NewBehavior();
        b.SyncData(store);
        b.OnTick();

        _hint.Received(1).ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void HintFlag_LoadOfASaveWithoutTheKey_LoadsAsOwed()
    {
        var store = new EngineDataStore { Loading = true };
        var behavior = NewBehavior();

        behavior.SyncData(store);
        behavior.OnTick();

        _hint.Received(1).ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Tick_TheHintThrows_IsSwallowedAndLogged_AndNotRetriedEveryFrame()
    {
        _hint.When(h => h.ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>())).Do(_ => throw new System.InvalidOperationException("boom"));
        var logger = Substitute.For<IModLogger>();
        var behavior = new HiddenHostsBehavior(_service, _hint, logger);

        behavior.OnTick();
        behavior.OnTick();

        logger.Received(1).LogError(Arg.Any<string>());
        _hint.Received(1).ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());
    }

    // ---- Phase 3: the memory hint, under its own key and flag

    private void DrawMemory()
    {
        _service.Evaluate(Snap(0.05f));
        Assert.IsTrue(_service.Evaluate(Snap(0.5f)).Masked, "precondition: a remembered count was drawn");
        Assert.IsTrue(_service.MemoryDrawn);
    }

    private HiddenHostsBehavior BehaviorPastTheFirstHint()
    {
        var behavior = NewBehavior();
        behavior.OnTick();
        _hint.ClearReceivedCalls();
        return behavior;
    }

    [TestMethod]
    public void Memory_Tick_AfterTheFirstHint_AndATildeWasDrawn_ShowsTheMemoryHintOnceWithTheTildeExample()
    {
        var behavior = BehaviorPastTheFirstHint();
        DrawMemory();

        behavior.OnTick();
        behavior.OnTick();

        _hint.Received(1).ShowMemoryHint("~1??");
        _hint.DidNotReceive().ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Memory_Tick_NoTildeDrawnYet_ShowsNothing()
    {
        var behavior = BehaviorPastTheFirstHint();

        behavior.OnTick();

        _hint.DidNotReceive().ShowMemoryHint(Arg.Any<string>());
    }

    [TestMethod]
    public void Memory_Tick_NeverOnTheTickTheFirstHintShows_SoTwoPopupsDoNotStack()
    {
        var behavior = NewBehavior();
        DrawMemory();

        behavior.OnTick();
        _hint.Received(1).ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());
        _hint.DidNotReceive().ShowMemoryHint(Arg.Any<string>());

        behavior.OnTick();
        _hint.Received(1).ShowMemoryHint("~1??");
    }

    [TestMethod]
    public void Memory_Tick_MapNotFree_WaitsForTheMap()
    {
        var behavior = BehaviorPastTheFirstHint();
        DrawMemory();
        _hint.IsMapFree().Returns(false);
        behavior.OnTick();
        _hint.DidNotReceive().ShowMemoryHint(Arg.Any<string>());

        _hint.IsMapFree().Returns(true);
        behavior.OnTick();

        _hint.Received(1).ShowMemoryHint(Arg.Any<string>());
    }

    [TestMethod]
    public void Memory_Tick_FeatureOff_ShowsNothing_AndKeepsTheHintOwed()
    {
        var behavior = BehaviorPastTheFirstHint();
        DrawMemory();
        _settings.Enabled.Returns(false);
        behavior.OnTick();
        _hint.DidNotReceive().ShowMemoryHint(Arg.Any<string>());

        _settings.Enabled.Returns(true);
        behavior.OnTick();

        _hint.Received(1).ShowMemoryHint(Arg.Any<string>());
    }

    [TestMethod]
    public void Memory_Tick_HooksStoppedByTheFaultBudget_ShowsNothing()
    {
        var behavior = BehaviorPastTheFirstHint();
        DrawMemory();
        HiddenHostsCalls.Initialize(null, null, Substitute.For<IModLogger>());
        try
        {
            for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
                HiddenHostsCalls.ReportFault("hook", new System.InvalidOperationException());

            behavior.OnTick();

            _hint.DidNotReceive().ShowMemoryHint(Arg.Any<string>());
        }
        finally
        {
            HiddenHostsCalls.Initialize(null, null);
        }
    }

    [TestMethod]
    public void Memory_Flag_SaveThenLoad_IntoAFreshBehavior_RoundTrips_UnderItsOwnKey()
    {
        var a = BehaviorPastTheFirstHint();
        DrawMemory();
        a.OnTick();
        var store = new EngineDataStore { Loading = false };
        a.SyncData(store);
        Assert.IsTrue(store.Has("_taomHiddenHostsHintShown"), "phase 1's key is untouched");
        Assert.IsTrue(store.Has("_taomHiddenHostsMemoryHintShown"));
        _hint.ClearReceivedCalls();

        store.Loading = true;
        var b = NewBehavior();
        b.SyncData(store);
        DrawMemory();
        b.OnTick();

        _hint.DidNotReceive().ShowMemoryHint(Arg.Any<string>());
        _hint.DidNotReceive().ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Memory_Flag_ASaveFromPhase1_LoadsTheMemoryHintAsOwed()
    {
        var store = new EngineDataStore { Loading = true };
        store.Seed("_taomHiddenHostsHintShown", true);
        var behavior = NewBehavior();
        behavior.SyncData(store);
        DrawMemory();

        behavior.OnTick();

        _hint.DidNotReceive().ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());
        _hint.Received(1).ShowMemoryHint("~1??");
    }

    [TestMethod]
    public void Memory_Tick_TheHintThrows_IsSwallowedAndLogged_AndNotRetried()
    {
        _hint.When(h => h.ShowMemoryHint(Arg.Any<string>())).Do(_ => throw new System.InvalidOperationException("boom"));
        var logger = Substitute.For<IModLogger>();
        var behavior = new HiddenHostsBehavior(_service, _hint, logger);
        behavior.OnTick();
        DrawMemory();

        behavior.OnTick();
        behavior.OnTick();

        logger.Received(1).LogError(Arg.Any<string>());
        _hint.Received(1).ShowMemoryHint(Arg.Any<string>());
    }

    [TestMethod]
    public void Memory_Tick_IsMapFreeThrows_GivesTheMemoryHintUpForTheSession_ButNotTheFlag()
    {
        var logger = Substitute.For<IModLogger>();
        var behavior = new HiddenHostsBehavior(_service, _hint, logger);
        behavior.OnTick();
        DrawMemory();
        _hint.IsMapFree().Throws(new System.InvalidOperationException("no map"));

        behavior.OnTick();
        behavior.OnTick();

        logger.Received(1).LogError(Arg.Any<string>());
        var store = new EngineDataStore { Loading = false };
        behavior.SyncData(store);
        Assert.IsFalse(store.Get<bool>("_taomHiddenHostsMemoryHintShown"), "a throw from the map test is not saved");
    }

    [TestMethod]
    public void Memory_Tick_ThePhase1HintAbandoned_NeverShowsTheMemoryHint()
    {
        bool mapTestThrows = true;
        _hint.IsMapFree().Returns(_ => mapTestThrows ? throw new System.InvalidOperationException("no map") : true);
        var behavior = NewBehavior();
        behavior.OnTick();
        mapTestThrows = false;
        DrawMemory();

        behavior.OnTick();

        _hint.DidNotReceive().ShowMemoryHint(Arg.Any<string>());
        _hint.DidNotReceive().ShowSymbolHint(Arg.Any<string>(), Arg.Any<string>());
    }
}
