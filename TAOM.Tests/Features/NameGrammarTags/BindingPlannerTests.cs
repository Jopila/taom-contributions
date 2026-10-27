using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.NameGrammarTags;

namespace TAOM.Tests.Features.NameGrammarTags;

[TestClass]
public class BindingPlannerTests
{
    [TestMethod]
    public void Plan_TwoKingdomSlots_GetIndexesZeroAndOne()
    {
        var plan = BindingPlanner.Plan(new[] { "KINGDOM", "KINGDOM_TO_CALL_TO_WAR_AGAINST", "KINGDOM_LINK" });
        Assert.AreEqual(0, plan.Single(b => b.Variable == "KINGDOM").Index);
        Assert.AreEqual(1, plan.Single(b => b.Variable == "KINGDOM_TO_CALL_TO_WAR_AGAINST").Index);
        Assert.AreEqual(0, plan.Single(b => b.Variable == "KINGDOM_LINK").Index);
    }

    [TestMethod]
    public void Plan_OneVariableOfEachKind_CountsIndexesPerKind()
    {
        var plan = BindingPlanner.Plan(new[] { "KINGDOM", "CLAN_NAME", "SETTLEMENT" });
        Assert.IsTrue(plan.All(b => b.Index == 0));
    }

    [TestMethod]
    public void Plan_OtherVariables_AreNotBound()
    {
        var plan = BindingPlanner.Plan(new[] { "RULER.NAME", "GOLD_AMOUNT", "KINGDOM" });
        CollectionAssert.AreEqual(new[] { "KINGDOM" }, plan.Select(b => b.Variable).ToArray());
    }

    [TestMethod]
    public void Plan_FactionVariable_IsMarkedAsAFaction()
    {
        var plan = BindingPlanner.Plan(new[] { "MAP_FACTION_NAME", "KINGDOM" });
        Assert.IsTrue(plan.Single(b => b.Variable == "MAP_FACTION_NAME").IsFaction);
        Assert.IsFalse(plan.Single(b => b.Variable == "KINGDOM").IsFaction);
    }
}
