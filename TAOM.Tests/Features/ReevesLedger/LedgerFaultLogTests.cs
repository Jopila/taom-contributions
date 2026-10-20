using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.ReevesLedger.UI;

namespace TAOM.Tests.Features.ReevesLedger;

[TestClass]
public class LedgerFaultLogTests
{
    private IModLogger _logger = null!;
    private LedgerFaultLog _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new LedgerFaultLog(_logger);
    }

    [TestMethod]
    public void Run_SwallowsTheThrow_AndLogsOncePerSite()
    {
        for (var i = 0; i < 5; i++)
            _sut.Run("refresh", () => throw new InvalidOperationException("boom"));

        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("refresh") && m.Contains("boom")));
    }

    [TestMethod]
    public void TheLogLine_SaysOnlyTheLineIsLatched_NotThatThePartIsSkipped()
    {
        _sut.Run("refresh", () => throw new InvalidOperationException("boom"));

        _logger.Received(1).LogError(Arg.Is<string>(m => !m.Contains("skipped") && m.Contains("later calls still run") && m.Contains("logged once")));
    }

    [TestMethod]
    public void ADifferentSite_StillLogs()
    {
        _sut.Run("refresh", () => throw new InvalidOperationException("a"));
        _sut.Run("click", () => throw new InvalidOperationException("b"));

        _logger.Received(2).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Lines_And_Text_ReturnEmptyOnAThrow()
    {
        Assert.AreEqual(0, _sut.Lines("tip", () => throw new InvalidOperationException()).Count);
        Assert.AreEqual("", _sut.Text("patrol", () => throw new InvalidOperationException()));
        Assert.AreEqual("ok", _sut.Text("patrol2", () => "ok"));
        _logger.Received(2).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void AProblemWithoutAnException_LogsOncePerSiteToo()
    {
        _sut.LogOnce("step", "the reserve moved");
        _sut.LogOnce("step", "the reserve moved");

        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("the reserve moved")));
    }
}
