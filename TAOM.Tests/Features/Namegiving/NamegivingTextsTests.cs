using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Namegiving;
using TAOM.Features.Namegiving.UI;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Namegiving;

/// <summary>
/// The player text of Namegiving. An inline <c>{=key}default</c> text renders its default without the game, so these run
/// headless on the desktop; they construct <c>TextObject</c>, so the reference-assembly run skips them.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class NamegivingTextsTests
{
    [TestMethod]
    public void Button_ReadsRename()
        => Assert.AreEqual("Rename", NamegivingTexts.Button().ToString());

    [TestMethod]
    public void PickerTitleBodyAndEntry_ReadAsDecided()
    {
        Assert.AreEqual("Rename a Fief", NamegivingTexts.PickTitle());
        Assert.AreEqual("Choose the fief or village to rename.", NamegivingTexts.PickBody());
        Assert.AreEqual("Aldburg (2000 influence)", NamegivingTexts.Entry("Aldburg", 2000));
    }

    [TestMethod]
    public void InputTitleAndBody_NameThePlaceAndThePrice()
    {
        Assert.AreEqual("Rename Aldburg", NamegivingTexts.InputTitle("Aldburg"));
        Assert.AreEqual("Enter a new name. It costs 500 influence.", NamegivingTexts.InputBody(500));
    }

    [TestMethod]
    public void ConfirmAndDone_NameBothNames()
    {
        Assert.AreEqual("Confirm the New Name", NamegivingTexts.ConfirmTitle());
        Assert.AreEqual("Aldburg will be known as Meduseld. This costs 2000 influence.", NamegivingTexts.ConfirmBody("Aldburg", "Meduseld", 2000));
        Assert.AreEqual("Aldburg is now known as Meduseld.", NamegivingTexts.Done("Aldburg", "Meduseld"));
    }

    [TestMethod]
    public void Block_OwnReasons_ReadAsDecided()
    {
        Assert.AreEqual("Only the ruler of the realm can rename its fiefs.", NamegivingTexts.Block(RenameBlock.NotRuler, ""));
        Assert.AreEqual("This fief no longer belongs to your realm.", NamegivingTexts.Block(RenameBlock.NotRealm, ""));
    }

    [TestMethod]
    public void Block_MapAction_PassesTheEnginesReasonThrough()
        => Assert.AreEqual("This action is disabled while in a mission", NamegivingTexts.Block(RenameBlock.MapAction, "This action is disabled while in a mission"));

    [TestMethod]
    public void Block_HiddenStates_SayNothing()
    {
        Assert.AreEqual("", NamegivingTexts.Block(RenameBlock.None, ""));
        Assert.AreEqual("", NamegivingTexts.Block(RenameBlock.Disabled, ""));
        Assert.AreEqual("", NamegivingTexts.Block(RenameBlock.NotSinglePlayer, ""));
    }

    [TestMethod]
    public void Problem_EachRuleHasItsOwnText()
    {
        Assert.AreEqual("A name must be 1 to 50 characters long.", NamegivingTexts.Problem(NameProblem.Length));
        Assert.AreEqual("A name may hold only letters, digits, spaces and punctuation.", NamegivingTexts.Problem(NameProblem.Characters));
        Assert.AreEqual("A name cannot contain braces or square brackets.", NamegivingTexts.Problem(NameProblem.Brackets));
        Assert.AreEqual("A name cannot start or end with a space.", NamegivingTexts.Problem(NameProblem.Edge));
        Assert.AreEqual("A name cannot contain two spaces in a row.", NamegivingTexts.Problem(NameProblem.Double));
        Assert.AreEqual("That is already its name.", NamegivingTexts.Problem(NameProblem.Same));
        Assert.AreEqual("Another place already bears that name.", NamegivingTexts.Problem(NameProblem.Taken));
        Assert.AreEqual("", NamegivingTexts.Problem(NameProblem.None));
    }

    [TestMethod]
    public void EveryProblem_HasAText()
    {
        foreach (var problem in System.Enum.GetValues(typeof(NameProblem)).Cast<NameProblem>().Where(p => p != NameProblem.None))
            Assert.AreNotEqual("", NamegivingTexts.Problem(problem), problem.ToString());
    }

    [TestMethod]
    public void EveryTextKey_StartsWithTheOwnedPrefix_AndIsUnique()
    {
        var src = RepoPaths.ReadSource("Main/Features/Namegiving/UI/NamegivingTexts.cs", stripComments: true);
        var keys = System.Text.RegularExpressions.Regex.Matches(src, @"\{=([A-Za-z0-9_]+)\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).ToList();

        Assert.IsTrue(keys.Count >= 17, "the texts file holds the decided strings");
        foreach (var key in keys)
            StringAssert.StartsWith(key, "taom_namegiving_", key);
        Assert.AreEqual(keys.Count, keys.Distinct().Count(), "one key per string");
    }

    [TestMethod]
    public void ShortOfInfluence_ReusesVanillasKey_NotANewOne()
    {
        var src = RepoPaths.ReadSource("Main/Features/Namegiving/UI/NamegivingTexts.cs", stripComments: true);

        StringAssert.Contains(src, "str_warning_you_dont_have_enough_influence");
    }
}
