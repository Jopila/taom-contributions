using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.FireAndTribute;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>
/// What a paid tribute does to the host (#870 slice 2): the host is steered again at payment, not at its next hourly step,
/// and the engine's own party AI is told to leave the player's party alone. The rig's Gundabad host rides against empire_w
/// from day 100 at campaign hour 2400; its envoy is due 24 hours later.
/// </summary>
[TestClass]
public class FireAndTributeEnvoyPaymentTests
{
    private const int DueHour = 2400 + 24;

    private FireAndTributeRig _r = null!;
    private RaidHostRecord _host = null!;

    [TestInitialize]
    public void SetUp()
    {
        _r = new FireAndTributeRig();
        _r.Random.DoubleFallback = 0.99;
        _r.OwnVillages(3);
        _host = _r.SpawnEvil();
    }

    private void Offer()
    {
        _r.Snapshots.Hours = DueHour;
        _r.Service.OnHourlyTick();
    }

    private static NearbyParty Party(string id, float distance, bool main = false, bool leadsMain = false) =>
        new NearbyParty
        {
            Id = id, KingdomId = "empire_w", Strength = 50f, Speed = 3f, Distance = distance, IsMainParty = main, LeadsMainParty = leadsMain,
        };

    [TestMethod]
    public void Pay_ReplacesAnEngageOrderOnThePlayersParty_AtOnce()
    {
        _r.Adapter.Nearby.Add(Party("main", 1f, main: true));
        Offer();
        Assert.IsTrue(_r.Adapter.Called($"engage:{_host.PartyId}:main"), "the unpaid host went for the player in the hour the envoy came");
        _r.Adapter.Parties[_host.PartyId].Snapshot.RaidTargetId = null; // an engage order replaces the raid order in the engine
        _r.Adapter.Calls.Clear();

        _r.Presenter.Choose(EnvoyChoice.Pay);

        Assert.IsTrue(_r.Adapter.Called($"raid:{_host.PartyId}:empire_w_v1"), "a new order at payment, not at the next hour");
        Assert.IsFalse(_r.Adapter.Called($"engage:{_host.PartyId}:main"));
    }

    [TestMethod]
    public void Pay_TellsTheEnginesPartyAiToLeaveThePlayerAlone_ForTheRestOfTheHostsLife()
    {
        Offer();
        _r.Adapter.Calls.Clear();

        _r.Presenter.Choose(EnvoyChoice.Pay);

        int days = _r.ConfigProvider.Config.Tuning.MaxHostDays;
        Assert.AreEqual(1, _r.Adapter.Count("spare:"));
        Assert.IsTrue(_r.Adapter.Called($"spare:{_host.PartyId}:{2 * days * 24}"), "twice the timeout, the longest a host stays out");
    }

    [DataTestMethod]
    [DataRow(EnvoyChoice.Refuse)]
    [DataRow(EnvoyChoice.Seize)]
    [DataRow(EnvoyChoice.Delay)]
    public void AnAnswerOtherThanPay_ProtectsNobody_AndReSteersNothing(EnvoyChoice choice)
    {
        Offer();
        _r.Adapter.Calls.Clear();

        _r.Presenter.Choose(choice);

        Assert.AreEqual(0, _r.Adapter.Count("spare:"));
        Assert.AreEqual(0, _r.Adapter.Count("read:"), "no steering step ran");
    }

    [TestMethod]
    public void Pay_APaymentWithNoClock_StillSparesTheClanAndTheParty()
    {
        Offer();
        _r.Snapshots.DayReadable = false;
        _r.Adapter.Calls.Clear();

        _r.Presenter.Choose(EnvoyChoice.Pay);

        Assert.AreEqual("player_clan", _r.LiveRecords().Single().SparedClanId);
        Assert.AreEqual(1, _r.Adapter.Count("spare:"));
        Assert.AreEqual(0, _r.Adapter.Count("read:"), "no day to steer by, so the next hourly step does it");
    }

    [TestMethod]
    public void Pay_AFailingSteeringStep_IsLogged_AndThePaymentStillStandsAndIsToldToThePlayer()
    {
        Offer();
        _r.Adapter.ReadThrows.Add(_host.PartyId);

        _r.Presenter.Choose(EnvoyChoice.Pay);

        _r.Logger.Received().LogError(Arg.Is<string>(s => s.Contains("payment")));
        Assert.AreEqual("player_clan", _r.LiveRecords().Single().SparedClanId);
        Assert.AreEqual(EnvoyOutcome.Paid, _r.Presenter.Outcomes.Single().Outcome);
    }

    // ---- the army the player marches in ------------------------------------------------------------------------------

    [TestMethod]
    public void APaidHost_NeverEngagesTheLeaderOfAnArmyThePlayerIsIn_ButStillEngagesOtherParties()
    {
        Offer();
        _r.Presenter.Choose(EnvoyChoice.Pay);
        _r.Adapter.Nearby.Add(Party("army_leader", 1f, leadsMain: true));
        _r.Adapter.Nearby.Add(Party("companion", 4f));
        _r.Adapter.Calls.Clear();

        _r.Service.OnHourlyTick();

        Assert.IsTrue(_r.Adapter.Called($"engage:{_host.PartyId}:companion"));
        Assert.IsFalse(_r.Adapter.Called($"engage:{_host.PartyId}:army_leader"));
    }

    [TestMethod]
    public void AnUnpaidHost_EngagesTheLeaderOfAnArmyThePlayerIsIn()
    {
        Offer();
        _r.Presenter.Choose(EnvoyChoice.Refuse);
        _r.Adapter.Nearby.Add(Party("army_leader", 1f, leadsMain: true));
        _r.Adapter.Calls.Clear();

        _r.Service.OnHourlyTick();

        Assert.IsTrue(_r.Adapter.Called($"engage:{_host.PartyId}:army_leader"));
    }
}
