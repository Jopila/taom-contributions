using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HardQuestions.Domain;

namespace TAOM.Tests.Features.HardQuestions;

/// <summary>Which slots a fresh kit may fill on release: never a piece the lord kept, never a gap in the kit.</summary>
[TestClass]
public class KitSlotRuleTests
{
    private const string Rag = "torn_bandit_clothes";

    [TestMethod]
    public void CanFill_AnEmptySlotWithAKitPiece_IsTrue()
    {
        Assert.IsTrue(KitSlotRule.CanFill(null, "sword", Rag));
        Assert.IsTrue(KitSlotRule.CanFill("", "sword", Rag));
    }

    [TestMethod]
    public void CanFill_ASlotHoldingTheRag_IsTrue()
    {
        Assert.IsTrue(KitSlotRule.CanFill(Rag, "tunic", Rag));
    }

    [TestMethod]
    public void CanFill_ASlotHoldingAKeptNamedPiece_IsFalse()
    {
        Assert.IsFalse(KitSlotRule.CanFill("named_sword", "sword", Rag));
    }

    [TestMethod]
    public void CanFill_AnEmptyKitSlot_IsFalse()
    {
        Assert.IsFalse(KitSlotRule.CanFill(null, null, Rag));
        Assert.IsFalse(KitSlotRule.CanFill(Rag, "", Rag));
    }
}
