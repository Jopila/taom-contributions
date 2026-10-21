using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Features.FarWatch;
using TAOM.Features.FarWatch.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FarWatch;

/// <summary>
/// Every line the player reads in Far Watch renders on every branch (no "ERROR:" from a missing key or a broken template),
/// names pass through the brace filter, the Hidden Hosts glyphs reach the report verbatim as variables, the sight line prints
/// only when the ride set a bonus, and no registered default holds a newline (a raw one is read back as a space, and the
/// harvest tool writes a C# escape as a line break).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FarWatchTextsTests
{
    [TestInitialize]
    public void SetUp() => FarWatchTexts.HoursText = hours => new TextObject("{=!}{H} hours").SetTextVariable("H", hours);

    [TestCleanup]
    public void Cleanup() => FarWatchTexts.HoursText = FarWatchTexts.DefaultHoursText;

    private static void AssertRenders(string text, string what)
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(text), what + " is empty");
        StringAssert.DoesNotMatch(text, new System.Text.RegularExpressions.Regex("ERROR|\\{[A-Z_]+\\}"), what + ": " + text);
    }

    [TestMethod]
    public void TheDefaults_RenderAsTheirEnglish()
    {
        Assert.AreEqual("Send a scout", FarWatchTexts.OptionLabel(new ScoutOffer(ScoutOfferStatus.Ready)).ToString());
        Assert.AreEqual("Ride out to scout", FarWatchTexts.OptionLabel(new ScoutOffer(ScoutOfferStatus.Ready, selfScout: true)).ToString());
        Assert.AreEqual("Await your scout", FarWatchTexts.OptionLabel(new ScoutOffer(ScoutOfferStatus.Running)).ToString());
        Assert.AreEqual("Continue the ride", FarWatchTexts.OptionLabel(new ScoutOffer(ScoutOfferStatus.Running, selfScout: true)).ToString());
        Assert.AreEqual("Call Mirien back", FarWatchTexts.OptionRecall("Mirien", selfScout: false));
        Assert.AreEqual("Ride back to camp", FarWatchTexts.OptionRecall("Aldric", selfScout: true));
    }

    [TestMethod]
    public void EveryBlock_HasAReason_ThatRenders_AndNamesTheScoutWhereItShould()
    {
        foreach (ScoutBlock block in Enum.GetValues(typeof(ScoutBlock)))
        {
            if (block == ScoutBlock.None) continue;
            var reason = FarWatchTexts.BlockReason(new ScoutOffer(ScoutOfferStatus.Blocked, block, "s", "Mirien", 10, false, 3));
            Assert.IsNotNull(reason, block.ToString());
            AssertRenders(reason!.ToString(), block.ToString());
        }

        StringAssert.Contains(FarWatchTexts.BlockReason(new ScoutOffer(ScoutOfferStatus.Blocked, ScoutBlock.Resting, "s", "Mirien", 10, false, 3))!.ToString(), "Mirien is resting");
        StringAssert.Contains(FarWatchTexts.BlockReason(new ScoutOffer(ScoutOfferStatus.Blocked, ScoutBlock.Resting, "s", "Mirien", 10, false, 3))!.ToString(), "3 hours");
    }

    [TestMethod]
    public void ARestOfZeroHours_ReadsAsOneHour_NeverZero() =>
        StringAssert.Contains(FarWatchTexts.BlockReason(new ScoutOffer(ScoutOfferStatus.Blocked, ScoutBlock.Resting, "s", "M", 0, false, 0))!.ToString(), "1 hours");

    [TestMethod]
    public void ANonBlockStatus_HasNoReason() =>
        Assert.IsNull(FarWatchTexts.BlockReason(new ScoutOffer(ScoutOfferStatus.Ready)));

    [TestMethod]
    public void EveryEndReason_RendersItsOwnLine_NamingTheScout()
    {
        var lines = new HashSet<string>();
        foreach (ScoutEndReason reason in Enum.GetValues(typeof(ScoutEndReason)))
        {
            var text = FarWatchTexts.EndMessage(new ScoutEndNotice(reason, "Mirien"));
            AssertRenders(text, reason.ToString());
            StringAssert.Contains(text, "Mirien", reason.ToString());
            Assert.IsTrue(lines.Add(text), reason + " repeats another reason's line");
        }
    }

    [TestMethod]
    public void TheBattleLine_IsTrueOfAnyEncounter_NotOnlyOfABattle() =>
        StringAssert.DoesNotMatch(FarWatchTexts.EndMessage(new ScoutEndNotice(ScoutEndReason.Battle, "M")), new System.Text.RegularExpressions.Regex("battle"));

    [TestMethod]
    public void AllEightDirections_AndEveryKind_RenderDistinctWords()
    {
        var directions = Enumerable.Range(0, 8).Select(FarWatchTexts.Direction).ToList();
        CollectionAssert.AreEqual(new[] { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" }, directions);
        Assert.AreEqual("north", FarWatchTexts.Direction(-1));
        Assert.AreEqual("north", FarWatchTexts.Direction(99));

        var kinds = Enum.GetValues(typeof(ScoutComposition)).Cast<ScoutComposition>().Select(FarWatchTexts.Kind).ToList();
        Assert.AreEqual(kinds.Count, kinds.Distinct().Count());
        kinds.ForEach(k => AssertRenders(k, "kind"));
    }

    [TestMethod]
    public void Safe_StripsBraces_NeverReturnsNull()
    {
        Assert.AreEqual("Aldric the Bold", FarWatchTexts.Safe("{Aldric} the {=x}Bold".Replace("=x", "")));
        Assert.AreEqual("", FarWatchTexts.Safe(null));
        Assert.AreEqual("", FarWatchTexts.Safe(""));
        Assert.AreEqual("=taom_x", FarWatchTexts.Safe("{=taom_x}"), "a leading key marker cannot survive the brace filter");
    }

    [TestMethod]
    public void ANameWithBraces_CannotBreakALine()
    {
        var text = FarWatchTexts.EndMessage(new ScoutEndNotice(ScoutEndReason.CallBack, "{=evil}Aldric}"));

        Assert.AreEqual("=evilAldric rides back to camp with nothing to report.", text);
    }

    // ---- The report

    private static ScoutReport Surroundings(params ScoutHostRow[] rows)
    {
        var report = new ScoutReport(ScoutKind.Surroundings, "Mirien", lost: false);
        report.Hosts.AddRange(rows);
        return report;
    }

    [TestMethod]
    public void TheReport_RowsCarryTheHiddenHostsGlyphsVerbatim()
    {
        var report = Surroundings(new ScoutHostRow("Lord Aldric", "12?", ScoutComposition.Horse, 1), new ScoutHostRow("Warband", "...", ScoutComposition.None, 6));

        var body = FarWatchReportText.Body(report);

        StringAssert.Contains(body, "Lord Aldric: 12? (mostly horse), north-east");
        StringAssert.Contains(body, "Warband: ... (unclear), west");
        StringAssert.Contains(body, "Hosts sighted around the camp:");
        Assert.IsFalse(body.Contains("sees farther"), "no sight was set by this report");
    }

    [TestMethod]
    public void TheReport_TheSightLine_PrintsOnlyWhenARideSetASight()
    {
        var with = Surroundings(new ScoutHostRow("A", "5", ScoutComposition.Foot, 0));
        with.SightHours = 36;

        StringAssert.Contains(FarWatchReportText.Body(with), "Your party sees farther for about 36 hours.");
        Assert.IsFalse(FarWatchReportText.Body(Surroundings(new ScoutHostRow("A", "5", ScoutComposition.Foot, 0))).Contains("sees farther"));
    }

    [TestMethod]
    public void TheReport_NoHosts_SaysSoByName_AndStillPrintsTheSightLine()
    {
        var report = Surroundings();
        report.SightHours = 24;

        var body = FarWatchReportText.Body(report);

        StringAssert.Contains(body, "Mirien saw no hosts worth reporting.");
        StringAssert.Contains(body, "sees farther for about 24 hours");
    }

    [TestMethod]
    public void TheReport_ABodyIsJoinedInCode_WithNoRawControlCharactersInAnyKey()
    {
        var report = Surroundings(new ScoutHostRow("A", "5", ScoutComposition.Foot, 0), new ScoutHostRow("B", "6", ScoutComposition.Foot, 2));

        Assert.AreEqual(3, FarWatchReportText.Body(report).Split('\n').Length, "a header and two rows");
    }

    [TestMethod]
    public void TheReport_Title_NamesTheScout() =>
        Assert.AreEqual("Mirien returns", FarWatchReportText.Title(Surroundings()));

    [TestMethod]
    public void TheSettlementReport_ListsTheTotal_ThenEachPartyByKind()
    {
        var report = new ScoutReport(ScoutKind.Settlement, "Mirien", lost: false) { SiteName = "Edoras", SiteTotal = 245 };
        report.Defenders.Add(new ScoutDefenderRow(ScoutDefenderKind.Garrison, "Edoras Garrison", 120, ScoutComposition.Foot));
        report.Defenders.Add(new ScoutDefenderRow(ScoutDefenderKind.Militia, "Edoras Militia", 40, ScoutComposition.Foot));
        report.Defenders.Add(new ScoutDefenderRow(ScoutDefenderKind.Lord, "Eomer's Party", 85, ScoutComposition.Horse));
        report.Defenders.Add(new ScoutDefenderRow(ScoutDefenderKind.Lord, "Empty Party", 0, ScoutComposition.None));

        var lines = FarWatchReportText.Body(report).Split('\n');

        CollectionAssert.AreEqual(new[]
        {
            "Edoras is held by 245 men:", "Garrison: 120 (mostly foot)", "Militia: 40", "Eomer's Party: 85 (mostly horse)",
        }, lines);
    }

    [TestMethod]
    public void TheSettlementReport_NobodyHome_SaysSoInsteadOfClaimingZeroMen()
    {
        var report = new ScoutReport(ScoutKind.Settlement, "Mirien", lost: false) { SiteName = "Edoras", SiteTotal = 0 };

        Assert.AreEqual("Mirien saw no hosts worth reporting.", FarWatchReportText.Body(report));
    }

    [TestMethod]
    public void TheClose_Button_Renders() => Assert.AreEqual("Close", FarWatchReportText.Close());

    // ---- The presenter

    [TestMethod]
    public void Presenter_AReport_OpensAnInquiry_ALostScoutIsOneLine_NothingIsNothing()
    {
        var notifier = Substitute.For<IFarWatchNotifier>();
        var sut = new FarWatchPresenter(notifier);

        sut.Report(null);
        sut.End(null);
        notifier.DidNotReceiveWithAnyArgs().ShowReport(default!, default!, default!);
        notifier.DidNotReceiveWithAnyArgs().ShowMessage(default!);

        sut.Report(new ScoutReport(ScoutKind.Surroundings, "Mirien", lost: true));
        notifier.Received(1).ShowMessage("Mirien did not come back.");
        notifier.DidNotReceiveWithAnyArgs().ShowReport(default!, default!, default!);

        sut.Report(Surroundings(new ScoutHostRow("A", "5", ScoutComposition.Foot, 0)));
        notifier.Received(1).ShowReport("Mirien returns", Arg.Is<string>(b => b.Contains("A: 5")), "Close");
    }

    // ---- The registered defaults

    [TestMethod]
    public void TheRegisteredFarWatchDefaults_HoldNoNewline_AndEveryKeyUsedInTheCodeIsRegistered()
    {
        var xml = XDocument.Parse(RepoPaths.ReadSource("Main/_Module/ModuleData/taom_module_strings.xml"));
        var rows = xml.Descendants("string").Where(e => ((string?)e.Attribute("id") ?? "").StartsWith("taom_fwatch_", StringComparison.Ordinal)).ToList();
        Assert.IsTrue(rows.Count >= 40, "the Far Watch rows are registered: " + rows.Count);

        foreach (var row in rows)
        {
            var text = (string)row.Attribute("text")!;
            Assert.IsFalse(text.Contains("\n") || text.Contains("\r"), (string)row.Attribute("id")! + " holds a newline");
            StringAssert.StartsWith(text, "{=" + (string)row.Attribute("id")! + "}", "the registered text is the inline default");
        }

        var used = System.Text.RegularExpressions.Regex.Matches(
                string.Concat(System.IO.Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "FarWatch", "Hooks"), "*.cs").Select(System.IO.File.ReadAllText)),
                "\\{=(taom_fwatch_\\w+)\\}")
            .Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        var registered = rows.Select(r => (string)r.Attribute("id")!).ToHashSet();
        CollectionAssert.AreEquivalent(used, registered.ToList(), "every registered key is rendered by the code, and every key the code uses is registered");
    }
}
