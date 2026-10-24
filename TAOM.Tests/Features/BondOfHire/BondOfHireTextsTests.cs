using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BondOfHire;
using TAOM.Features.BondOfHire.UI;

namespace TAOM.Tests.Features.BondOfHire;

/// <summary>
/// The player text of the Clans panel line (#868). An inline <c>{=key}default</c> text renders its default without the game.
/// The subject is always the mercenaries, never the player, and no word is gendered.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class BondOfHireTextsTests
{
    [TestMethod]
    public void Term_SeveralDays_SaysAtLeastNMoreDays()
    {
        Assert.AreEqual("These mercenaries agreed to serve for at least 30 more days",
            BondOfHireTexts.Term(new ContractTerm(ContractTermKind.Known, 30)));
        Assert.AreEqual("These mercenaries agreed to serve for at least 2 more days",
            BondOfHireTexts.Term(new ContractTerm(ContractTermKind.Known, 2)));
    }

    [TestMethod]
    public void Term_OneDay_IsSingular()
    {
        Assert.AreEqual("These mercenaries agreed to serve for at least 1 more day",
            BondOfHireTexts.Term(new ContractTerm(ContractTermKind.Known, 1)));
    }

    [TestMethod]
    public void Term_Unknown_SaysItIsNotKnown()
    {
        Assert.AreEqual("It is not known how long these mercenaries will serve",
            BondOfHireTexts.Term(new ContractTerm(ContractTermKind.Unknown, 0)));
    }

    [TestMethod]
    public void Term_Hidden_IsEmpty()
    {
        Assert.AreEqual("", BondOfHireTexts.Term(new ContractTerm(ContractTermKind.Hidden, 0)));
    }
}
