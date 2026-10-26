using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.NotableServices.Hooks;

namespace TAOM.Tests.Features.NotableServices;

/// <summary>
/// The two priced dialog lines render their count agreement and price (no "ERROR:" from a broken template). They need the
/// engine's text manager, so they run in the same category as the behavior tests.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class NotableServicesTextsTests
{
    private static void AssertRenders(string text, string what) =>
        StringAssert.DoesNotMatch(text, new System.Text.RegularExpressions.Regex(@"ERROR|\{[A-Z_?]+\}"), what + ": " + text);

    [TestMethod]
    public void IntroduceLine_ForOneNotable_IsSingular_AndCarriesThePrice()
    {
        var text = NotableServicesTexts.IntroduceLine(1, 50).ToString();

        AssertRenders(text, "one");
        StringAssert.StartsWith(text, "Introduce me to the other notable here (50");
        Assert.IsFalse(text.Contains("notables"), text);
    }

    [TestMethod]
    public void IntroduceLine_ForSeveralNotables_IsPlural_AndCarriesThePrice()
    {
        var text = NotableServicesTexts.IntroduceLine(2, 100).ToString();

        AssertRenders(text, "two");
        StringAssert.StartsWith(text, "Introduce me to the other notables here (100");
    }

    [TestMethod]
    public void RevealLine_CarriesThePrice()
    {
        var text = NotableServicesTexts.RevealLine(100).ToString();

        AssertRenders(text, "reveal");
        StringAssert.StartsWith(text, "Point me to the holds near here (100");
    }
}
