using System;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Composition;

[TestClass]
public sealed class MainMenuStepTests
{
    [TestInitialize]
    public void Reset() => MainMenuStep.ResetForTests();

    [TestMethod]
    public void Run_StepSucceeds_ReturnsTrueAndLogsNothing()
    {
        var log = new RecordingLogger();

        var ok = MainMenuStep.Run("a", () => { }, () => log);

        Assert.IsTrue(ok);
        Assert.AreEqual(0, log.Lines.Count);
    }

    [TestMethod]
    public void Run_StepThrows_ReturnsFalseAndDoesNotRethrow()
    {
        var log = new RecordingLogger();

        var ok = MainMenuStep.Run("a", () => throw new InvalidOperationException("boom"), () => log);

        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void Run_StepThrows_LogsOneWarningNamingStepAndException()
    {
        var log = new RecordingLogger();

        MainMenuStep.Run("menu customization", () => throw new InvalidOperationException("boom"), () => log);

        Assert.AreEqual(1, log.Lines.Count);
        StringAssert.StartsWith(log.Lines[0], "WARN ");
        StringAssert.Contains(log.Lines[0], "[MainMenuStep] step 'menu customization' failed and was skipped");
        StringAssert.Contains(log.Lines[0], "InvalidOperationException: boom");
    }

    [TestMethod]
    public void Run_StepThrows_WarningCarriesTheFullExceptionWithItsStack()
    {
        var log = new RecordingLogger();

        MainMenuStep.Run("menu customization", ThrowBoom, () => log);

        StringAssert.Contains(log.Lines[0], "System.InvalidOperationException: boom");
        StringAssert.Contains(log.Lines[0], nameof(ThrowBoom));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowBoom() => throw new InvalidOperationException("boom");

    [TestMethod]
    public void Run_SameStepThrowsTwice_LogsOnce()
    {
        var log = new RecordingLogger();

        MainMenuStep.Run("a", () => throw new InvalidOperationException("1"), () => log);
        MainMenuStep.Run("a", () => throw new InvalidOperationException("2"), () => log);

        Assert.AreEqual(1, log.Lines.Count);
    }

    [TestMethod]
    public void Run_TwoStepsThrow_LogsEachOnce()
    {
        var log = new RecordingLogger();

        MainMenuStep.Run("a", () => throw new InvalidOperationException("1"), () => log);
        MainMenuStep.Run("b", () => throw new InvalidOperationException("2"), () => log);
        MainMenuStep.Run("a", () => throw new InvalidOperationException("3"), () => log);

        Assert.AreEqual(2, log.Lines.Count);
    }

    [TestMethod]
    public void Run_StepThrowsThenSucceeds_RunsOnEveryCall()
    {
        var log = new RecordingLogger();
        var calls = 0;

        var first = MainMenuStep.Run("a", () => { calls++; throw new InvalidOperationException("x"); }, () => log);
        var second = MainMenuStep.Run("a", () => calls++, () => log);

        Assert.IsFalse(first);
        Assert.IsTrue(second);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public void Run_LoggerFactoryThrows_DoesNotRethrow()
    {
        var ok = MainMenuStep.Run("a", () => throw new InvalidOperationException("x"),
            () => throw new InvalidOperationException("no logger"));

        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void Run_LoggerNull_DoesNotRethrow()
    {
        var ok = MainMenuStep.Run("a", () => throw new InvalidOperationException("x"), () => null);

        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void Run_LoggerThrows_DoesNotRethrow()
    {
        var log = new RecordingLogger { OnLog = _ => throw new InvalidOperationException("log failed") };

        var ok = MainMenuStep.Run("a", () => throw new InvalidOperationException("x"), () => log);

        Assert.IsFalse(ok);
    }
}
