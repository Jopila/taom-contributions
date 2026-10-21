using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Execution;
using TAOM.Features.Namegiving;

namespace TAOM.Tests.Features.Namegiving;

[TestClass]
public class LoreNamesTests
{
    [TestMethod]
    public void All_IsTheFourDecidedPairs()
    {
        var rows = LoreNames.All.Select(r => $"{r.SettlementId}|{r.English}|{r.Side}").ToArray();

        CollectionAssert.AreEquivalent(new[]
        {
            "town_ES2|Minas Ithil|Free",
            "town_DG1|Amon Lanc|Free",
            "town_AN2|Deadmen's Dike|Evil",
            "town_isengard|Treegarth of Orthanc|Free",
        }, rows);
    }

    [TestMethod]
    public void EveryRow_HasAnOwnedPrefixKey_ClassifiesAsLore_AndHasAUniqueKeyAndSettlement()
    {
        foreach (var row in LoreNames.All)
        {
            StringAssert.StartsWith(row.Key, "taom_namegiving_lore_", "saves store the raw {=key} text and Classify reads this prefix: a key outside it reads as a player name, so never rename the prefix (" + row.SettlementId + ")");
            Assert.AreEqual(NameOrigin.Lore, NamegivingService.Classify("{=" + row.Key + "}" + row.English), row.SettlementId);
        }

        Assert.AreEqual(LoreNames.All.Count, LoreNames.All.Select(r => r.Key).Distinct().Count());
        Assert.AreEqual(LoreNames.All.Count, LoreNames.All.Select(r => r.SettlementId).Distinct().Count());
    }

    [TestMethod]
    public void EveryKey_IsTheDecidedOne()
    {
        CollectionAssert.AreEquivalent(new[]
        {
            "taom_namegiving_lore_minas_ithil", "taom_namegiving_lore_amon_lanc",
            "taom_namegiving_lore_deadmens_dike", "taom_namegiving_lore_treegarth",
        }, LoreNames.All.Select(r => r.Key).ToArray());
    }

    [TestMethod]
    public void EveryLoreEnglish_PassesTheNameRules_SoItCouldBeTypedByHand()
    {
        foreach (var row in LoreNames.All)
            Assert.AreEqual(NameProblem.None, NamegivingService.ValidateName(row.English, "x", new string[0]), row.English);
    }

    [TestMethod]
    public void NoRow_IsNeutral()
        => Assert.IsFalse(LoreNames.All.Any(r => r.Side == FactionSide.Neutral));

    [TestMethod]
    public void Find_ReturnsTheRowOrNull()
    {
        Assert.AreEqual("Amon Lanc", LoreNames.Find("town_DG1")!.English);
        Assert.IsNull(LoreNames.Find("town_A"));
        Assert.IsNull(LoreNames.Find(null));
    }
}
