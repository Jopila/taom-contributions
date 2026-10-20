using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #846 phase 2: the open tooltip frame. The number helpers have no parameter that names the subject, so the bracket
/// around the engine call publishes the frame here; it must be restored, not cleared, on close, and invisible to any
/// thread but the one that opened it.
/// </summary>
[TestClass]
public class HiddenHostsTooltipScopeTests
{
    private static TooltipFrame Frame(TooltipSubjectKind kind = TooltipSubjectKind.Party, TooltipMode mode = TooltipMode.Masked) =>
        new(kind, new TooltipVerdict(mode, 0.5f, 1f, 0, 100));

    [TestCleanup]
    public void Cleanup()
    {
        // A failed test must not leave a frame open for the next one.
        while (HiddenHostsTooltipScope.Current != null)
            HiddenHostsTooltipScope.Close(new ScopeToken(null));
    }

    [TestMethod]
    public void Current_Initially_IsNull() => Assert.IsNull(HiddenHostsTooltipScope.Current);

    [TestMethod]
    public void Open_MakesTheFrameCurrent_AndCloseRestoresNothing()
    {
        var frame = Frame();

        var token = HiddenHostsTooltipScope.Open(frame);

        Assert.IsTrue(token.IsSet);
        Assert.AreSame(frame, HiddenHostsTooltipScope.Current);

        HiddenHostsTooltipScope.Close(in token);

        Assert.IsNull(HiddenHostsTooltipScope.Current);
    }

    [TestMethod]
    public void Open_Nested_CloseRestoresTheOuterFrame()
    {
        var outer = Frame(TooltipSubjectKind.Settlement);
        var inner = Frame(TooltipSubjectKind.Party, TooltipMode.Horizon);
        var outerToken = HiddenHostsTooltipScope.Open(outer);

        var innerToken = HiddenHostsTooltipScope.Open(inner);
        Assert.AreSame(inner, HiddenHostsTooltipScope.Current);
        HiddenHostsTooltipScope.Close(in innerToken);

        Assert.AreSame(outer, HiddenHostsTooltipScope.Current);
        HiddenHostsTooltipScope.Close(in outerToken);
        Assert.IsNull(HiddenHostsTooltipScope.Current);
    }

    [TestMethod]
    public void Close_Twice_IsIdempotent()
    {
        var outer = Frame(TooltipSubjectKind.Settlement);
        var outerToken = HiddenHostsTooltipScope.Open(outer);
        var innerToken = HiddenHostsTooltipScope.Open(Frame());

        HiddenHostsTooltipScope.Close(in innerToken);
        HiddenHostsTooltipScope.Close(in innerToken);

        Assert.AreSame(outer, HiddenHostsTooltipScope.Current, "Harmony reruns every finalizer when a later one throws");
        HiddenHostsTooltipScope.Close(in outerToken);
    }

    [TestMethod]
    public void Close_UnsetToken_DoesNothing()
    {
        var frame = Frame();
        var token = HiddenHostsTooltipScope.Open(frame);

        HiddenHostsTooltipScope.Close(default(ScopeToken));

        Assert.AreSame(frame, HiddenHostsTooltipScope.Current, "a stripped prefix leaves __state default");
        HiddenHostsTooltipScope.Close(in token);
    }

    [TestMethod]
    public void Current_AnotherThread_SeesNothing()
    {
        var token = HiddenHostsTooltipScope.Open(Frame());
        TooltipFrame? seen = Frame();

        var thread = new Thread(() => seen = HiddenHostsTooltipScope.Current);
        thread.Start();
        thread.Join();

        Assert.IsNull(seen);
        Assert.IsNotNull(HiddenHostsTooltipScope.Current);
        HiddenHostsTooltipScope.Close(in token);
    }

    [TestMethod]
    public void Frame_RecordsItsKindAndVerdict()
    {
        var frame = new TooltipFrame(TooltipSubjectKind.Army, new TooltipVerdict(TooltipMode.Horizon, 0.9f, 2f, 1, 50));

        Assert.AreEqual(TooltipSubjectKind.Army, frame.Kind);
        Assert.AreEqual(TooltipMode.Horizon, frame.Verdict.Mode);
        Assert.AreEqual(50, frame.Verdict.SubjectCount);
    }

    [TestMethod]
    public void TrimToken_DefaultIsUnset_AndCarriesItsStart()
    {
        Assert.IsFalse(default(TrimToken).IsSet);
        Assert.AreEqual(0, default(TrimToken).Start);

        var token = new TrimToken(7);

        Assert.IsTrue(token.IsSet);
        Assert.AreEqual(7, token.Start);
    }

    [TestMethod]
    public void Interleaved_TwoThreads_OpeningAndClosingOutOfOrder_LeaveNoStaleFrame()
    {
        var aOpened = new ManualResetEventSlim();
        var bOpened = new ManualResetEventSlim();
        var aClosed = new ManualResetEventSlim();
        var bClosed = new ManualResetEventSlim();
        TooltipFrame? aFinal = Frame();
        TooltipFrame? bFinal = Frame();
        var a = new Thread(() =>
        {
            var token = HiddenHostsTooltipScope.Open(Frame());
            aOpened.Set();
            bOpened.Wait(5000);
            HiddenHostsTooltipScope.Close(in token);
            aClosed.Set();
            bClosed.Wait(5000);
            aFinal = HiddenHostsTooltipScope.Current;
        });
        var b = new Thread(() =>
        {
            aOpened.Wait(5000);
            var token = HiddenHostsTooltipScope.Open(Frame());
            bOpened.Set();
            aClosed.Wait(5000);
            HiddenHostsTooltipScope.Close(in token);
            bClosed.Set();
            bFinal = HiddenHostsTooltipScope.Current;
        });

        a.Start();
        b.Start();
        Assert.IsTrue(a.Join(10000) && b.Join(10000), "the interleave must finish");

        Assert.IsNull(aFinal, "thread A closed its frame; B's later close must not bring it back");
        Assert.IsNull(bFinal);
    }
}
