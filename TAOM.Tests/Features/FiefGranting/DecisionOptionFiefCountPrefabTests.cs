using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FiefGranting.UI;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FiefGranting;

/// <summary>
/// The XML the fief-count row inserts into the kingdom decision option card (#866). A binding that names nothing is a silent
/// no-op in Gauntlet, so every <c>@Binding</c>, <c>{DataSource}</c> and <c>Command.*</c> must resolve to a mixin member or a
/// vanilla one. That the XPath still matches the vanilla prefab is <c>PrefabExtensionBindingTests</c> plus the live-install test
/// below, which pins that the match sits outside every ButtonWidget.
/// </summary>
[TestClass]
public class DecisionOptionFiefCountPrefabTests
{
    private static readonly Type Vm = typeof(TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.DecisionOptionVM);

    private static List<XmlElement> Nodes() => PrefabNodes.Of(typeof(DecisionOptionFiefCountPrefab));

    private static IEnumerable<XmlElement> All() =>
        Nodes().SelectMany(n => new[] { n }.Concat(n.SelectNodes(".//*")!.Cast<XmlElement>()));

    private static string XPath() =>
        (string)CustomAttributeData.GetCustomAttributes(typeof(DecisionOptionFiefCountPrefab))
            .Single(a => a.AttributeType.Name == "PrefabExtensionAttribute").ConstructorArguments[1].Value!;

    [TestMethod]
    public void TheExtension_AppendsAsASiblingAfterTheCardWrapper_OfTheOptionItem()
    {
        var attribute = CustomAttributeData.GetCustomAttributes(typeof(DecisionOptionFiefCountPrefab))
            .Single(a => a.AttributeType.Name == "PrefabExtensionAttribute");

        Assert.AreEqual("KingdomDecisionOptionItem", attribute.ConstructorArguments[0].Value);
        Assert.AreEqual("descendant::ButtonWidget[@Command.Click='ExecuteSelection']/parent::Children/parent::Widget", attribute.ConstructorArguments[1].Value);
        var insert = (Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch)Activator.CreateInstance(typeof(DecisionOptionFiefCountPrefab), true)!;
        Assert.AreEqual(Bannerlord.UIExtenderEx.Prefabs2.InsertType.Append, insert.Type, "the row is a sibling of the card, outside its ButtonWidget, so a disabled card still hovers");
    }

    // Vanilla's shape (SandBox Inventory.xml:471-475): one TextWidget with a HintWidget child that only relays the parent's hover.
    [TestMethod]
    public void TheInsert_IsOneTextWidget_WithTheHintAsItsChild_AndNoSprite()
    {
        var nodes = Nodes();

        Assert.AreEqual(1, nodes.Count, "one root element");
        var row = nodes[0];
        Assert.AreEqual("TextWidget", row.Name);
        Assert.AreEqual("@ShowFiefCount", row.GetAttribute("IsVisible"));
        Assert.AreEqual("@FiefCountText", row.GetAttribute("Text"));
        Assert.AreEqual("CoverChildren", row.GetAttribute("WidthSizePolicy"));
        Assert.AreEqual("CoverChildren", row.GetAttribute("HeightSizePolicy"));
        Assert.IsFalse(row.HasAttribute("DoNotAcceptEvents"), "the TextWidget must receive the hover its HintWidget child relays");
        Assert.IsFalse(row.HasAttribute("Brush.FontSize"), "the brush's own size applies");
        var hint = (XmlElement)row.SelectSingleNode("Children/HintWidget")!;
        Assert.AreEqual("{FiefCountHint}", hint.GetAttribute("DataSource"));
        Assert.AreEqual("ExecuteBeginHint", hint.GetAttribute("Command.HoverBegin"));
        Assert.AreEqual("ExecuteEndHint", hint.GetAttribute("Command.HoverEnd"));
        Assert.IsNull(row.SelectSingleNode(".//*[@Sprite]"), "the row uses no sprite");
        Assert.IsNull(row.SelectSingleNode(".//ButtonWidget"), "the row is not a button");
    }

    // A StretchToParent child of a CoverChildren stack panel fills the whole space the parent is offered (the card layout
    // blew past the panel), and a HintWidget only relays its parent's hover, so it needs no size at all.
    [TestMethod]
    public void NoInsertedElement_IsStretchToParent_AndTheHintWidgetHasNoSizePolicy()
    {
        foreach (var element in All())
        {
            Assert.AreNotEqual("StretchToParent", element.GetAttribute("WidthSizePolicy"), element.Name);
            Assert.AreNotEqual("StretchToParent", element.GetAttribute("HeightSizePolicy"), element.Name);
        }

        var hint = All().Single(e => e.Name == "HintWidget");
        Assert.IsFalse(hint.HasAttribute("WidthSizePolicy") || hint.HasAttribute("HeightSizePolicy"));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void EveryBrush_IsDeclaredInTheInstalledVanillaBrushes()
    {
        var brushes = All().Where(e => e.HasAttribute("Brush")).Select(e => e.GetAttribute("Brush")).Distinct().ToList();
        Assert.IsTrue(brushes.Count > 0, "the row names a brush");
        var dir = Path.Combine(GameDir(), "Modules", "SandBox", "GUI", "Brushes");
        if (!Directory.Exists(dir))
            Assert.Inconclusive(dir + " is not installed on this machine.");
        var declared = Directory.GetFiles(dir, "*.xml").SelectMany(f => File.ReadAllText(f).Split('<')).ToList();
        foreach (var brush in brushes)
            Assert.IsTrue(declared.Any(s => s.StartsWith("Brush Name=\"" + brush + "\"", StringComparison.Ordinal)), "Brush " + brush + " is not declared; Gauntlet would render default styling silently");
    }

    [TestMethod]
    public void EveryBindingCommandAndDataSource_ResolvesToAMixinOrVanillaMember()
    {
        var (failures, count) = PrefabBindingWalker.Walk(Nodes()[0], Vm, typeof(DecisionOptionFiefCountMixin));

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        Assert.IsTrue(count >= 4, "the walk found only " + count + " bindings; it is not reading the inserted XML");
    }

    [TestMethod]
    public void TheMixinsDataSources_AreTheTypesTheWidgetsNeed()
    {
        Assert.AreEqual(typeof(bool), typeof(DecisionOptionFiefCountMixin).GetProperty("ShowFiefCount")!.PropertyType);
        Assert.AreEqual(typeof(string), typeof(DecisionOptionFiefCountMixin).GetProperty("FiefCountText")!.PropertyType);
        Assert.AreEqual("HintViewModel", typeof(DecisionOptionFiefCountMixin).GetProperty("FiefCountHint")!.PropertyType.Name);
    }

    private static string GameDir()
    {
        var env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
    }

    // The XPath count itself is PrefabExtensionBindingTests' gate. This pins the placement: a row inside a ButtonWidget would
    // never hover while the card is disabled, because Gauntlet does not hover a disabled widget.
    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TheXPathMatch_InTheInstalledOptionItem_HasNoButtonWidgetAncestor()
    {
        var path = Path.Combine(GameDir(), "Modules", "SandBox", "GUI", "Prefabs", "KingdomManagement", "Decision", "KingdomDecisionOptionItem.xml");
        if (!File.Exists(path))
            Assert.Inconclusive(path + " is not installed on this machine.");
        var doc = new XmlDocument();
        doc.Load(path);

        var matches = doc.DocumentElement!.SelectNodes(XPath())!.Cast<XmlNode>().ToList();

        Assert.IsTrue(matches.Count > 0, "the XPath matches nothing");
        foreach (var match in matches)
            Assert.IsNull(match.SelectSingleNode("ancestor-or-self::ButtonWidget"), "the row is appended after the match, so the match must not be inside a ButtonWidget");
    }
}
