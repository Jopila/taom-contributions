using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #833: the in-person desert line keeps the original, already translated key and composes the cost
/// sentence from its own key. (The wait-menu prompt is pinned in EnlistmentWaitMenuPresenterTests.)
/// </summary>
[TestClass]
public class DesertionCostTextTests
{
    [TestMethod]
    public void TheInPersonDesertLine_KeepsTheOriginalKeyAndComposesTheCost()
    {
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/Hooks/EnlistmentReleaseDialogBehavior.cs", stripComments: true);
        StringAssert.Contains(src, "{=taom_enlist_release_desert}");
        StringAssert.Contains(src, "{=taom_enlist_desert_cost_reply}");
        StringAssert.Contains(src, "{LINE} {COST}");
    }

    [TestMethod]
    public void BothCostKeys_AreRegistered_WithGenderNeutralText()
    {
        var xml = RepoPaths.ReadSource("Main/_Module/ModuleData/taom_enlistment_strings.xml");
        StringAssert.Contains(xml, "{=taom_enlist_desert_cost}Deserting will also cost you your commander's favor and your good name.");
        StringAssert.Contains(xml, "{=taom_enlist_desert_cost_reply}(This will cost me my commander's favor and my good name.)");
    }
}
