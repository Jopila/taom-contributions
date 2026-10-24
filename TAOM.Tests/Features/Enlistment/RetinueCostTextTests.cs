using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Presentation;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>#839: ONE key states the cost of the player's own men, on the board and in the commander's oath question.</summary>
[TestClass]
public class RetinueCostTextTests
{
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Build_BindsBothNumbersAndTheCoinOnTheTextObject()
    {
        var text = RetinueCostText.Build(180, 14);

        StringAssert.Contains(text.Value, "{=taom_enlist_retinue_cost}Your own men cost {COST}{COIN} a day; service pays {PAY}{COIN}.");
        Assert.AreEqual("180", text.Attributes["COST"]);
        Assert.AreEqual("14", text.Attributes["PAY"]);
        Assert.IsNotNull(text.Attributes["COIN"]);
    }

    [TestMethod]
    public void TheKey_IsRegisteredOnce_AndTheOldKeysAreGone()
    {
        var xml = RepoPaths.ReadSource("Main/_Module/ModuleData/taom_enlistment_strings.xml");
        Assert.AreEqual(1, Regex.Matches(xml, "id=\"taom_enlist_retinue_cost\"").Count);
        StringAssert.Contains(xml, "{=taom_enlist_retinue_cost}Your own men cost {COST}{COIN} a day; service pays {PAY}{COIN}.");
        Assert.IsFalse(xml.Contains("taom_enlist_board_retinue"));
        Assert.IsFalse(xml.Contains("taom_enlist_oath_retinue"));
    }

    [TestMethod]
    public void TheOathQuestion_WrapsTheUnchangedAskKey_AndTheLineIsSetByItsCondition()
    {
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/Hooks/EnlistmentDialogBehavior.cs", stripComments: true);
        StringAssert.Contains(src, "{=!}{OATH_ASK}{TAOM_ENLIST_OATH_RETINUE}");
        StringAssert.Contains(src, "{=taom_enlist_oath_ask}Then swear it: your blade at my side, my bread in your pack, until your term is served. Do you swear?");
        StringAssert.Contains(src, "RetinueCostText.SetOathLine(_gate.OathRetinueCost(PartnerId()))");
    }

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(180, 14)]
    [TestCategory("RequiresGame")]
    public void SetOathLine_AlwaysReturnsTrue_SoItCannotHideTheQuestion(int cost, int pay)
    {
        Assert.IsTrue(RetinueCostText.SetOathLine((cost, pay)));
    }
}
