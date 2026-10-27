using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.InnRounds.Hooks;

namespace TAOM.Tests.Features.InnRounds;

/// <summary>
/// The two priced dialog lines render their price (no "ERROR:" from a broken template). They need the engine's text manager, so
/// they run in the same category as the behavior tests.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class InnRoundsTextsTests
{
    private static void AssertRenders(string text, string what) =>
        StringAssert.DoesNotMatch(text, new System.Text.RegularExpressions.Regex(@"ERROR|\{[A-Z_?]+\}"), what + ": " + text);

    [TestMethod]
    public void PartyLine_CarriesThePrice()
    {
        var text = InnRoundsTexts.PartyLine(240).ToString();

        AssertRenders(text, "party");
        StringAssert.Contains(text, "(240");
    }

    [TestMethod]
    public void HouseLine_CarriesThePrice()
    {
        var text = InnRoundsTexts.HouseLine(100).ToString();

        AssertRenders(text, "house");
        StringAssert.Contains(text, "(100");
    }

    [TestMethod]
    public void TheTwoLines_DoNotShareAnyText()
    {
        Assert.AreNotEqual(InnRoundsTexts.PartyLine(50).ToString(), InnRoundsTexts.HouseLine(50).ToString());
    }

    [TestMethod]
    public void TheCapReason_IsAFullSentence()
    {
        var text = InnRoundsTexts.CapReached().ToString();

        AssertRenders(text, "cap");
        Assert.IsTrue(text.Length > 10, text);
    }
}
