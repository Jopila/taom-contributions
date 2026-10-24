using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HearthRoll;

namespace TAOM.Tests.Features.HearthRoll;

/// <summary>The two age lines. An inline <c>{=key}default</c> text renders its default without the game; it constructs <c>TextObject</c>, so the reference-assembly run skips it.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HearthRollTextsTests
{
    [TestMethod]
    public void OneYear_UsesTheSingularLine()
        => Assert.AreEqual("1 year old", HearthRollTexts.Age(1).ToString());

    [DataTestMethod]
    [DataRow(0, "0 years old")]
    [DataRow(2, "2 years old")]
    [DataRow(7, "7 years old")]
    [DataRow(12, "12 years old")]
    public void AnyOtherCount_UsesThePluralLine(int years, string expected)
        => Assert.AreEqual(expected, HearthRollTexts.Age(years).ToString());

    [TestMethod]
    public void TheKeys_AreTheTwoRegisteredOnes()
    {
        var source = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/Features/HearthRoll/HearthRollTexts.cs", stripComments: true);

        StringAssert.Contains(source, "{=taom_hr_age_one}{AGE} year old");
        StringAssert.Contains(source, "{=taom_hr_age_many}{AGE} years old");
    }
}
