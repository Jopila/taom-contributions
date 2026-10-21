using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.NamedCompanions.Domain;
using TAOM.Features.NamedCompanions.Oaths;
using TAOM.Features.NamedCompanions.Oaths.Hooks;

namespace TAOM.Tests.Features.NamedCompanions.Oaths;

[TestClass]
public class OathTextRendererTests
{
    private static string Name(string id) => id == "mithril" ? "Mithril Ore" : null;

    private static HireTermsDefinition Items(params (string id, int count)[] items)
    {
        var terms = new HireTermsDefinition { Items = new List<ItemCostDefinition>() };
        foreach (var (id, count) in items) terms.Items.Add(new ItemCostDefinition { ItemId = id, Count = count });
        return terms;
    }

    [TestMethod]
    public void Terms_GoldThenEachItem()
    {
        var text = OathTextRenderer.Terms(Items(("mithril", 2), ("ale", 5)), 3000, Name);

        Assert.AreEqual("3000{GOLD_ICON}, 2 x Mithril Ore, 5 x ale", text);
    }

    [TestMethod]
    public void Terms_ItemsOnly_OmitsTheGoldPart()
    {
        Assert.AreEqual("1 x Mithril Ore", OathTextRenderer.Terms(Items(("mithril", 1)), 0, Name));
    }

    [TestMethod]
    public void Terms_UnknownName_FallsBackToTheItemId()
    {
        Assert.AreEqual("4 x ale", OathTextRenderer.Terms(Items(("ale", 4)), 0, _ => string.Empty));
        Assert.AreEqual("4 x ale", OathTextRenderer.Terms(Items(("ale", 4)), 0, null));
    }

    [TestMethod]
    public void Terms_NothingToList_IsNullNotEmpty()
    {
        Assert.IsNull(OathTextRenderer.Terms(Items(), 0, Name));
        Assert.IsNull(OathTextRenderer.Terms(null, 0, Name));
    }

    [TestMethod]
    public void Missing_ListsGoldAndItemsInOrder()
    {
        var missing = new List<MissingTerm>
        {
            new() { Kind = MissingTermKind.Gold, Count = 500 },
            new() { Kind = MissingTermKind.Item, ItemId = "mithril", Count = 1 },
        };

        Assert.AreEqual("500{GOLD_ICON}, 1 x Mithril Ore", OathTextRenderer.Missing(missing, Name));
    }
}
