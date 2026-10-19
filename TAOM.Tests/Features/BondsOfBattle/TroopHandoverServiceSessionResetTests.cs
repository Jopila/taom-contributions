using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.BondsOfBattle;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.BondsOfBattle;

/// <summary>
/// The hand-over service is a process singleton holding one piece of per-campaign state, the day's landed ledger, so a second
/// campaign in one process, or a load in the middle of a day, must start with it empty (csharp-architecture.md, "Singleton
/// Services Holding Per-Campaign State"). Nothing here is saved, so there is no load path to restore.
/// </summary>
[TestClass]
public class TroopHandoverServiceSessionResetTests
{
    private IRelationGrantService _grants = null!;
    private TroopHandoverService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _grants = Substitute.For<IRelationGrantService>();
        _grants.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(call => call.ArgAt<int>(1));
        var settings = Substitute.For<IBondsOfBattleSettingsProvider>();
        settings.Get().Returns(new BondsOfBattleSettings(true, 1, false, 5, true, 25f, true, 15));
        _sut = new TroopHandoverService(_grants, settings, Substitute.For<IEnlistmentStateQuery>(),
            Substitute.For<IRandomSource>(), Substitute.For<IBondsNoticeAdapter>(), Substitute.For<ICoopSessionProvider>());
    }

    private void FillTheDay() => _sut.OnHandover("lord_1", "Lord One", 20, false, false, null);

    [TestMethod]
    public void ResetForNewSession_EmptiesTheDaysLedger()
    {
        FillTheDay();
        Assert.IsFalse(_sut.OnHandover("lord_1", "Lord One", 20, false, false, null), "the day is full");

        _sut.ResetForNewSession();

        Assert.IsTrue(_sut.OnHandover("lord_1", "Lord One", 20, false, false, null));
    }

    [TestMethod]
    public void ResetForNewSession_OnAFreshService_IsHarmless()
    {
        _sut.ResetForNewSession();

        Assert.IsTrue(_sut.OnHandover("lord_1", "Lord One", 20, false, false, null));
    }

    [TestMethod]
    public void StartNewDay_EmptiesTheLedger_ForEveryLord()
    {
        FillTheDay();
        _sut.OnHandover("lord_2", "Lord Two", 20, false, false, null);

        _sut.StartNewDay();

        Assert.IsTrue(_sut.OnHandover("lord_1", "Lord One", 20, false, false, null));
        Assert.IsTrue(_sut.OnHandover("lord_2", "Lord Two", 20, false, false, null));
    }
}
