using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.CharacterDeveloper;
using System.IO;

namespace TAOM.Tests.Migration;

/// <summary>
/// Pins the vanilla facts the Loremaster's Line copies or leans on (#877), so an engine bump that changes one goes red
/// instead of drawing the line in the wrong place: the skill bar is MaxLevel + 25 wide, the prefab insertion index is
/// right before the level indicator, and the private hero item is still the character developer's hero item.
/// Each test reads the installed engine IL, data or signature, with no campaign.
/// </summary>
[TestClass]
public class LoremastersLineEngineMirrorTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void BarMaths_StillDividesByMaxLevelPlus25_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = AccessTools.Method(typeof(PerkSelectionBarWidget), "GetXPositionOfLevelOnBar");
        Assert.IsNotNull(target, "PerkSelectionBarWidget.GetXPositionOfLevelOnBar did not resolve.");

        var il = PatchProcessor.GetOriginalInstructions(target!).ToList();

        Assert.IsTrue(il.Any(ci => ci.opcode == OpCodes.Ldc_R4 && ci.operand is float f && f == 25f),
            "GetXPositionOfLevelOnBar no longer loads 25. LearningFloorCalculator.BarOverhang copies the bar's MaxLevel + 25 width.");
        Assert.IsTrue(il.Any(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
                && ci.operand is MethodInfo mi && mi.Name == "get_MaxLevel" && mi.DeclaringType == typeof(PerkSelectionBarWidget)),
            "GetXPositionOfLevelOnBar no longer reads MaxLevel. LearningFloorCalculator.BarOverhang copies the bar's MaxLevel + 25 width.");
        Assert.IsTrue(il.Any(ci => ci.opcode == OpCodes.Div), "GetXPositionOfLevelOnBar no longer divides.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheInstalledSkillBar_StillHasThePercentageIndicatorAsItsFourthChild()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var path = Path.Combine(GameAssemblies.GameDir, "Modules", "SandBox", "GUI", "Prefabs", "CharacterDeveloper", "CharacterDeveloper.xml");
        Assert.IsTrue(File.Exists(path), path);

        // XDocument.Load keeps comments, but Elements() skips them, so the index counts widgets only; Gauntlet loads the
        // prefab with IgnoreComments, which is what the insertion index sees.
        var bar = XDocument.Load(path).Descendants("PerkSelectionBarWidget").Single(e => (string?)e.Attribute("Id") == "PerkSelectionBarWidget");
        var children = bar.Element("Children")!.Elements().ToList();

        Assert.IsTrue(children.Count > 3, "the skill bar has fewer than four children.");
        Assert.AreEqual("PercentageIndicatorWidget", (string?)children[3].Attribute("Id"),
            "LoremastersLinePrefab.Index is 3 so the line draws under the level indicator and over the clips. "
            + "Children now: " + string.Join(", ", children.Select(c => c.Name.LocalName + "#" + (string?)c.Attribute("Id"))));
        Assert.AreEqual("ProgressClip", (string?)children[1].Attribute("Id"));
        Assert.AreEqual("FullLearningRateClipParent", (string?)children[2].Attribute("Id"));
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void SkillVM_StillHoldsItsHeroItemInThePrivateField_AndMaxLevelIsAnIntProperty()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var field = AccessTools.Field(typeof(SkillVM), "_heroItem");

        Assert.IsNotNull(field, "SkillVM._heroItem is gone. LoremastersLineSkillAccess reads it to find the hero's attributes.");
        Assert.IsNotNull(AccessTools.FieldRefAccess<SkillVM, CharacterDeveloperHeroItemVM>("_heroItem"),
            "the typed field reference the mixin binds no longer resolves.");
        Assert.AreEqual(typeof(CharacterDeveloperHeroItemVM), field!.FieldType);
        Assert.AreEqual(typeof(int), typeof(SkillVM).GetProperty("MaxLevel")!.PropertyType);
        Assert.IsNotNull(typeof(CharacterDeveloperHeroItemVM).GetProperty("CharacterAttributes"));
    }
}
