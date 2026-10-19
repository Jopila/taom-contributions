using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Clan;
using TAOM.Features.ShadowfaxScreens.Clan.Hooks;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TAOM.Tests.Features.LoadTimeStamps;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.ShadowfaxScreens.Clan;

/// <summary>
/// The three Clan fast open hooks against real Harmony 2.4.2 on dummy classes of vanilla's shape: a RefreshValues that calls
/// RefreshPartiesList, and an OnActivate that builds the VM. The hooks take <c>object __instance</c> so the dummy binds.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ClanFastOpenHookTests
{
    private const string HarmonyId = "taom.tests.clanfastopen.hooks";

    private static bool _gameLoaded;

    private IShadowfaxScreensSettingsProvider _settings = null!;
    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;
    private ClanScreenFastPathService _service = null!;
    private Harmony _harmony = null!;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private sealed class DummyParties
    {
        public int Builds;
        public int Refreshes;
        public bool ThrowInBuild;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void RefreshValues()
        {
            Refreshes++;
            RefreshPartiesList();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void RefreshPartiesList()
        {
            if (ThrowInBuild) throw new InvalidOperationException("build failed");
            Builds++;
        }
    }

    private sealed class DummyScreen
    {
        public DummyParties Parties = new DummyParties();

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void OnActivate()
        {
            Parties.RefreshValues();
            Parties.RefreshPartiesList();
        }
    }

    private static void ThrowingFinalizer() => throw new InvalidOperationException("a finalizer after the fast path");

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        _settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        _settings.ClanFastOpen.Returns(true);
        _settings.LogScreenOpenTimes.Returns(true);
        var coop = Substitute.For<ICoopPresenceProvider>();
        _clock = new FakeStampClock { Frequency = 10000, Now = 5000 };
        _logger = new RecordingLogger();
        _service = new ClanScreenFastPathService(_settings, coop, _clock);
        ClanFastOpenCalls.Initialize(_service);
        ScreenOpenTiming.Initialize(new ScreenOpenTimer(_clock, _logger, _settings));
        _harmony = new Harmony(HarmonyId);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _harmony?.UnpatchAll(HarmonyId);
        ClanFastOpenCalls.Initialize(null);
        ScreenOpenTiming.Initialize(null);
    }

    private static HarmonyMethod Fix(Type hook, string name) => new HarmonyMethod(hook.GetMethod(name));

    private void PatchRefreshValues(bool prefix = true) => _harmony.Patch(
        typeof(DummyParties).GetMethod(nameof(DummyParties.RefreshValues)),
        prefix: prefix ? Fix(typeof(ClanPartiesVM_RefreshValues_ClanFastOpen_Patch), "Prefix") : null,
        finalizer: Fix(typeof(ClanPartiesVM_RefreshValues_ClanFastOpen_Patch), "Finalizer"));

    private void PatchRefreshPartiesList() => _harmony.Patch(
        typeof(DummyParties).GetMethod(nameof(DummyParties.RefreshPartiesList)),
        prefix: Fix(typeof(ClanPartiesVM_RefreshPartiesList_ClanFastOpen_Patch), "Prefix"),
        finalizer: Fix(typeof(ClanPartiesVM_RefreshPartiesList_ClanFastOpen_Patch), "Finalizer"));

    private void PatchOnActivate(bool prefix = true) => _harmony.Patch(
        typeof(DummyScreen).GetMethod(nameof(DummyScreen.OnActivate)),
        prefix: prefix ? Fix(typeof(GauntletClanScreen_OnActivate_ClanFastOpen_Patch), "Prefix") : null,
        finalizer: Fix(typeof(GauntletClanScreen_OnActivate_ClanFastOpen_Patch), "Finalizer"));

    [TestMethod]
    public void InnerBuildInsideRefreshValues_IsSkipped_ADirectCallStillBuilds()
    {
        PatchRefreshValues();
        PatchRefreshPartiesList();
        var parties = new DummyParties();

        parties.RefreshValues();
        Assert.AreEqual(0, parties.Builds, "the build inside RefreshValues is skipped");

        parties.RefreshPartiesList();
        Assert.AreEqual(1, parties.Builds, "a direct call builds");
    }

    [TestMethod]
    public void ToggleOff_EveryBuildRuns()
    {
        _settings.ClanFastOpen.Returns(false);
        PatchRefreshValues();
        PatchRefreshPartiesList();
        var parties = new DummyParties();

        parties.RefreshValues();

        Assert.AreEqual(1, parties.Builds);
    }

    [TestMethod]
    public void LaterFinalizerThatThrows_StillRestoresTheOwner()
    {
        PatchRefreshValues();
        PatchRefreshPartiesList();
        _harmony.Patch(typeof(DummyParties).GetMethod(nameof(DummyParties.RefreshValues)),
            finalizer: new HarmonyMethod(typeof(ClanFastOpenHookTests)
                .GetMethod(nameof(ThrowingFinalizer), BindingFlags.NonPublic | BindingFlags.Static)) { priority = Priority.Last });
        var parties = new DummyParties();

        Assert.ThrowsException<InvalidOperationException>(() => parties.RefreshValues());

        parties.RefreshPartiesList();
        Assert.AreEqual(1, parties.Builds, "the owner was restored, so a direct build runs");
    }

    [TestMethod]
    public void PrefixStripped_TheFinalizerAloneChangesNothing()
    {
        PatchRefreshValues(prefix: false);
        PatchRefreshPartiesList();
        var parties = new DummyParties();

        parties.RefreshValues();

        Assert.AreEqual(1, parties.Builds, "no token, so nothing skipped");
        Assert.AreEqual(1, parties.Refreshes);
    }

    [TestMethod]
    public void DirectBuildThatThrows_PropagatesAsInVanilla_AndLeavesNoOwner()
    {
        PatchRefreshValues();
        PatchRefreshPartiesList();
        var parties = new DummyParties { ThrowInBuild = true };
        var other = new DummyParties();

        // The inner build is skipped, so no throw happens inside RefreshValues; a direct call throws as vanilla does.
        parties.RefreshValues();
        Assert.ThrowsException<InvalidOperationException>(() => parties.RefreshPartiesList());

        other.RefreshPartiesList();
        Assert.AreEqual(1, other.Builds);
    }

    [TestMethod]
    public void OnActivate_WritesOneOpenLine_WithTheBuildCounts()
    {
        PatchRefreshValues();
        PatchRefreshPartiesList();
        PatchOnActivate();

        new DummyScreen().OnActivate();

        var line = _logger.Lines.Single(l => l.Contains("[ScreenOpen]"));
        StringAssert.Contains(line, "[ScreenOpen] Clan open: ");
        StringAssert.EndsWith(line, "(fast open on; list builds: 1 run, 1 skipped, 0.0 ms)");
    }

    [TestMethod]
    public void OnActivate_TimerOff_WritesNothing_AndOpensNoCountWindow()
    {
        _settings.LogScreenOpenTimes.Returns(false);
        PatchRefreshValues();
        PatchRefreshPartiesList();
        PatchOnActivate();

        new DummyScreen().OnActivate();

        Assert.AreEqual(0, _logger.Lines.Count);
        Assert.AreEqual("", _service.EndOpen(), "no window was opened");
    }

    [TestMethod]
    public void OnActivate_FinalizerWithoutItsPrefix_WritesNothing()
    {
        PatchOnActivate(prefix: false);

        new DummyScreen().OnActivate();

        Assert.AreEqual(0, _logger.Lines.Count);
    }
}
