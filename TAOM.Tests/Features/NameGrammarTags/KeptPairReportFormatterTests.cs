using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.NameGrammarTags;

namespace TAOM.Tests.Features.NameGrammarTags;

[TestClass]
public class KeptPairReportFormatterTests
{
    private static readonly CanaryResult Pass = new CanaryResult(true, true, "{.di}{CANARY} printed 'di Roma'");

    private static RenderedLine Line(string variant, string vanilla, string taom, bool failed = false) =>
        new RenderedLine(variant, vanilla, taom, failed);

    private static RenderedPair Pair(string id, string subject, params RenderedLine[] lines) =>
        new RenderedPair(new KeptPair(id, "raw v", "raw t"), subject, lines);

    private static string Format(params RenderedPair[] rendered) =>
        KeptPairReportFormatter.Format("IT", "Italiano", "ItalianTextProcessor", Pass, rendered.Length,
            new[] { "Gondor" }, rendered);

    [TestMethod]
    public void Format_Pair_ListsBothRenderedLinesUnderTheirVariant()
    {
        var text = Format(Pair("0bPb5btR", "Gondor", Line("Name", "Il re di Gondor", "Il re Gondor")));
        StringAssert.Contains(text, "0bPb5btR");
        StringAssert.Contains(text, "[Gondor]");
        StringAssert.Contains(text, "[Name]");
        StringAssert.Contains(text, "vanilla: Il re di Gondor");
        StringAssert.Contains(text, "TAOM:    Il re Gondor");
    }

    [TestMethod]
    public void Format_Header_NamesLanguageProcessorCanaryAndCounts()
    {
        var text = KeptPairReportFormatter.Format("BR", "Português (BR)", "PortugueseBrazilTextProcessor", Pass, 171,
            new[] { "Gondor", "Rohan" }, new[] { Pair("a", "Gondor", Line("Name", "x", "y")) });
        StringAssert.Contains(text, "Language: BR (Português (BR))");
        Assert.IsFalse(text.Contains("the game is running in"));
        StringAssert.Contains(text, "Text processor: PortugueseBrazilTextProcessor");
        StringAssert.Contains(text, "Canary: PASS");
        StringAssert.Contains(text, "Kept pairs: 171");
        StringAssert.Contains(text, "Subjects: Gondor, Rohan");
    }

    [TestMethod]
    public void Format_FailedCanary_SaysFailInTheHeader()
    {
        var text = KeptPairReportFormatter.Format("IT", "Italiano", "EnglishTextProcessor",
            new CanaryResult(true, false, "{.di}{CANARY} printed 'Roma'"), 1, new[] { "Gondor" }, new[] { Pair("a", "Gondor", Line("Name", "x", "x")) });
        StringAssert.Contains(text, "Canary: FAIL");
        StringAssert.Contains(text, "do not trust");
    }

    [TestMethod]
    public void Format_NotApplicableCanary_SaysSo()
    {
        var text = KeptPairReportFormatter.Format("DE", "Deutsch", "GermanTextProcessor",
            new CanaryResult(false, true, "no canary for DE"), 1, new[] { "Gondor" }, new[] { Pair("a", "Gondor", Line("Name", "x", "x")) });
        StringAssert.Contains(text, "Canary: not applicable");
    }

    [TestMethod]
    public void Format_IdenticalLines_AreMarkedSame()
    {
        var text = Format(Pair("a", "Gondor", Line("Name", "stesso", "stesso")));
        StringAssert.Contains(text, "a [Gondor] SAME");
    }

    [TestMethod]
    public void Format_AnyVariantDiffers_MarksTheBlockDifferent()
    {
        var text = Format(Pair("a", "Gondor", Line("Name", "stesso", "stesso"), Line("InformalName", "uno", "due")));
        StringAssert.Contains(text, "a [Gondor] DIFFERENT");
    }

    [TestMethod]
    public void Format_ARenderThatFailed_IsFailedNeverSame()
    {
        var failed = Pair("a", "Gondor", Line("Name", "render failed: bad", "render failed: bad", failed: true));
        Assert.AreEqual("FAILED", failed.Verdict);
        StringAssert.Contains(Format(failed), "a [Gondor] FAILED");
    }

    [TestMethod]
    public void Format_Summary_CountsSameDifferentAndFailed()
    {
        var text = Format(Pair("a", "G", Line("Name", "x", "x")), Pair("b", "G", Line("Name", "x", "y")),
            Pair("c", "G", Line("Name", "e", "e", failed: true)));
        StringAssert.Contains(text, "Rendered 3 block(s): 1 identical, 1 different, 1 failed.");
    }

    [TestMethod]
    public void Format_Report_HasNoEmDashOrEnDash()
    {
        var text = Format(Pair("a", "G", Line("Name", "x", "y")));
        Assert.IsFalse(text.Contains((char)0x2014) || text.Contains((char)0x2013));
    }
}
