using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BondOfHire.UI;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BondOfHire;

/// <summary>
/// The XML the contract line inserts into the kingdom Clans panel (#868). A binding that names nothing is a silent no-op in
/// Gauntlet, so every <c>@Binding</c> must resolve to a mixin member or a vanilla one. That the XPath still matches the vanilla
/// prefab exactly once is <c>PrefabExtensionBindingTests</c>; the live-install tests below pin where the match sits and what
/// the line lands between.
/// </summary>
[TestClass]
public class MercenaryTermPrefabTests
{
    private static readonly Type Vm = typeof(TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Clans.KingdomClanVM);

    private static List<XmlElement> Nodes() => PrefabNodes.Of(typeof(MercenaryTermPrefab));

    private static IEnumerable<XmlElement> All() =>
        Nodes().SelectMany(n => new[] { n }.Concat(n.SelectNodes(".//*")!.Cast<XmlElement>()));

    private static CustomAttributeData Extension() =>
        CustomAttributeData.GetCustomAttributes(typeof(MercenaryTermPrefab)).Single(a => a.AttributeType.Name == "PrefabExtensionAttribute");

    private static string XPath() => (string)Extension().ConstructorArguments[1].Value!;

    [TestMethod]
    public void TheExtension_InsertsAChildOfTheMembersList_BeforeTheMembersGrid()
    {
        Assert.AreEqual("ClansPanel", Extension().ConstructorArguments[0].Value);
        Assert.AreEqual("descendant::ListPanel[@Id='MembersList']/Children", XPath());
        var insert = (Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch)Activator.CreateInstance(typeof(MercenaryTermPrefab), true)!;
        Assert.AreEqual(Bannerlord.UIExtenderEx.Prefabs2.InsertType.Child, insert.Type);
        Assert.AreEqual(1, insert.Index, "InsertType.Child inserts BEFORE the child at Index: 0 is the Members header, 1 the grid");
    }

    [TestMethod]
    public void TheInsert_IsOneInertTextWidget_BoundToTheMixin_WithNoSpriteAndNoHint()
    {
        var nodes = Nodes();

        Assert.AreEqual(1, nodes.Count, "one root element");
        var row = nodes[0];
        Assert.AreEqual("TextWidget", row.Name);
        Assert.AreEqual("@IsMercenaryTermVisible", row.GetAttribute("IsVisible"));
        Assert.AreEqual("@MercenaryTermText", row.GetAttribute("Text"));
        Assert.AreEqual("Fixed", row.GetAttribute("WidthSizePolicy"));
        Assert.AreEqual("560", row.GetAttribute("SuggestedWidth"));
        Assert.AreEqual("Left", row.GetAttribute("Brush.TextHorizontalAlignment"), "a wrapped line reads left aligned");
        Assert.AreEqual("CoverChildren", row.GetAttribute("HeightSizePolicy"));
        Assert.AreEqual("true", row.GetAttribute("DoNotAcceptEvents"), "a text line takes no hover or click");
        Assert.AreEqual("Kingdom.ParagraphSmall.Text", row.GetAttribute("Brush"));
        Assert.AreEqual(0, row.SelectNodes(".//*")!.Count, "no child element: no hint, no panel");
        Assert.IsNull(row.SelectSingleNode(".//*[@Sprite]"));
        Assert.IsFalse(row.HasAttribute("Brush.FontSize"), "the brush's own size applies");
    }

    // A StretchToParent child of a CoverChildren parent fills all the space the parent is offered (#866). The MembersList the
    // line goes into is itself CoverChildren in height, so the height axis may not stretch (the width is fixed).
    [TestMethod]
    public void NoInsertedElement_StretchesItsHeight()
    {
        foreach (var element in All())
        {
            Assert.AreNotEqual("StretchToParent", element.GetAttribute("HeightSizePolicy"), element.Name);
        }
    }

    [TestMethod]
    public void EveryBinding_ResolvesToAMixinOrVanillaMember()
    {
        var (failures, count) = PrefabBindingWalker.Walk(Nodes()[0], Vm, typeof(MercenaryTermMixin));

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        Assert.IsTrue(count >= 2, "the walk found only " + count + " bindings; it is not reading the inserted XML");
    }

    [TestMethod]
    public void TheMixinsDataSources_AreTheTypesTheWidgetNeeds()
    {
        Assert.AreEqual(typeof(string), typeof(MercenaryTermMixin).GetProperty("MercenaryTermText")!.PropertyType);
        Assert.AreEqual(typeof(bool), typeof(MercenaryTermMixin).GetProperty("IsMercenaryTermVisible")!.PropertyType);
    }

    [TestMethod]
    public void TheMixin_HooksRefreshValues_AndTheViewModelDeclaresItsOwnRefreshValuesAndOnFinalize()
    {
        var attribute = CustomAttributeData.GetCustomAttributes(typeof(MercenaryTermMixin)).Single(a => a.AttributeType.Name == "ViewModelMixinAttribute");

        Assert.AreEqual("RefreshValues", attribute.ConstructorArguments[0].Value);
        Assert.AreEqual(Vm, Vm.GetMethod("RefreshValues")!.DeclaringType, "else the mixin would stack a hook on the base ViewModel");
        Assert.AreEqual(Vm, Vm.GetMethod("OnFinalize")!.DeclaringType, "else the mixin would stack a hook on the base ViewModel");
    }

    private static string GameDir()
    {
        var env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void EveryBrush_IsDeclaredInTheInstalledVanillaBrushes()
    {
        var brushes = All().Where(e => e.HasAttribute("Brush")).Select(e => e.GetAttribute("Brush")).Distinct().ToList();
        Assert.IsTrue(brushes.Count > 0, "the line names a brush");
        var dir = Path.Combine(GameDir(), "Modules", "SandBox", "GUI", "Brushes");
        if (!Directory.Exists(dir))
            Assert.Inconclusive(dir + " is not installed on this machine.");
        var declared = Directory.GetFiles(dir, "*.xml").SelectMany(f => File.ReadAllText(f).Split('<')).ToList();
        foreach (var brush in brushes)
            Assert.IsTrue(declared.Any(s => s.StartsWith("Brush Name=\"" + brush + "\"", StringComparison.Ordinal)), "Brush " + brush + " is not declared; Gauntlet would render default styling silently");
    }

    private static XmlDocument InstalledClansPanel()
    {
        var path = Path.Combine(GameDir(), "Modules", "SandBox", "GUI", "Prefabs", "KingdomManagement", "Clan", "ClansPanel.xml");
        if (!File.Exists(path))
            Assert.Inconclusive(path + " is not installed on this machine.");
        // Gauntlet drops comments, so they do not count as children: read the file the way it does.
        var doc = new XmlDocument();
        using (var reader = XmlReader.Create(path, new XmlReaderSettings { IgnoreComments = true }))
            doc.Load(reader);
        return doc;
    }

    // The placement: the match is the one vertical, height-covering list; its children are the Members header (0) and the fixed
    // grid (1), so Index 1 lands between them; Id "MembersList" is not referenced by any other widget (no hardcoded indexing).
    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TheXPathMatch_InTheInstalledPanel_IsTheMembersListWithTheHeaderThenTheGrid()
    {
        var doc = InstalledClansPanel();

        var matches = doc.DocumentElement!.SelectNodes(XPath())!.Cast<XmlElement>().ToList();

        Assert.AreEqual(1, matches.Count, "the XPath must match exactly one Children element");
        var list = (XmlElement)matches[0].ParentNode!;
        Assert.AreEqual("VerticalTopToBottom", list.GetAttribute("StackLayout.LayoutMethod"));
        Assert.AreEqual("CoverChildren", list.GetAttribute("HeightSizePolicy"), "the inserted line grows the list, which covers its children");
        Assert.AreEqual(2, matches[0].ChildNodes.Count, "Index 1 is between the header and the grid only when the list has exactly these two children");
        var children = matches[0].ChildNodes.Cast<XmlNode>().OfType<XmlElement>().ToList();
        Assert.AreEqual(2, children.Count, "header and grid");
        Assert.AreEqual("@MembersText", children[0].SelectSingleNode(".//TextWidget")!.Attributes!["Text"]!.Value, "child 0 is the Members header");
        Assert.AreEqual("440", children[1].GetAttribute("SuggestedHeight"), "child 1 is the fixed-height members grid");
        Assert.IsNull(matches[0].SelectSingleNode("ancestor-or-self::ButtonWidget"), "the line must not sit inside a button");
        Assert.AreEqual(1, doc.OuterXml.Split(new[] { "MembersList" }, StringSplitOptions.None).Length - 1, "no other widget refers to the list by id");
    }
}
