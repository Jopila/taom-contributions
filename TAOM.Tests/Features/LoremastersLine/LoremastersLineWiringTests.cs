using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Bannerlord.UIExtenderEx.Attributes;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.GauntletUI.ExtraWidgets;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.LoremastersLine;
using TAOM.Features.LoremastersLine.UI;
using TAOM.Tests.Migration;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.LoremastersLine;

/// <summary>
/// Source and attribute pins for the Loremaster's Line (#877). A binding that names nothing is a silent no-op in
/// Gauntlet, so every name in the inserted XML must resolve to a mixin member, a vanilla skill view model member or a
/// property of the engine's fill widget, and the two sprites must be declared by TAOM's sprite sheet. The hover wiring
/// (the hint on the band, the band passing no events to its children) is pinned on the XML, since only the game can
/// show a tooltip.
/// </summary>
[TestClass]
public class LoremastersLineWiringTests
{
    private const string MixinFile = "Client/Features/LoremastersLine/UI/LoremastersLineSkillMixin.cs";
    private const string AccessFile = "Client/Features/LoremastersLine/UI/LoremastersLineSkillAccess.cs";
    private const string SpriteData = "Main/_Module/GUI/TAOMSpriteData.xml";
    private static readonly string[] Sprites =
    {
        @"CharacterDeveloper\learning_rate_limit_triangle",
        @"CharacterDeveloper\progress_bar_fill",
    };

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    private static XmlElement Root()
    {
        var instance = Activator.CreateInstance(typeof(LoremastersLinePrefab), nonPublic: true)!;
        var method = typeof(LoremastersLinePrefab).GetMethods().Single(m => m.CustomAttributes
            .Any(a => a.AttributeType.Name == "PrefabExtensionXmlDocumentAttribute"));
        return ((XmlDocument)method.Invoke(instance, null)!).DocumentElement!;
    }

    private static IEnumerable<XmlElement> All() => new[] { Root() }.Concat(Root().SelectNodes(".//*")!.Cast<XmlElement>());

    private static XmlElement ById(string id) => (XmlElement)Root().SelectSingleNode("descendant-or-self::*[@Id='" + id + "']")!;

    // The widget a node sits in: its parent is the Children element, whose parent is the widget.
    private static XmlElement WidgetParent(XmlElement element) => (XmlElement)element.ParentNode!.ParentNode!;

    [TestMethod]
    public void TheMixin_HooksRefreshWithCurrentValues_OnSkillVM()
    {
        var attribute = CustomAttributeData.GetCustomAttributes(typeof(LoremastersLineSkillMixin)).Single(a => a.AttributeType.Name == "ViewModelMixinAttribute");

        Assert.AreEqual("RefreshWithCurrentValues", attribute.ConstructorArguments[0].Value);
        Assert.AreEqual(typeof(SkillVM), typeof(LoremastersLineSkillMixin).BaseType!.GetGenericArguments().Single());
        Assert.IsNotNull(typeof(SkillVM).GetMethod("RefreshWithCurrentValues"));
    }

    [TestMethod]
    public void ThePrefab_InsertsAsChildIndexThree_OfThePerkSelectionBar_BeforeThePercentageIndicator()
    {
        var attribute = (PrefabExtensionAttribute)typeof(LoremastersLinePrefab).GetCustomAttributes(typeof(PrefabExtensionAttribute), false).Single();
        var prefab = (Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch)Activator.CreateInstance(typeof(LoremastersLinePrefab), true)!;

        Assert.AreEqual("CharacterDeveloper", attribute.Movie);
        Assert.AreEqual("descendant::PerkSelectionBarWidget[@Id='PerkSelectionBarWidget']/Children", attribute.XPath);
        Assert.AreEqual(Bannerlord.UIExtenderEx.Prefabs2.InsertType.Child, prefab.Type);
        Assert.AreEqual(3, prefab.Index);
    }

    [TestMethod]
    public void TheInsertedRoot_IsTheEnginesFillBar_BoundToTheMixin_AndIgnoresEvents()
    {
        var root = Root();

        Assert.AreEqual("FillBarHorizontalWidget", root.Name);
        Assert.AreEqual("true", root.GetAttribute("DoNotAcceptEvents"), "the root is never a hover target itself; the band inside it is");
        Assert.AreEqual("@HasLoremastersLine", root.GetAttribute("IsVisible"));
        Assert.AreEqual("Band", root.GetAttribute("FillWidget"));
        Assert.AreEqual("@LoremastersLineBarLevels", root.GetAttribute("MaxAmount"));
        Assert.AreEqual("@LoremastersLineBandLevels", root.GetAttribute("InitialAmount"));
        Assert.AreNotEqual("true", root.GetAttribute("IsDirectionRightward"), "the band hangs off the right edge of the bar");
        CollectionAssert.AreEquivalent(new[] { "TaomLoremastersLine", "Band", "BandClip", "BandFill", "Arrow", "BandHint" },
            All().Select(e => e.GetAttribute("Id")).Where(i => i.Length > 0).ToArray());
    }

    [TestMethod]
    public void EveryAttributeOnTheRoot_IsAPropertyOfTheFillBar_AndEveryBindingResolves()
    {
        var root = Root();
        var widgetProperties = typeof(FillBarHorizontalWidget).GetProperties().Select(p => p.Name).ToHashSet();
        var mixinProperties = typeof(LoremastersLineSkillMixin)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.CustomAttributes.Any(a => a.AttributeType.Name == "DataSourceProperty"))
            .Select(p => p.Name).ToHashSet();

        foreach (var attribute in root.Attributes.Cast<XmlAttribute>().Where(a => a.Name != "Id"))
            Assert.IsTrue(widgetProperties.Contains(attribute.Name), "the fill bar has no property " + attribute.Name);

        var bindings = All().SelectMany(e => e.Attributes.Cast<XmlAttribute>()).Select(a => a.Value)
            .Where(v => v.StartsWith("@") || (v.StartsWith("{") && v.EndsWith("}"))).Select(v => v.Trim('@', '{', '}')).Distinct().ToList();
        CollectionAssert.AreEquivalent(
            new[] { "HasLoremastersLine", "LoremastersLineBandLevels", "LoremastersLineBarLevels", "LoremastersLineTooltip" }, bindings);
        foreach (var name in bindings)
            Assert.IsTrue(mixinProperties.Contains(name), name + " is not a DataSourceProperty of the mixin");

        // The mixin and the fill widget name the same quantities: ints, as MaxAmount and InitialAmount are.
        CollectionAssert.AreEquivalent(bindings, mixinProperties.ToArray());
        Assert.AreEqual(typeof(BasicTooltipViewModel), typeof(LoremastersLineSkillMixin).GetProperty("LoremastersLineTooltip")!.PropertyType);
        Assert.AreEqual(typeof(int), typeof(LoremastersLineSkillMixin).GetProperty("LoremastersLineBarLevels")!.PropertyType);
        Assert.AreEqual(typeof(int), typeof(LoremastersLineSkillMixin).GetProperty("LoremastersLineBandLevels")!.PropertyType);
        Assert.AreEqual(typeof(int), typeof(FillBarHorizontalWidget).GetProperty("MaxAmount")!.PropertyType);
        Assert.AreEqual(typeof(int), typeof(FillBarHorizontalWidget).GetProperty("InitialAmount")!.PropertyType);
    }

    [TestMethod]
    public void TheFillWidgetReference_PointsAtTheBand_WhichClipsAFixedWideRightAlignedFill()
    {
        var band = ById("Band");
        var fill = ById("BandFill");

        Assert.IsNotNull(band);
        Assert.AreEqual("TaomLoremastersLine", WidgetParent(band).GetAttribute("Id"));
        Assert.AreEqual("BandClip", WidgetParent(fill).GetAttribute("Id"));
        Assert.AreEqual("true", ById("BandClip").GetAttribute("ClipContents"));
        Assert.AreEqual("Fixed", fill.GetAttribute("WidthSizePolicy"));
        Assert.AreEqual("1000", fill.GetAttribute("SuggestedWidth"));
        Assert.AreEqual("Right", fill.GetAttribute("HorizontalAlignment"));
    }

    [TestMethod]
    public void TheHint_IsAChildOfTheBand_AndTheBandPassesNoEventsToItsChildren()
    {
        var hint = ById("BandHint");
        var band = ById("Band");

        Assert.AreEqual("HintWidget", hint.Name);
        Assert.AreEqual("Band", WidgetParent(hint).GetAttribute("Id"), "the hint relays only its parent's hover, so the band is the hover target");
        Assert.AreEqual("{LoremastersLineTooltip}", hint.GetAttribute("DataSource"));
        Assert.AreEqual("ExecuteBeginHint", hint.GetAttribute("Command.HoverBegin"));
        Assert.AreEqual("ExecuteEndHint", hint.GetAttribute("Command.HoverEnd"));
        Assert.IsFalse(hint.HasAttribute("IsEnabled"), "the HintWidget constructor already disables it");
        Assert.AreEqual("StretchToParent", hint.GetAttribute("WidthSizePolicy"));
        Assert.AreEqual("StretchToParent", hint.GetAttribute("HeightSizePolicy"));

        // CollectEnableWidgetsAt gathers children first: an enabled child of the band would take the hover from it.
        Assert.AreEqual("true", band.GetAttribute("DoNotPassEventsToChildren"));
        Assert.IsFalse(band.HasAttribute("IsEnabled"), "a disabled band cannot be hovered");
        Assert.IsFalse(band.HasAttribute("IsDisabled"), "a disabled band cannot be hovered");
        Assert.IsFalse(band.HasAttribute("DoNotAcceptEvents"), "the band is the hover target, so it must accept events");
    }

    [TestMethod]
    public void TheBand_SpansTheBarsHeight_WithNoVerticalOffset()
    {
        var band = ById("Band");

        Assert.AreEqual("StretchToParent", band.GetAttribute("HeightSizePolicy"));
        Assert.IsFalse(band.HasAttribute("PositionYOffset"), "an offset band would leave part of the bar unhoverable");
    }

    [TestMethod]
    public void TheArrow_IsDecoration_FlippedToPointAtTheBar()
    {
        var arrow = ById("Arrow");

        Assert.AreEqual(0, arrow.SelectNodes("*")!.Count, "the arrow is decoration: the band carries the hint");
        Assert.AreEqual("false", arrow.GetAttribute("IsEnabled"));
        Assert.AreEqual("true", arrow.GetAttribute("VerticalFlip"), "TAOM's triangle points up");
        Assert.AreEqual("-30", arrow.GetAttribute("PositionYOffset"));
        Assert.AreEqual("-15", arrow.GetAttribute("PositionXOffset"));
        Assert.AreEqual("Band", WidgetParent(arrow).GetAttribute("Id"));
    }

    [TestMethod]
    public void BothSprites_AreDeclaredByTaomsSpriteSheet_AsAPartAndAGenericSprite()
    {
        var doc = XDocument.Parse(ReadSource(SpriteData));
        var parts = doc.Descendants("SpritePart").Select(p => (string?)p.Element("Name")).ToList();
        var generics = doc.Descendants("GenericSprite").Select(p => (string?)p.Element("Name")).ToList();

        CollectionAssert.AreEquivalent(Sprites, All().Select(e => e.GetAttribute("Sprite")).Where(s => s.Length > 0).Distinct().ToList());
        foreach (var sprite in Sprites)
        {
            CollectionAssert.Contains(parts, sprite, "TAOMSpriteData.xml declares no SpritePart '" + sprite + "'");
            CollectionAssert.Contains(generics, sprite, "TAOMSpriteData.xml declares no GenericSprite '" + sprite + "'");
        }
    }

    [TestMethod]
    public void TheHeroItem_IsReadByAGuardedFieldRef_AndTheRateByTheGameModel()
    {
        var access = ReadSource(AccessFile, stripComments: true);
        var mixin = ReadSource(MixinFile, stripComments: true);

        StringAssert.Contains(access, "AccessTools.FieldRefAccess<SkillVM, CharacterDeveloperHeroItemVM>(\"_heroItem\")");
        StringAssert.Contains(access, "catch (Exception ex)", "a missing field must leave the screen vanilla, not throw from the type initializer");
        StringAssert.Contains(access, "CalculateLearningRate(");
        StringAssert.Contains(mixin, "GetLearningLimitTooltip(");
        foreach (var source in new[] { access, mixin })
            Assert.IsFalse(source.Contains("#region") || source.Contains("[Obsolete"));
    }

    [TestMethod]
    public void EveryEngineCalledMixinMember_RunsInsideATryCatchThatNamesTheException()
    {
        var mixin = ReadSource(MixinFile, stripComments: true);

        foreach (var signature in new[]
                 {
                     "public LoremastersLineSkillMixin(",
                     "public override void OnRefresh()",
                     "private List<TooltipProperty> GetLimitTooltip()",
                     "private List<TooltipProperty> GetLineTooltip()",
                 })
        {
            var start = mixin.IndexOf(signature, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, signature);
            var body = Regex.Split(mixin.Substring(start), @"\r?\n    \}\r?\n")[0];

            StringAssert.Contains(body, "try", signature + " has no guard");
            StringAssert.Contains(body, "catch (Exception ex)", signature + " has no guard that can log the cause");
        }

        Assert.IsFalse(Regex.IsMatch(mixin, @"catch\s*\{"), "a catch without a variable cannot log the cause");
    }

    [TestMethod]
    public void TheLogLatches_AreProcessWide_SoOneFaultLogsOnceNotOncePerSkill()
    {
        var mixin = ReadSource(MixinFile, stripComments: true);

        Assert.IsTrue(Regex.IsMatch(mixin, @"private static readonly HashSet<string> RefreshFailuresLogged\b"));
        Assert.IsTrue(Regex.IsMatch(mixin, @"private static readonly HashSet<string> TooltipFailuresLogged\b"));
        Assert.IsFalse(Regex.IsMatch(mixin, @"private readonly HashSet<string>"), "an instance latch logs the same fault once per skill view model");
    }

    [TestMethod]
    public void TheExtraLimitLine_ShowsOnlyWhileTheMarkerShows()
    {
        var mixin = ReadSource(MixinFile, stripComments: true);
        var start = mixin.IndexOf("private List<TooltipProperty> GetLimitTooltip()", StringComparison.Ordinal);
        var body = Regex.Split(mixin.Substring(start), @"\r?\n    \}\r?\n")[0];

        Assert.IsTrue(Regex.IsMatch(body, @"if \(HasLoremastersLine\)\s*result\.Add\(LoremastersLineTooltips\.LimitLine\(\)\);"),
            "the line explains the red marker, so it follows the marker's flag");
    }

    [TestMethod]
    public void TheWidths_AreSetBeforeTheFlagTurnsTrue_AndOnlyForABarOfPositiveWidth()
    {
        var mixin = ReadSource(MixinFile, stripComments: true);

        var bar = mixin.IndexOf("LoremastersLineBarLevels = LearningFloorCalculator.BarLevels(", StringComparison.Ordinal);
        var band = mixin.IndexOf("LoremastersLineBandLevels = LearningFloorCalculator.BandLevels(", StringComparison.Ordinal);
        var flag = mixin.IndexOf("HasLoremastersLine = onBar;", StringComparison.Ordinal);

        Assert.IsTrue(bar >= 0 && band >= 0 && flag >= 0);
        Assert.IsTrue(bar < flag && band < flag);
        StringAssert.Contains(mixin, "IsOnBar(_floor, vm.MaxLevel)");
    }

    [TestMethod]
    public void TheTooltipLines_StayInTheClient_WhereTheTooltipPropertyTypeLives()
    {
        Assert.IsFalse(File.Exists(RepoPath("Main", "Features", "LoremastersLine", "LoremastersLineTooltips.cs")));
        Assert.IsTrue(File.Exists(RepoPath("Client", "Features", "LoremastersLine", "UI", "LoremastersLineTooltips.cs")));
        foreach (var file in Directory.GetFiles(RepoPath("Main", "Features", "LoremastersLine"), "*.cs"))
            Assert.IsFalse(File.ReadAllText(file).Contains("TooltipProperty"), Path.GetFileName(file) + ": the headless server never loads it");
    }

    [TestMethod]
    public void EveryFile_StaysUnder150Lines()
    {
        var files = Directory.GetFiles(RepoPath("Main", "Features", "LoremastersLine"), "*.cs")
            .Concat(Directory.GetFiles(RepoPath("Client", "Features", "LoremastersLine", "UI"), "*.cs")).ToArray();

        Assert.IsTrue(files.Length >= 7, "calculator, module, settings provider, mixin, skill access, tooltips and the prefab patch");
        foreach (var file in files)
            Assert.IsTrue(File.ReadAllLines(file).Length < 150, "ADR-002: " + Path.GetFileName(file) + " is 150 lines or more");
    }

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<LoremastersLineModule>().Count(),
            "LoremastersLineModule must be listed exactly once in Main/Composition/FeatureModules.cs");
    }

    [TestMethod]
    public void TheModule_IsNamedLoremastersLine_NotParked_OwnsNoSaveData_DeclaresNothingElse()
    {
        var module = new LoremastersLineModule();

        Assert.AreEqual("LoremastersLine", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "the line is read-only UI");
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.PatchCategories.Count, "no Harmony: the feature is a mixin and a prefab insertion");
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void TheModulesServices_ResolveFromARealContainer()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        new LoremastersLineModule().RegisterServices(container);

        Assert.IsNotNull(container.Resolve<LoremastersLineSettingsProvider>());
        Assert.AreSame(container.Resolve<LoremastersLineSettingsProvider>(), container.Resolve<LoremastersLineSettingsProvider>());
    }

    [TestMethod]
    public void IoC_DoesNotRegisterTheFeatureByHand()
    {
        var src = ReadSource("Main/IoC.cs", stripComments: true) + ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("LoremastersLine"), "the module registers the provider; UIExtenderEx finds the mixin and prefab by assembly scan");
    }
}
