using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Bannerlord.UIExtenderEx.Prefabs2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TAOM.Features.CoopInterop;
using TAOM.Features.SmithsMeasure.UI;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SmithsMeasure;

/// <summary>
/// The XML the Make Tools card inserts into the crafting screen (#910). A binding that names nothing is a silent no-op in
/// Gauntlet, so every <c>@Binding</c>, <c>{DataSource}</c> and <c>Command.*</c> must resolve to a mixin member or a vanilla
/// one. That the XPath still matches the vanilla prefab exactly once is <c>PrefabExtensionBindingTests</c>; the live-install tests
/// below pin where the match sits and that the card reuses vanilla's brushes and names no sprite of its own.
/// </summary>
[TestClass]
public class MakeToolsPrefabTests
{
    private const string XPathText = "descendant::ListPanel[@DataSource='{PlayerCurrentMaterials}']";
    private static readonly Type Vm = typeof(CraftingVM);

    private static XmlElement Root()
    {
        var instance = Activator.CreateInstance(typeof(MakeToolsPrefab), nonPublic: true)!;
        var method = typeof(MakeToolsPrefab).GetMethods().Single(m => m.CustomAttributes
            .Any(a => a.AttributeType.Name == "PrefabExtensionXmlDocumentAttribute"));
        return ((XmlDocument)method.Invoke(instance, null)!).DocumentElement!;
    }

    private static System.Collections.Generic.List<XmlElement> All() =>
        new[] { Root() }.Concat(Root().SelectNodes(".//*")!.Cast<XmlElement>()).ToList();

    private static CustomAttributeData Extension() =>
        CustomAttributeData.GetCustomAttributes(typeof(MakeToolsPrefab)).Single(a => a.AttributeType.Name == "PrefabExtensionAttribute");

    [TestMethod]
    public void TheExtension_AppendsASibling_AfterTheMaterialsList()
    {
        Assert.AreEqual("Crafting", Extension().ConstructorArguments[0].Value);
        Assert.AreEqual(XPathText, Extension().ConstructorArguments[1].Value);
        var insert = (PrefabExtensionInsertPatch)Activator.CreateInstance(typeof(MakeToolsPrefab), true)!;
        Assert.AreEqual(InsertType.Append, insert.Type);
    }

    [TestMethod]
    public void TheCard_IsOneWidget_WithABrushedButton_AndAHint()
    {
        var root = Root();

        Assert.AreEqual("Widget", root.Name);
        Assert.AreEqual("@IsMakeToolsVisible", root.GetAttribute("IsVisible"));
        var button = All().Single(e => e.Name == "ButtonWidget");
        Assert.AreEqual("Crafting.MainAction.Button", button.GetAttribute("Brush"));
        Assert.AreEqual("ExecuteMakeTools", button.GetAttribute("Command.Click"));
        Assert.AreEqual("@IsMakeToolsEnabled", button.GetAttribute("IsEnabled"));
        Assert.AreEqual("@MakeToolsText", All().Single(e => e.Name == "TextWidget").GetAttribute("Text"));
        Assert.IsTrue(All().Any(e => e.Name == "HintWidget"));
    }

    // A HintWidget relays only its PARENT's hover, and a disabled widget with its children is skipped by hit-testing. A greyed
    // button therefore needs a sibling hint under a hoverable parent (vanilla's MainActionParent, Crafting.xml:568-585) and an
    // enabled one a hint inside the button (vanilla's Reset Angle button, Crafting.xml:147-151). Both share one tooltip.
    [TestMethod]
    public void TheCard_HasTwoHints_OneBesideTheButton_AndOneInsideIt()
    {
        var hints = All().Where(e => e.Name == "HintWidget").ToList();

        Assert.AreEqual(2, hints.Count);
        Assert.IsTrue(hints.All(h => h.GetAttribute("DataSource") == "{MakeToolsHint}"), "one tooltip view model feeds both");
        Assert.IsTrue(hints.All(h => h.GetAttribute("Command.HoverBegin") == "ExecuteBeginHint"));
        Assert.IsTrue(hints.All(h => h.GetAttribute("Command.HoverEnd") == "ExecuteEndHint"));
        var sibling = (XmlElement?)Root().SelectSingleNode("Children/HintWidget");
        var inside = (XmlElement?)Root().SelectSingleNode("Children/ButtonWidget/Children/HintWidget");
        Assert.IsNotNull(sibling, "a hint beside the button explains it when it is greyed");
        Assert.IsNotNull(inside, "a hint inside the button shows the recipe when it is enabled");
        Assert.IsFalse(inside!.HasAttribute("WidthSizePolicy") || inside.HasAttribute("SuggestedWidth"), "the inner hint is size-less, as vanilla's");
    }

    [TestMethod]
    public void TheCardRoot_StaysHoverable_AndPassesEventsOn()
    {
        var root = Root();

        foreach (var attr in new[] { "DoNotAcceptEvents", "IsEnabled", "IsDisabled", "DoNotPassEventsToChildren" })
            Assert.IsFalse(root.HasAttribute(attr), "the hint relays the root's hover, so the root carries no " + attr);
    }

    [TestMethod]
    public void TheCard_ClearsTheCraftingHeroToggle()
    {
        // The toggle covers x 60 to 190 (Crafting.xml:492-494).
        Assert.AreEqual("200", Root().GetAttribute("MarginLeft"));
    }

    [TestMethod]
    public void TheCard_NamesNoSpriteOfItsOwn()
    {
        Assert.IsFalse(All().Any(e => e.HasAttribute("Sprite")), "the vanilla brush carries the art");
    }

    [TestMethod]
    public void NoInsertedElement_StretchesAnAxisItsParentCovers()
    {
        var root = Root();
        Assert.AreEqual("Fixed", root.GetAttribute("WidthSizePolicy"));
        Assert.AreEqual("Fixed", root.GetAttribute("HeightSizePolicy"));
    }

    [TestMethod]
    public void EveryBinding_ResolvesToAMixinOrVanillaMember()
    {
        var (failures, count) = PrefabBindingWalker.Walk(Root(), Vm, typeof(MakeToolsMixin));

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        Assert.IsTrue(count >= 6, "the walk found only " + count + " bindings; it is not reading the inserted XML");
    }

    [TestMethod]
    public void TheMixinsDataSources_AreTheTypesTheWidgetsNeed()
    {
        Assert.AreEqual(typeof(bool), typeof(MakeToolsMixin).GetProperty("IsMakeToolsVisible")!.PropertyType);
        Assert.AreEqual(typeof(bool), typeof(MakeToolsMixin).GetProperty("IsMakeToolsEnabled")!.PropertyType);
        Assert.AreEqual(typeof(string), typeof(MakeToolsMixin).GetProperty("MakeToolsText")!.PropertyType);
        Assert.AreEqual(typeof(TaleWorlds.Core.ViewModelCollection.Information.BasicTooltipViewModel),
            typeof(MakeToolsMixin).GetProperty("MakeToolsHint")!.PropertyType);
    }

    [TestMethod]
    public void TheMixinsRefreshMethod_IsDeclaredOnceOnTheCraftingViewModel_AndItsTabSwitchesArePublic()
    {
        var declared = Vm.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "RefreshEnableMainAction").ToList();

        Assert.AreEqual(1, declared.Count, "the transpiler target must be unambiguous");
        Assert.IsFalse(declared[0].IsAbstract);
        Assert.IsTrue(Vm.GetMethod("ExecuteSwitchToRefinement", BindingFlags.Public | BindingFlags.Instance)!.IsPublic,
            "the press refreshes the screen through the view model's own public tab switch");
        Assert.IsTrue(Vm.GetProperty("IsInRefinementMode")!.GetGetMethod()!.IsPublic);
        Assert.IsTrue(Vm.GetProperty("CurrentCraftingHero")!.GetGetMethod()!.IsPublic);
    }

    [TestMethod]
    public void BothUiTypes_AreSuppressedUnderCoop()
    {
        Assert.IsTrue(Attribute.IsDefined(typeof(MakeToolsPrefab), typeof(CoopSuppressedUiAttribute)));
        Assert.IsTrue(Attribute.IsDefined(typeof(MakeToolsMixin), typeof(CoopSuppressedUiAttribute)));
    }

    private static string GameDir()
    {
        var env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
    }

    [DataTestMethod]
    [TestCategory("LiveInstall")]
    [DataRow("Crafting.MainAction.Button")]
    [DataRow("Crafting.MainAction.Text")]
    public void EveryBrush_IsDeclaredInTheInstalledVanillaBrushes(string brush)
    {
        Assert.IsTrue(All().Any(e => e.GetAttribute("Brush") == brush), "the card names " + brush);
        var dir = Path.Combine(GameDir(), "Modules", "SandBox", "GUI", "Brushes");
        if (!Directory.Exists(dir))
            Assert.Inconclusive(dir + " is not installed on this machine.");
        var declared = Directory.GetFiles(dir, "*.xml").SelectMany(f => File.ReadAllText(f).Split('<')).ToList();
        Assert.IsTrue(declared.Any(s => s.StartsWith("Brush Name=\"" + brush + "\"", StringComparison.Ordinal)),
            "Brush " + brush + " is not declared; Gauntlet would render default styling silently");
    }

    // The placement: the match is the one materials list, a direct child of the screen widget (so a sibling lands in the screen's
    // own Children), and the vanilla main-action button uses the same brushes and the same hint widget shape.
    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TheXPathMatch_InTheInstalledPrefab_IsTheMaterialsListInTheScreensChildren()
    {
        var path = Path.Combine(GameDir(), "Modules", "SandBox", "GUI", "Prefabs", "Crafting", "Crafting.xml");
        if (!File.Exists(path))
            Assert.Inconclusive(path + " is not installed on this machine.");
        var doc = new XmlDocument();
        using (var reader = XmlReader.Create(path, new XmlReaderSettings { IgnoreComments = true }))
            doc.Load(reader);

        var matches = doc.DocumentElement!.SelectNodes(XPathText)!.Cast<XmlElement>().ToList();

        Assert.AreEqual(1, matches.Count, "the XPath must match exactly one list");
        var list = matches[0];
        Assert.AreEqual("CraftingScreenWidget", ((XmlElement)list.ParentNode!.ParentNode!).Name);
        Assert.AreEqual("Children", list.ParentNode!.Name);

        var button = doc.DocumentElement.SelectSingleNode("descendant::ButtonWidget[@Id='MainActionButtonWidget']") as XmlElement;
        Assert.IsNotNull(button);
        Assert.AreEqual("Crafting.MainAction.Button", button!.GetAttribute("Brush"));
        Assert.IsNotNull(doc.DocumentElement.SelectSingleNode("descendant::HintWidget[@DataSource='{MainActionHint}']"),
            "the card's hint widget copies vanilla's main-action hint");
    }
}
