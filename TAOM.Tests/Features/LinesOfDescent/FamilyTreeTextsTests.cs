using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Localization;
using TAOM.Features.LinesOfDescent;
using TAOM.Features.LinesOfDescent.UI;

namespace TAOM.Tests.Features.LinesOfDescent;

/// <summary>
/// The phrase renderer of Lines of Descent. An inline <c>{=key}default</c> text renders its default without the game, and the
/// hero binder is swapped for a fake that sets the name variables, so these run headless on the desktop; they construct
/// <c>TextObject</c>, so the reference-assembly run skips them.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FamilyTreeTextsTests
{
    [TestInitialize]
    public void SetUp()
        => FamilyTreeTexts.BindHero = (text, tag, id) => text.SetTextVariable(tag, new TextObject("{NAME}").SetTextVariable("NAME", id.ToUpperInvariant()));

    [TestCleanup]
    public void Cleanup() => FamilyTreeTexts.BindHero = FamilyTreeTexts.DefaultBind;

    [TestMethod]
    public void TheFixedLines_ReadAsDecided()
    {
        Assert.AreEqual("Family Tree", FamilyTreeTexts.Button().ToString());
        Assert.AreEqual("The Line of ARAGORN", FamilyTreeTexts.Title("aragorn").ToString());
        Assert.AreEqual("Kin of ARWEN", FamilyTreeTexts.Kin("arwen").ToString());
        Assert.AreEqual("12 more not shown", FamilyTreeTexts.More(12).ToString());
        Assert.AreEqual("See above", FamilyTreeTexts.Repeat().ToString());
    }

    [TestMethod]
    public void EveryRelationWord_ReadsAsDecided()
    {
        Assert.AreEqual("father", FamilyTreeTexts.Word(RelationKind.Father));
        Assert.AreEqual("mother", FamilyTreeTexts.Word(RelationKind.Mother));
        Assert.AreEqual("son", FamilyTreeTexts.Word(RelationKind.Son));
        Assert.AreEqual("daughter", FamilyTreeTexts.Word(RelationKind.Daughter));
        Assert.AreEqual("husband", FamilyTreeTexts.Word(RelationKind.Husband));
        Assert.AreEqual("wife", FamilyTreeTexts.Word(RelationKind.Wife));
        Assert.AreEqual("brother", FamilyTreeTexts.Word(RelationKind.Brother));
        Assert.AreEqual("sister", FamilyTreeTexts.Word(RelationKind.Sister));
        Assert.AreEqual("former husband", FamilyTreeTexts.Word(RelationKind.ExHusband));
        Assert.AreEqual("former wife", FamilyTreeTexts.Word(RelationKind.ExWife));
    }

    [TestMethod]
    public void OneStep_UpperCasesTheWord_AndNamesTheTarget()
    {
        var phrase = new RelationPhrase(new[] { new RelationStep(RelationKind.Brother, "boromir") });
        Assert.AreEqual("Brother of BOROMIR", FamilyTreeTexts.Render(phrase).ToString());
    }

    [TestMethod]
    public void TwoSteps_ChainWithOnlyTheFirstWordUpperCased()
    {
        var phrase = new RelationPhrase(new[]
        {
            new RelationStep(RelationKind.Husband, "z"),
            new RelationStep(RelationKind.Daughter, "w", "w", "v"),
        });
        Assert.AreEqual("Husband of Z, daughter of W and V", FamilyTreeTexts.Render(phrase).ToString());
    }

    [TestMethod]
    public void ThreeSteps_NestTheChain()
    {
        var phrase = new RelationPhrase(new[]
        {
            new RelationStep(RelationKind.Son, "y"),
            new RelationStep(RelationKind.Son, "z"),
            new RelationStep(RelationKind.Daughter, "w"),
        });
        Assert.AreEqual("Son of Y, son of Z, daughter of W", FamilyTreeTexts.Render(phrase).ToString());
    }

    [TestMethod]
    public void KinPhrase_RendersTheKinLine()
        => Assert.AreEqual("Kin of ELROND", FamilyTreeTexts.Render(RelationPhrase.Kin("elrond")).ToString());
}
