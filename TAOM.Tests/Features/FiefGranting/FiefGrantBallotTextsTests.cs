using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FiefGranting.UI;

namespace TAOM.Tests.Features.FiefGranting;

/// <summary>
/// The player text of the fief-vote ballot (#866). An inline <c>{=key}default</c> text renders its default without the game.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FiefGrantBallotTextsTests
{
    [TestMethod]
    public void Count_ReadsCurrentFiefs()
    {
        Assert.AreEqual("Current fiefs: 0", FiefGrantBallotTexts.Count(0));
        Assert.AreEqual("Current fiefs: 7", FiefGrantBallotTexts.Count(7));
    }

    [TestMethod]
    public void Hint_ListsTheNamesSortedIgnoringCase_UnderATitle()
    {
        var hint = FiefGrantBallotTexts.Hint(new[] { "minas Tirith", "Aldburg", "dol Amroth" }, null);

        Assert.AreEqual("Fiefs held\nAldburg\ndol Amroth\nminas Tirith", hint);
    }

    [TestMethod]
    public void Hint_ForAClanWithNoFortification_SaysSo()
    {
        Assert.AreEqual("Fiefs held\nHolds no town or castle.", FiefGrantBallotTexts.Hint(new string[0], null));
    }

    [TestMethod]
    public void Hint_NamesTheFiefNotCounted_OnlyWhenGiven()
    {
        Assert.AreEqual(
            "Fiefs held\nEdoras\nNot counting Pelargir, the fief under vote",
            FiefGrantBallotTexts.Hint(new[] { "Edoras" }, "Pelargir"));
        Assert.IsFalse(FiefGrantBallotTexts.Hint(new[] { "Edoras" }, null).Contains("Not counting"));
    }
}
