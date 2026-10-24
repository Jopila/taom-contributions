using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SettlementServices;

namespace TAOM.Tests.Features.SettlementServices;

[TestClass]
public class PlayerPurseTests
{
    private ISettlementServicesAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private int _gold;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<ISettlementServicesAdapter>();
        _logger = Substitute.For<IModLogger>();
        _gold = 10_000;
        _adapter.PlayerGold.Returns(ci => _gold);
        _adapter.When(a => a.Refund(Arg.Any<int>())).Do(ci => _gold += ci.Arg<int>());
    }

    [TestMethod]
    public void AFullCharge_Succeeds_AndLogsNothing()
    {
        _adapter.ChargePlayer(500).Returns(500);

        Assert.IsTrue(PlayerPurse.TryCharge(_adapter, _logger, 500, "Watch-Silver"));
        _adapter.DidNotReceive().Refund(Arg.Any<int>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void AShortReceipt_Fails_RefundsWhatWasTaken_AndSaysSo()
    {
        _adapter.ChargePlayer(500).Returns(200);

        Assert.IsFalse(PlayerPurse.TryCharge(_adapter, _logger, 500, "Watch-Silver"));
        _adapter.Received(1).Refund(200);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.StartsWith("[SettlementServices] Watch-Silver took 200 of 500 gold")));
    }

    [TestMethod]
    public void AnEmptyReceipt_Fails_AndRefundsNothing()
    {
        _adapter.ChargePlayer(500).Returns(0);

        Assert.IsFalse(PlayerPurse.TryCharge(_adapter, _logger, 500, "Tidings of the Lists"));
        _adapter.DidNotReceive().Refund(Arg.Any<int>());
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("Tidings of the Lists took 0 of 500")));
    }

    [TestMethod]
    public void AThrowBeforeTheDebit_Fails_RefundsNothing_AndLogsTheCause()
    {
        _adapter.ChargePlayer(500).Throws(new InvalidOperationException("no debit"));

        Assert.IsFalse(PlayerPurse.TryCharge(_adapter, _logger, 500, "Watch-Silver"));
        _adapter.DidNotReceive().Refund(Arg.Any<int>());
        Assert.AreEqual(10_000, _gold);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("no debit")));
    }

    [TestMethod]
    public void AThrowAfterTheDebit_Fails_AndGivesBackWhatTheDebitTook()
    {
        _adapter.When(a => a.ChargePlayer(500)).Do(_ =>
        {
            _gold -= 500;
            throw new InvalidOperationException("dispatch failed");
        });

        Assert.IsFalse(PlayerPurse.TryCharge(_adapter, _logger, 500, "Watch-Silver"));
        Assert.AreEqual(10_000, _gold);
        _adapter.Received(1).Refund(500);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("took 500 of 500") && m.Contains("dispatch failed")));
    }

    [TestMethod]
    public void ARefundThatThrows_StillFails_AndLogsIt()
    {
        // The refund raises the same gold event after it credits, so a listener that threw on the debit throws again.
        _adapter.ChargePlayer(500).Returns(200);
        _adapter.When(a => a.Refund(Arg.Any<int>())).Do(_ => throw new InvalidOperationException("listener again"));

        Assert.IsFalse(PlayerPurse.TryCharge(_adapter, _logger, 500, "Tidings of the Lists"));
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("Tidings of the Lists could not give back 200 gold") && m.Contains("listener again")));
    }
}
