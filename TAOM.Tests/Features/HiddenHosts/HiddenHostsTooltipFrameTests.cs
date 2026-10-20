using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #846: the decisions the tooltip patches make from a frame, as plain code over every subject kind and mode. The patches
/// only apply them to rows; nothing here needs an engine object or a tooltip.
/// </summary>
[TestClass]
public class HiddenHostsTooltipFrameTests
{
    private static TooltipFrame Frame(TooltipSubjectKind kind, TooltipMode mode, string? memory = null, bool shown = false)
        => new(kind, new TooltipVerdict(mode, 0.7f, 1f, 0, 123, memory, 5)) { ShowedMemory = shown };

    private static readonly TooltipSubjectKind[] Kinds =
    {
        TooltipSubjectKind.None, TooltipSubjectKind.Party, TooltipSubjectKind.Army, TooltipSubjectKind.Settlement,
        TooltipSubjectKind.MapEvent, TooltipSubjectKind.SiegeEvent,
    };

    private static readonly TooltipMode[] Modes = { TooltipMode.Vanilla, TooltipMode.Masked, TooltipMode.Horizon };

    private static bool IsPartyOrArmy(TooltipSubjectKind k) => k == TooltipSubjectKind.Party || k == TooltipSubjectKind.Army;

    [TestMethod]
    public void IsPartyOrArmy_IsTrueForExactlyThoseTwoKinds()
    {
        foreach (var kind in Kinds)
            Assert.AreEqual(IsPartyOrArmy(kind), Frame(kind, TooltipMode.Masked).IsPartyOrArmy, kind.ToString());
    }

    [TestMethod]
    public void RenamesTitle_OnlyAPartyOrArmyAtTheHorizon()
    {
        foreach (var kind in Kinds)
            foreach (var mode in Modes)
                Assert.AreEqual(IsPartyOrArmy(kind) && mode == TooltipMode.Horizon, Frame(kind, mode).RenamesTitle, kind + "/" + mode);
    }

    [TestMethod]
    public void WrapsTotalRow_AMaskedPartyOrArmy_WithOrWithoutAMemory()
    {
        foreach (var kind in Kinds)
            foreach (var mode in Modes)
                foreach (var memory in new string?[] { null, "~123" })
                    Assert.AreEqual(IsPartyOrArmy(kind) && mode == TooltipMode.Masked, Frame(kind, mode, memory).WrapsTotalRow,
                        kind + "/" + mode + "/" + memory);
    }

    [TestMethod]
    public void TrimsTroopRows_MaskedOrHorizon_NeverVanilla()
    {
        foreach (var kind in Kinds)
        {
            Assert.IsFalse(Frame(kind, TooltipMode.Vanilla).TrimsTroopRows);
            Assert.IsTrue(Frame(kind, TooltipMode.Masked).TrimsTroopRows);
            Assert.IsTrue(Frame(kind, TooltipMode.Horizon).TrimsTroopRows);
        }
    }

    [TestMethod]
    public void RewritesDefenders_OnlyASettlementThatIsNotVanilla()
    {
        foreach (var kind in Kinds)
            foreach (var mode in Modes)
                Assert.AreEqual(kind == TooltipSubjectKind.Settlement && mode != TooltipMode.Vanilla, Frame(kind, mode).RewritesDefenders,
                    kind + "/" + mode);
    }

    [TestMethod]
    public void AddsReasonRow_OnlyWhenARememberedCountWasPrinted_InAMaskedPartyArmyOrSettlement()
    {
        foreach (var kind in Kinds)
            foreach (var mode in Modes)
                foreach (var memory in new string?[] { null, "~123" })
                    foreach (var shown in new[] { false, true })
                    {
                        bool expected = (IsPartyOrArmy(kind) || kind == TooltipSubjectKind.Settlement)
                                        && mode == TooltipMode.Masked && memory != null && shown;
                        Assert.AreEqual(expected, Frame(kind, mode, memory, shown).AddsReasonRow, kind + "/" + mode + "/" + memory + "/" + shown);
                    }
    }
}
