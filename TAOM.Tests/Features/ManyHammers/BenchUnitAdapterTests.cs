using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Features.ManyHammers;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.ManyHammers;

/// <summary>
/// The two bench adapters need a running campaign for everything but the formula arithmetic, so the engine reads are pinned
/// from the source: the numbers a unit is judged by must come from the installed smithing model and the party roster, and the
/// work must go through the crafting behavior the smithy screen holds.
/// </summary>
[TestClass]
public class BenchUnitAdapterTests
{
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void NetEffect_InputsTake_OutputsGive()
    {
        // Iron2 x2 + charcoal -> Iron3 + Iron1, the vanilla formula at the third step.
        var formula = new Crafting.RefiningFormula(CraftingMaterials.Iron2, 2, CraftingMaterials.Charcoal, 1, CraftingMaterials.Iron3, 1, CraftingMaterials.Iron1, 1);

        var net = RefineBenchUnit.NetEffect(formula);

        Assert.AreEqual(UnitSnapshot.Slots, net.Length);
        Assert.AreEqual(-2, net[(int)CraftingMaterials.Iron2]);
        Assert.AreEqual(-1, net[(int)CraftingMaterials.Charcoal]);
        Assert.AreEqual(1, net[(int)CraftingMaterials.Iron3]);
        Assert.AreEqual(1, net[(int)CraftingMaterials.Iron1]);
        Assert.AreEqual(0, net[(int)CraftingMaterials.IronOre]);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void NetEffect_AnUnusedSecondInput_TouchesNothing()
    {
        // Wood x2 + (Iron1 x0) -> charcoal x3: the zero-count second input must not read as a slot to guard.
        var formula = new Crafting.RefiningFormula(CraftingMaterials.Wood, 2, CraftingMaterials.Iron1, 0, CraftingMaterials.Charcoal, 3);

        var net = RefineBenchUnit.NetEffect(formula);

        Assert.AreEqual(-2, net[(int)CraftingMaterials.Wood]);
        Assert.AreEqual(3, net[(int)CraftingMaterials.Charcoal]);
        Assert.AreEqual(0, net[(int)CraftingMaterials.Iron1]);
        Assert.AreEqual(0, net[(int)CraftingMaterials.IronOre], "the default second output at count 0 gives nothing");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void NetEffect_ANegativeCount_IsIgnoredAsTheEngineDoes()
    {
        var formula = new Crafting.RefiningFormula(CraftingMaterials.Wood, -4, CraftingMaterials.Iron1, 0, CraftingMaterials.Charcoal, -1);

        var net = RefineBenchUnit.NetEffect(formula);

        CollectionAssert.AreEqual(new int[UnitSnapshot.Slots], net);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void NetEffect_AnOutputThatIsAlsoAnInput_NetsInTheSameSlot()
    {
        // Wood x3 in, wood x1 and charcoal x2 out: the wood slot nets to -2, as the doc's known limit describes.
        var formula = new Crafting.RefiningFormula(CraftingMaterials.Wood, 3, CraftingMaterials.Iron1, 0, CraftingMaterials.Wood, 1, CraftingMaterials.Charcoal, 2);

        var net = RefineBenchUnit.NetEffect(formula);

        Assert.AreEqual(-2, net[(int)CraftingMaterials.Wood]);
        Assert.AreEqual(2, net[(int)CraftingMaterials.Charcoal]);
    }

    [TestMethod]
    public void TheConstructors_RefuseNulls()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new SmeltBenchUnit(null!, default, null!));
        Assert.ThrowsException<ArgumentNullException>(() => new RefineBenchUnit(null!, null!, null!));
    }

    [TestMethod]
    public void TheSmeltUnit_ReadsTheModelAndTheRoster_AndDoesItsWorkThroughTheBehavior()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/SmeltBenchUnit.cs", stripComments: true);

        StringAssert.Contains(src, "model.GetEnergyCostForSmelting(item, _hero)");
        StringAssert.Contains(src, "model.GetSmeltingOutputForItem(item)");
        StringAssert.Contains(src, "_behavior.GetHeroCraftingStamina(_hero)");
        StringAssert.Contains(src, "roster.FindIndexOfElement(_weapon)");
        StringAssert.Contains(src, "_behavior.DoSmelting(_hero, _weapon)");
    }

    [TestMethod]
    public void TheRefineUnit_ReadsTheModelAndDoesItsWorkThroughTheBehavior()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/RefineBenchUnit.cs", stripComments: true);

        StringAssert.Contains(src, "model.GetEnergyCostForRefining(ref formula, _hero)");
        StringAssert.Contains(src, "_behavior.GetHeroCraftingStamina(_hero)");
        StringAssert.Contains(src, "_behavior.DoRefinement(_hero, _formula)");
    }

    [TestMethod]
    public void TheStockRead_UsesTheModelsMaterialItems_AndTheMainPartyRoster()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/CraftBenchStock.cs", stripComments: true);

        StringAssert.Contains(src, "model.GetCraftingMaterialItem((CraftingMaterials)slot)");
        StringAssert.Contains(src, "MobileParty.MainParty.ItemRoster");
    }

    [TestMethod]
    public void NoBenchCode_NamesTheSmithingModelOfTheCulturalFeats()
    {
        // The unit cost and the smelt output (a cap on it included) must reach bulk through the model slot, so the adapters
        // never name TaomSmithingModel.
        foreach (var file in new[] { "SmeltBenchUnit", "RefineBenchUnit", "CraftBenchStock" })
            Assert.IsFalse(RepoPaths.ReadSource("Main/Adapters/" + file + ".cs", stripComments: true).Contains("TaomSmithingModel"), file);
    }
}
