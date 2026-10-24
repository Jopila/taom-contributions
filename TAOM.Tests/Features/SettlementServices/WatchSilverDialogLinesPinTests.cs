using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SettlementServices;

/// <summary>
/// Source pins for the Watch-Silver conversation graph (#859). The order matters: the engine sorts a node's lines by descending
/// priority and the first line whose condition is true wins, so the paid-out reply must sit above the unconditional refusal. The
/// lines register with a running campaign, so the graph is pinned on the comment-stripped source.
/// </summary>
[TestClass]
public class WatchSilverDialogLinesPinTests
{
    private static string Lines() => RepoPaths.ReadSource("Main/Features/SettlementServices/Hooks/WatchSilverDialogLines.cs", stripComments: true);

    [TestMethod]
    public void TheTokenChain_RunsFromTheMainOptionsToTheResult()
    {
        var src = Lines();

        Assert.IsTrue(Regex.IsMatch(src, @"AddPlayerLine\(""taom_wsilver_offer"",\s*""hero_main_options"",\s*""taom_wsilver_ask"""));
        Assert.IsTrue(Regex.IsMatch(src, @"AddDialogLine\(""taom_wsilver_ask"",\s*""taom_wsilver_ask"",\s*""taom_wsilver_tiers"""));
        foreach (var tier in new[] { 0, 1, 2 })
            Assert.IsTrue(Regex.IsMatch(src, $@"AddPlayerLine\(""taom_wsilver_tier_{tier}"",\s*""taom_wsilver_tiers"",\s*""taom_wsilver_result"""), "tier " + tier);
    }

    [TestMethod]
    public void EveryReply_AndNeverMind_ReturnToTheGovernorsMainOptions()
    {
        var src = Lines();

        Assert.IsTrue(Regex.IsMatch(src, @"AddPlayerLine\(""taom_wsilver_never_mind"",\s*""taom_wsilver_tiers"",\s*""lord_pretalk"",\s*""\{=D33fIGQe\}Never mind\."""));
        Assert.IsTrue(Regex.IsMatch(src, @"AddDialogLine\(""taom_wsilver_done"",\s*""taom_wsilver_result"",\s*""lord_pretalk"""));
        Assert.IsTrue(Regex.IsMatch(src, @"AddDialogLine\(""taom_wsilver_refused"",\s*""taom_wsilver_result"",\s*""lord_pretalk"""));
    }

    [TestMethod]
    public void ThePaidOutReply_SitsOneAboveTheUnconditionalRefusal_AtPriority111()
    {
        var src = Lines();

        StringAssert.Contains(src, "private const int Priority = 111;");
        Assert.IsTrue(Regex.IsMatch(src, @"b\.PaidOut,\s*null,\s*Priority \+ 1\)"), "done is true only after a payment and wins at Priority + 1");
        Assert.IsTrue(Regex.IsMatch(src, @"""taom_wsilver_refused"",[\s\S]*?\bnull,\s*null,\s*Priority\)"), "refused has no condition and no consequence, at Priority");
    }
}
