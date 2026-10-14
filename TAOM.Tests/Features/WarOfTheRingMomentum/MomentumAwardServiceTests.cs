using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Domain;
using TAOM.Features.WarOfTheRingMomentum.Models;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

/// <summary>
/// <c>IMomentumAwardService.TryAwardScripted</c> (#765, M6): the War Chronicle's momentum award. It writes a
/// <see cref="MomentumActionType.Chronicle"/> event that decays like any other, and refuses outside a
/// running war.
/// </summary>
[TestClass]
public class MomentumAwardServiceTests
{
    private MomentumStateStore _store = null!;
    private IMomentumConfigProvider _config = null!;
    private IMomentumSettingsProvider _settings = null!;
    private int _changed;

    [TestInitialize]
    public void Setup()
    {
        _store = new MomentumStateStore(Substitute.For<IModLogger>());
        _store.State.MarkWarStarted();
        _store.MomentumChanged += () => _changed++;
        _config = Substitute.For<IMomentumConfigProvider>();
        _config.GetConfig().Returns(new MomentumConfig { DurationsHours = new MomentumDurationsConfig { Chronicle = 240 } });
        _settings = Substitute.For<IMomentumSettingsProvider>();
        _settings.MomentumEnabled.Returns(true);
    }

    private MomentumAwardService CreateSut() => new MomentumAwardService(_store, _config, _settings);

    [TestMethod]
    public void TryAwardScripted_RunningWar_AddsAChronicleEventOnTheStoreScale()
    {
        Assert.IsTrue(CreateSut().TryAwardScripted(MomentumSide.Free, 50, "The Hornburg held", 1000d));

        var ev = _store.State.Free.GetEvents(MomentumActionType.Chronicle).Single();
        Assert.AreEqual(50 * MomentumWarState.MomentumScale, ev.Value);
        Assert.AreEqual(1240d, ev.EndTimeHours);
        Assert.AreEqual("The Hornburg held", ev.Description);
        Assert.AreEqual(50 * MomentumWarState.MomentumScale, _store.State.Free.SideMomentum);
        Assert.AreEqual(0, _store.State.Evil.SideMomentum);
        Assert.AreEqual(1, _changed, "the meter refreshes");
    }

    [TestMethod]
    public void TryAwardScripted_UnitsAboveTheCap_AreClamped()
    {
        Assert.IsTrue(CreateSut().TryAwardScripted(MomentumSide.Evil, 5000, "x", 0d));

        Assert.AreEqual(MomentumAwardService.MaxScriptedUnits * MomentumWarState.MomentumScale, _store.State.Evil.SideMomentum);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-20)]
    public void TryAwardScripted_UnitsBelowOne_Refuses(int units)
    {
        Assert.IsFalse(CreateSut().TryAwardScripted(MomentumSide.Free, units, "x", 0d));
        Assert.AreEqual(0, _store.State.Free.SideMomentum);
    }

    [TestMethod]
    public void TryAwardScripted_BeforeTheWarStarts_Refuses()
    {
        _store.ResetForNewGame();

        Assert.IsFalse(CreateSut().TryAwardScripted(MomentumSide.Free, 50, "x", 0d));
        Assert.AreEqual(0, _store.State.Free.GetEvents(MomentumActionType.Chronicle).Count());
        Assert.AreEqual(0, _changed);
    }

    [TestMethod]
    public void TryAwardScripted_AfterTheWarEnded_Refuses()
    {
        _store.State.MarkWarEnded(WarOutcome.FreeVictory);

        Assert.IsFalse(CreateSut().TryAwardScripted(MomentumSide.Free, 50, "x", 0d));
    }

    [TestMethod]
    public void TryAwardScripted_MomentumOff_Refuses()
    {
        _settings.MomentumEnabled.Returns(false);

        Assert.IsFalse(CreateSut().TryAwardScripted(MomentumSide.Free, 50, "x", 0d));
    }

    [DataTestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void TryAwardScripted_NonFiniteClock_Refuses(double now)
    {
        Assert.IsFalse(CreateSut().TryAwardScripted(MomentumSide.Free, 50, "x", now));
        Assert.AreEqual(0, _store.State.Free.SideMomentum);
    }

    [TestMethod]
    public void TryAwardScripted_NullDescription_IsStoredEmpty()
    {
        Assert.IsTrue(CreateSut().TryAwardScripted(MomentumSide.Free, 10, null!, 0d));

        Assert.AreEqual(string.Empty, _store.State.Free.GetEvents(MomentumActionType.Chronicle).Single().Description);
    }

    [TestMethod]
    public void ChronicleAward_SurvivesASaveRoundTrip()
    {
        CreateSut().TryAwardScripted(MomentumSide.Evil, 40, "Osgiliath fell", 100d);

        var data = _store.Serialize();
        var restored = new MomentumStateStore(Substitute.For<IModLogger>());
        restored.Deserialize(data);

        Assert.AreEqual(40 * MomentumWarState.MomentumScale, restored.State.Evil.GetEvents(MomentumActionType.Chronicle).Single().Value);
    }
}
