using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.ManyHammers;

namespace TAOM.Tests.Features.ManyHammers;

[TestClass]
public class ManyHammersServiceTests
{
    private IManyHammersSettingsProvider _settings = null!;
    private ICoopSessionProvider _coop = null!;
    private IModLogger _logger = null!;
    private ManyHammersService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IManyHammersSettingsProvider>();
        _settings.BulkEnabled.Returns(true);
        _settings.HideLockedEnabled.Returns(true);
        _coop = Substitute.For<ICoopSessionProvider>();
        _logger = Substitute.For<IModLogger>();
        _service = new ManyHammersService(_settings, _coop, _logger);
    }

    private static int[] Slots(int iron1, int charcoal)
    {
        var s = new int[UnitSnapshot.Slots];
        s[1] = iron1;
        s[8] = charcoal;
        return s;
    }

    [TestMethod]
    public void BulkActive_SettingOnAndNoCoopDeferral_IsTrue() => Assert.IsTrue(_service.BulkActive);

    [TestMethod]
    public void BulkActive_SettingOff_IsFalse()
    {
        _settings.BulkEnabled.Returns(false);

        Assert.IsFalse(_service.BulkActive);
    }

    [TestMethod]
    public void BulkActive_ACoopGuest_IsFalse()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.IsFalse(_service.BulkActive);
    }

    [TestMethod]
    public void DesiredCount_ACoopGuestHoldingCtrl_IsOne()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.AreEqual(1, _service.DesiredCount(shift: false, ctrl: true, stack: 30, selectionLocked: false));
    }

    [TestMethod]
    public void DesiredCount_ALockedSelection_IsOne()
    {
        // Vanilla asks "Are you sure?" for a locked weapon; bulk must not run extra units behind that question.
        Assert.AreEqual(1, _service.DesiredCount(shift: true, ctrl: false, stack: 30, selectionLocked: true));
        Assert.AreEqual(1, _service.DesiredCount(shift: false, ctrl: true, stack: 30, selectionLocked: true));
    }

    [TestMethod]
    public void DesiredCount_BulkSwitchedOff_IsOne()
    {
        _settings.BulkEnabled.Returns(false);

        Assert.AreEqual(1, _service.DesiredCount(shift: true, ctrl: true, stack: 30, selectionLocked: false));
    }

    [TestMethod]
    public void DesiredCount_FollowsTheModifiers()
    {
        Assert.AreEqual(1, _service.DesiredCount(false, false, 30, false));
        Assert.AreEqual(5, _service.DesiredCount(true, false, 30, false));
        Assert.AreEqual(30, _service.DesiredCount(true, true, 30, false));
    }

    [TestMethod]
    public void HidesLockedItems_FollowsTheSetting()
    {
        Assert.IsTrue(_service.HidesLockedItems);

        _settings.HideLockedEnabled.Returns(false);

        Assert.IsFalse(_service.HidesLockedItems);
    }

    [TestMethod]
    public void HidesLockedItems_ACoopGuestStillHides()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.IsTrue(_service.HidesLockedItems);
    }

    [TestMethod]
    public void RunExtras_LeavesExactlyOneUnitForVanilla()
    {
        var unit = new FakeUnit { Stamina = 500, Cost = 1, Charcoal = 99, Available = 99 };

        var done = _service.RunExtras(unit, desired: 5);

        Assert.AreEqual(4, done);
        Assert.AreEqual(4, unit.Calls);
    }

    [TestMethod]
    public void RunExtras_StopsWithTheStamina_ReservingVanillasLastUnit()
    {
        // Stamina 40 at cost 10 fits three units (40, 30, 20); two run here, the third is vanilla's.
        var unit = new FakeUnit { Stamina = 40, Cost = 10, Charcoal = 99, Available = 99 };

        Assert.AreEqual(2, _service.RunExtras(unit, desired: 50));
        Assert.AreEqual(20, unit.Stamina, "the unit left for vanilla still has 20 stamina to spend");
    }

    [TestMethod]
    public void RunExtras_StopsWithTheCharcoal()
    {
        var unit = new FakeUnit { Stamina = 500, Cost = 1, Charcoal = 3, Available = 99 };

        Assert.AreEqual(2, _service.RunExtras(unit, desired: 50));
        Assert.AreEqual(1, unit.Charcoal);
    }

    [TestMethod]
    public void RunExtras_StopsWithTheStack()
    {
        var unit = new FakeUnit { Stamina = 500, Cost = 1, Charcoal = 99, Available = 3 };

        Assert.AreEqual(2, _service.RunExtras(unit, desired: 50));
        Assert.AreEqual(1, unit.Available);
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(0)]
    [DataRow(-5)]
    public void RunExtras_NothingBeyondTheFirstUnitAskedFor_MakesNoCalls(int desired)
    {
        var unit = new FakeUnit { Stamina = 500, Cost = 1, Charcoal = 99, Available = 99 };

        Assert.AreEqual(0, _service.RunExtras(unit, desired));
        Assert.AreEqual(0, unit.Calls);
    }

    [TestMethod]
    public void RunExtras_OnlyOneUnitFits_MakesNoCalls()
    {
        var unit = new FakeUnit { Stamina = 500, Cost = 1, Charcoal = 1, Available = 99 };

        Assert.AreEqual(0, _service.RunExtras(unit, desired: 50));
        Assert.AreEqual(0, unit.Calls);
    }

    [TestMethod]
    public void RunExtras_ALiveStaminaThatDrainsFasterThanTheModelSaid_StopsEarly()
    {
        // The model reads 10 a unit, so 100 stamina plans nine units and eight extras; each call really costs 25 (a perk
        // or a level changed the price), so the live re-plan sees too little left and stops after four.
        var unit = new FakeUnit { Stamina = 100, Cost = 10, DrainPerCall = 25, Charcoal = 99, Available = 99 };

        var done = _service.RunExtras(unit, desired: 50);

        Assert.AreEqual(4, done);
        Assert.AreEqual(0, unit.Stamina);
    }

    [TestMethod]
    public void RunExtras_ASnapshotThatNeverMoves_IsBoundedByTheOpeningPlan()
    {
        // The co-op guest shape: DoOne changes nothing the next Read can see.
        var unit = new FakeUnit { Stamina = 500, Cost = 1, Charcoal = 99, Available = 99, Frozen = true };

        Assert.AreEqual(49, _service.RunExtras(unit, desired: 50));
        Assert.AreEqual(49, unit.Calls);
    }

    [TestMethod]
    public void RunExtras_ASnapshotThatNeverMovesAndAnUnboundedAsk_StopsAtTheCap()
    {
        var unit = new FakeUnit { Stamina = 500, Cost = 0, Charcoal = int.MaxValue, Available = int.MaxValue, Frozen = true };

        Assert.AreEqual(BulkCraftRules.MaxUnits - 1, _service.RunExtras(unit, desired: int.MaxValue));
    }

    [TestMethod]
    public void RunExtras_ADoOneThatThrows_StopsLogsAndDoesNotRethrow()
    {
        var unit = new FakeUnit { Stamina = 500, Cost = 1, Charcoal = 99, Available = 99, ThrowOnCall = 3 };

        var done = _service.RunExtras(unit, desired: 10);

        Assert.AreEqual(2, done, "the two calls that finished are counted, the third threw");
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("ManyHammers") && m.Contains("boom")));
    }

    [TestMethod]
    public void RunExtras_AReadThatThrows_IsLoggedAndReportsNone()
    {
        var unit = new FakeUnit { ThrowOnRead = true };

        Assert.AreEqual(0, _service.RunExtras(unit, desired: 10));
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("ManyHammers")));
    }

    [TestMethod]
    public void RunExtras_ANullUnit_IsNone()
    {
        Assert.AreEqual(0, _service.RunExtras(null!, desired: 10));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void RunExtras_ALoggerThatThrows_DoesNotEscape()
    {
        _logger.When(l => l.LogError(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log down"));
        var unit = new FakeUnit { Stamina = 500, Cost = 1, Charcoal = 99, Available = 99, ThrowOnCall = 1 };

        Assert.AreEqual(0, _service.RunExtras(unit, desired: 10));
    }

    /// <summary>A smelting bench in memory: one weapon gives one Iron1 and takes one charcoal.</summary>
    private sealed class FakeUnit : ICraftBenchUnit
    {
        public int Stamina;
        public int Cost;
        public int DrainPerCall = -1;
        public int Charcoal;
        public int Available;
        public bool Frozen;
        public int ThrowOnCall;
        public bool ThrowOnRead;
        public int Calls;

        public UnitSnapshot Read()
        {
            if (ThrowOnRead) throw new InvalidOperationException("read failed");
            return new UnitSnapshot(Stamina, Cost, Slots(1, -1), Slots(0, Charcoal), Available);
        }

        public void DoOne()
        {
            Calls++;
            if (ThrowOnCall > 0 && Calls == ThrowOnCall) throw new InvalidOperationException("boom");
            if (Frozen) return;
            Stamina = Math.Max(0, Stamina - (DrainPerCall >= 0 ? DrainPerCall : Cost));
            Charcoal--;
            Available--;
        }
    }
}
