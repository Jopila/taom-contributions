using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.WarChronicle.Cheats;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Tests.Features.WarChronicle.Cheats;

/// <summary>The text of <c>taom.chronicle_status</c> and the argument check of <c>taom.chronicle_open</c>.</summary>
[TestClass]
public class ChronicleCheatReportTests
{
    private static List<ChronicleStatusRow> Rows() => new List<ChronicleStatusRow>
    {
        new ChronicleStatusRow { Id = "fellowship", Kind = ChronicleEventKind.Narrative, State = ChronicleEventState.Occurred, OpenDay = 20, ResolvedDay = 20 },
        new ChronicleStatusRow { Id = "hornburg", Kind = ChronicleEventKind.Siege, State = ChronicleEventState.Active, OpenDay = 49, CloseDay = 69, Assaulted = true, Target = "town_V2" },
        new ChronicleStatusRow { Id = "gondor", Kind = ChronicleEventKind.Siege, State = ChronicleEventState.Pending, Target = "town_EW1" },
    };

    [TestMethod]
    public void FormatStatus_ListsEveryEventWithItsStateAndDays()
    {
        var text = ChronicleCheatReport.FormatStatus(Rows(), new ArmyTargetFocus[0], 55.2, true, true);

        StringAssert.Contains(text, "day 55.2");
        StringAssert.Contains(text, "fellowship");
        StringAssert.Contains(text, "Occurred");
        StringAssert.Contains(text, "hornburg");
        StringAssert.Contains(text, "Active");
        StringAssert.Contains(text, "town_V2");
        StringAssert.Contains(text, "closes day 69");
        StringAssert.Contains(text, "assaulted");
        StringAssert.Contains(text, "gondor");
        StringAssert.Contains(text, "opens when its anchor is known");
    }

    [TestMethod]
    public void FormatStatus_ListsTheArmyFocus()
    {
        var focus = new[] { new ArmyTargetFocus("town_V2", new[] { "isengard", "empire" }, "vlandia", 1.4f) };

        var text = ChronicleCheatReport.FormatStatus(Rows(), focus, 55, true, true);

        StringAssert.Contains(text, "town_V2 x1.40: besieged by isengard, empire; defended by vlandia");
        Assert.IsFalse(text.Contains("inactive"));
    }

    [TestMethod]
    public void FormatStatus_StrategicIntelligenceOff_SaysTheFocusIsInactive()
    {
        var focus = new[] { new ArmyTargetFocus("town_V2", new[] { "isengard" }, "vlandia", 1.4f) };

        var text = ChronicleCheatReport.FormatStatus(Rows(), focus, 55, true, false);

        StringAssert.Contains(text, "inactive: AI Strategic Intelligence is off");
    }

    [TestMethod]
    public void FormatStatus_ActivePastItsCloseDay_SaysTheCloseWaitsOnTheSiege()
    {
        var text = ChronicleCheatReport.FormatStatus(Rows(), new ArmyTargetFocus[0], 72, true, true);

        StringAssert.Contains(text, "overtime: an attacker still besieges it");
    }

    [TestMethod]
    public void FormatStatus_ChronicleOff_SaysSo()
    {
        StringAssert.Contains(ChronicleCheatReport.FormatStatus(Rows(), new ArmyTargetFocus[0], 1, false, true), "off");
    }

    [TestMethod]
    public void FormatStatus_NoEvents_SaysTheFileLoadedNone()
    {
        StringAssert.Contains(ChronicleCheatReport.FormatStatus(new List<ChronicleStatusRow>(), new ArmyTargetFocus[0], 1, true, true), "No chronicle events");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(2)]
    public void TryReadOpenArgs_NotExactlyOneId_ReturnsTheUsage(int count)
    {
        var args = new List<string>();
        for (var i = 0; i < count; i++)
            args.Add("x" + i);

        Assert.IsFalse(ChronicleCheatReport.TryReadOpenArgs(args, out _, out var error));
        StringAssert.Contains(error, "taom.chronicle_open <id>");
    }

    [TestMethod]
    public void TryReadOpenArgs_OneId_ReturnsItTrimmed()
    {
        Assert.IsTrue(ChronicleCheatReport.TryReadOpenArgs(new List<string> { " hornburg " }, out var id, out _));
        Assert.AreEqual("hornburg", id);
    }
}
