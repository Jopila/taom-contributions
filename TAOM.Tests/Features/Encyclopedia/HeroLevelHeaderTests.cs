using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Encyclopedia;

namespace TAOM.Tests.Features.Encyclopedia;

/// <summary>The hero page shows "Lvl: N, Skills:" from the engine's coming-of-age and never when information is hidden (#907).</summary>
[TestClass]
public class HeroLevelHeaderTests
{
    [TestMethod]
    public void ShouldShowLevel_JustBelowComingOfAge_IsFalse()
        => Assert.IsFalse(HeroLevelHeader.ShouldShowLevel(17.9f, 18, false));

    [TestMethod]
    public void ShouldShowLevel_AtComingOfAge_IsTrue()
        => Assert.IsTrue(HeroLevelHeader.ShouldShowLevel(18f, 18, false));

    [TestMethod]
    public void ShouldShowLevel_FollowsTheEnginesComingOfAge()
    {
        Assert.IsFalse(HeroLevelHeader.ShouldShowLevel(20f, 21, false));
        Assert.IsTrue(HeroLevelHeader.ShouldShowLevel(21f, 21, false));
    }

    [TestMethod]
    public void ShouldShowLevel_HiddenInformation_IsFalse()
        => Assert.IsFalse(HeroLevelHeader.ShouldShowLevel(40f, 18, true));

    [TestMethod]
    public void ShouldShowLevel_NaNAge_IsFalse()
        => Assert.IsFalse(HeroLevelHeader.ShouldShowLevel(float.NaN, 18, false));
}
