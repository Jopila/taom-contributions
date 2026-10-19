using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Core.Logging;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #846: a hook that faults shows vanilla's text for that frame. A fault that repeats every frame would cost a throw per
/// plate and flood the log, so one line per hook and exception type is logged and the 32nd fault stops the hooks for the
/// rest of the process. No game type is touched here, so none of this needs the engine.
/// </summary>
[TestClass]
public class HiddenHostsFaultBudgetTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        HiddenHostsCalls.Initialize(null, null, _logger);
    }

    [TestCleanup]
    public void Cleanup() => HiddenHostsCalls.Initialize(null, null);

    [TestMethod]
    public void ReportFault_LogsOncePerHookAndExceptionType()
    {
        HiddenHostsCalls.ReportFault("hook", new InvalidOperationException("a"));
        HiddenHostsCalls.ReportFault("hook", new InvalidOperationException("b"));
        HiddenHostsCalls.ReportFault("hook", new ArgumentException("c"));
        HiddenHostsCalls.ReportFault("other", new ArgumentException("d"));

        _logger.Received(3).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void ReportFault_WithinTheBudget_LetsTheHooksGoOn()
    {
        for (int i = 0; i < HiddenHostsCalls.FaultBudget - 1; i++)
        {
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());
            Assert.IsFalse(HiddenHostsCalls.Stopped, "fault " + (i + 1));
        }

        Assert.IsFalse(HiddenHostsCalls.Stopped);
    }

    [TestMethod]
    public void ReportFault_PastTheBudget_TellsTheHooksToStop()
    {
        for (int i = 0; i < HiddenHostsCalls.FaultBudget - 1; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());
        Assert.IsTrue(HiddenHostsCalls.Stopped, "the fault that spends the budget");
        HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());
        Assert.IsTrue(HiddenHostsCalls.Stopped, "and every one after");
    }

    [TestMethod]
    public void ReportFault_TheBudgetIsReached_LogsOneFromNowOnLine()
    {
        for (int i = 0; i < HiddenHostsCalls.FaultBudget + 5; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        // One line for the first fault of the hook, one when the budget is reached; nothing per fault after that.
        _logger.Received(2).LogError(Arg.Any<string>());
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("from now on")));
    }

    [TestMethod]
    public void Initialize_ResetsTheBudgetAndTheReportedSet()
    {
        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());
        Assert.IsTrue(HiddenHostsCalls.Stopped);

        HiddenHostsCalls.Initialize(null, null, _logger);

        Assert.IsFalse(HiddenHostsCalls.Stopped);
        _logger.ClearReceivedCalls();
        HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void ReportFault_TheLoggerThrows_IsSwallowed()
    {
        _logger.When(l => l.LogError(Arg.Any<string>())).Throw(new InvalidOperationException("log down"));

        HiddenHostsCalls.ReportFault("hook", new InvalidOperationException("a"));

        Assert.IsFalse(HiddenHostsCalls.Stopped);
    }

    [TestMethod]
    public void ReportFault_NoLogger_StillCounts()
    {
        HiddenHostsCalls.Initialize(null, null);

        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        Assert.IsTrue(HiddenHostsCalls.Stopped);
    }
}
