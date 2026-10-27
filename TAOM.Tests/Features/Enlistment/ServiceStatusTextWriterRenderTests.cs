using System.Collections;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Localization;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Presentation;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #1019: the board is stored as a flat string, which the menu re-parses as text every frame. A stored string that
/// parsed differently from what was rendered (a leftover brace, a tag) would show on screen, so rendering the variable
/// must give back exactly the stored string, with every optional line present.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ServiceStatusTextWriterRenderTests
{
    private const string Variable = "TAOM_ENLISTMENT_WAIT_TEXT";

    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    private static IDictionary Variables()
    {
        var context = typeof(MBTextManager).GetField("TextContext", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        return (IDictionary)context.GetType().GetField("_variables", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(context);
    }

    private static string Stored() => ((TextObject)Variables()[Variable]).Value;

    [TestMethod]
    public void Write_WhenNewlineWasNeverSet_StillStoresTheBoardWithLineBreaks()
    {
        // The engine sets the NEWLINE global lazily inside its UI builders, and a new campaign clears all globals.
        Variables().Remove("NEWLINE");
        var status = new ServiceStatusModel(
            commanderName: "Aragorn", activity: CommanderActivity.Marching, settlementName: null,
            rank: ServiceRank.Specialist, daysServed: 24, trust: 9, deferredWages: 0, serviceXp: 100, dailyWage: 12,
            nextRank: null, nextRequirementKey: null, nextRequirementTarget: 0, kitWaits: false,
            columnText: null, retinueWage: 0, activeDutyTitle: null);

        new ServiceStatusTextWriter().Write(status);

        StringAssert.Contains(Stored(), "\n");
    }

    [TestMethod]
    public void WriteNotServing_StoresTheUnflattenedTextObject()
    {
        new ServiceStatusTextWriter().WriteNotServing();

        Assert.AreEqual("{=taom_enlist_wait_discharged}Your service has ended.", Stored());
    }

    [TestMethod]
    public void Write_EveryOptionalLine_RendersTheStoredStringUnchanged()
    {
        var status = new ServiceStatusModel(
            commanderName: "Aragorn", activity: CommanderActivity.InBattle, settlementName: "Minas Tirith",
            rank: ServiceRank.Specialist, daysServed: 24, trust: 9, deferredWages: 40, serviceXp: 100, dailyWage: 12,
            nextRank: ServiceRank.Sergeant, nextRequirementKey: "xp", nextRequirementTarget: 200, kitWaits: true,
            columnText: "Marching to Osgiliath", retinueWage: 30, activeDutyTitle: "Scout the ford");

        new ServiceStatusTextWriter().Write(status);
        var rendered = new TextObject("{" + Variable + "}").ToString();

        Assert.AreEqual(Stored(), rendered);
        StringAssert.Contains(rendered, "Toward ");
        StringAssert.Contains(rendered, "Pay owed to you: 40");
        StringAssert.Contains(rendered, "You have orders: Scout the ford");
        StringAssert.Contains(rendered, "Kit to draw");
        StringAssert.Contains(rendered, "Column: Marching to Osgiliath");
        StringAssert.Contains(rendered, "Your own men cost 30");
        Assert.IsFalse(rendered.Contains("{") || rendered.Contains("}"), rendered);
    }
}
