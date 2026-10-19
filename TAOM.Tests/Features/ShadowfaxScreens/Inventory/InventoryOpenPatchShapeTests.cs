using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Engine.GauntletUI;
using TAOM.Adapters;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TAOM.Features.ShadowfaxScreens.Inventory;
using TAOM.Features.ShadowfaxScreens.Inventory.Hooks;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ShadowfaxScreens.Inventory;

/// <summary>
/// The OnReady bracket and the InitializeInventory phase, applied with real Harmony around stand-ins that carry the
/// engine's parameter and field names. A stripped prefix leaves the finalizer a no-op, a throw still ends the
/// window and reaches the caller with its throw site, and a repeat OnReady (layer already built) is not an open.
/// RequiresGame because the hook classes' attributes and field type name engine types.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class InventoryOpenPatchShapeTests
{
    private const string HarmonyId = "taom.tests.shadowfaxinventory";

    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;
    private InventoryOpenFastPathService _service = null!;

    public sealed class FakeScreen
    {
        public GauntletLayer? _gauntletLayer;
        public bool Throws;

        public void OnReady()
        {
            if (Throws) Thrower();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Thrower() => throw new InvalidOperationException("movie failed");
    }

    public sealed class FakeViewModel
    {
        public static Action? Work;

        public void InitializeInventory() => Work?.Invoke();
    }

    [TestInitialize]
    public void Setup()
    {
        _clock = new FakeStampClock();
        _logger = new RecordingLogger();
        var settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        settings.LogScreenOpenTimes.Returns(true);
        settings.InventoryFastOpen.Returns(true);
        _service = new InventoryOpenFastPathService(settings, _clock, _logger);
        var adapter = Substitute.For<IInventoryOpenAdapter>();
        adapter.TryRead(out Arg.Any<string>(), out Arg.Any<int>(), out Arg.Any<int>()).Returns(call =>
        {
            call[0] = "trade";
            call[1] = 312;
            call[2] = 88;
            return true;
        });
        ScreenOpenTiming.Initialize(new ScreenOpenTimer(_clock, _logger, settings));
        ShadowfaxInventoryCalls.Initialize(_service, adapter, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        new Harmony(HarmonyId).UnpatchAll(HarmonyId);
        FakeViewModel.Work = null;
        ScreenOpenTiming.Initialize(null);
        ShadowfaxInventoryCalls.Initialize(null, null, null);
    }

    private static void PatchOnReady(bool withPrefix)
    {
        new Harmony(HarmonyId).Patch(AccessTools.Method(typeof(FakeScreen), nameof(FakeScreen.OnReady)),
            prefix: withPrefix ? new HarmonyMethod(typeof(GauntletInventoryScreen_OnReady_ShadowfaxPatch).GetMethod("Prefix")) : null,
            finalizer: new HarmonyMethod(typeof(GauntletInventoryScreen_OnReady_ShadowfaxPatch).GetMethod("Finalizer")));
    }

    [TestMethod]
    public void OnReady_FirstCall_LogsOneOpenLineWithModeCountsAndRows()
    {
        PatchOnReady(withPrefix: true);

        new FakeScreen().OnReady();

        Assert.AreEqual(1, _logger.Lines.Count);
        Assert.AreEqual("INFO [ScreenOpen] Inventory open: 0.0 ms (mode=trade, left=312, right=88, fast=on, rows=0.0 ms, sorts skipped=0, concepts cached=0/0)",
            _logger.Lines.Single());
    }

    [TestMethod]
    public void OnReady_WithAnExistingLayer_IsNotAnOpen()
    {
        PatchOnReady(withPrefix: true);
        var screen = new FakeScreen { _gauntletLayer = (GauntletLayer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(GauntletLayer)) };

        screen.OnReady();

        Assert.AreEqual(0, _logger.Lines.Count);
        Assert.IsFalse(_service.OpenActive);
    }

    [TestMethod]
    public void OnReady_Throws_StillEndsTheWindow_AndRethrowsWithTheThrowSite()
    {
        PatchOnReady(withPrefix: true);

        var thrown = Assert.ThrowsException<InvalidOperationException>(() => new FakeScreen { Throws = true }.OnReady());

        Assert.AreEqual("movie failed", thrown.Message);
        StringAssert.Contains(thrown.StackTrace, "Thrower");
        StringAssert.Contains(_logger.Lines.Single(), "Inventory open: 0.0 ms, threw InvalidOperationException (mode=trade");
        Assert.IsFalse(_service.OpenActive);
    }

    [TestMethod]
    public void OnReady_WithThePrefixStripped_TheFinalizerDoesNothing()
    {
        PatchOnReady(withPrefix: false);

        new FakeScreen().OnReady();
        Assert.ThrowsException<InvalidOperationException>(() => new FakeScreen { Throws = true }.OnReady());

        Assert.AreEqual(0, _logger.Lines.Count);
    }

    [TestMethod]
    public void OnReady_TimerOff_LogsNothing_AndTheServiceStaysClosed()
    {
        var off = Substitute.For<IShadowfaxScreensSettingsProvider>();
        off.LogScreenOpenTimes.Returns(false);
        ScreenOpenTiming.Initialize(new ScreenOpenTimer(_clock, _logger, off));
        PatchOnReady(withPrefix: true);

        new FakeScreen().OnReady();

        Assert.AreEqual(0, _logger.Lines.Count);
        Assert.IsFalse(_service.OpenActive);
    }

    [TestMethod]
    public void RowsPhase_InsideAnOpen_IsReportedOnTheOpenLine_AndOutsideOneIsNot()
    {
        var harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(FakeViewModel), nameof(FakeViewModel.InitializeInventory)),
            prefix: new HarmonyMethod(typeof(SPInventoryVM_InitializeInventory_ShadowfaxPatch).GetMethod("Prefix")),
            finalizer: new HarmonyMethod(typeof(SPInventoryVM_InitializeInventory_ShadowfaxPatch).GetMethod("Finalizer")));

        new FakeViewModel().InitializeInventory();
        Assert.AreEqual(0, _logger.Lines.Count, "A Reset outside an open logs nothing.");

        var window = ShadowfaxInventoryCalls.BeginOpen();
        FakeViewModel.Work = () => _clock.Advance(7);
        new FakeViewModel().InitializeInventory();
        ShadowfaxInventoryCalls.EndOpen(window, null);

        Assert.AreEqual(1, _logger.Lines.Count);
        StringAssert.Contains(_logger.Lines.Single(), "Inventory open: 7.0 ms");
        StringAssert.Contains(_logger.Lines.Single(), "rows=7.0 ms");
    }
}
