using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LinesOfDescent.UI;
using TAOM.Tests.Migration;
using static TAOM.Tests.Infrastructure.RepoPaths;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LinesOfDescent;

/// <summary>
/// Wiring pins for the Lines of Descent client UI (#905). A binding that names nothing is a silent no-op in Gauntlet and an
/// XPath that matches nothing is one red line in the log at movie load, so each is pinned here. What no test can show is that
/// the popup draws (tree lines, card sizes, the scroll panel); that is the in-game check.
/// </summary>
[TestClass]
public class FamilyTreeUiWiringTests
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

    private static string PrefabPath(string file) => RepoPath("Main", "_Module", "GUI", "Prefabs", "LinesOfDescent", file);

    private static XmlDocument Load(string path)
    {
        var doc = new XmlDocument();
        doc.Load(path);
        return doc;
    }

    private static string XPathOf(Type extension) =>
        CustomAttributeData.GetCustomAttributes(extension).Single(a => a.AttributeType.Name == "PrefabExtensionAttribute")
            .ConstructorArguments[1].Value!.ToString()!;

    // The injected Family Tree button; its scope targeter goes in beside it (InsertedNodes).
    private static XmlElement InsertedElement(Type extension) => InsertedNodes(extension).Single(n => n.Name == "ButtonWidget");

    [TestMethod]
    public void TheHeroPageAnchor_ExistsOnceInTaomsCopy_AndTheButtonGoesBeforeIt()
    {
        var xpath = XPathOf(typeof(EncyclopediaHeroFamilyTreePrefab));
        var doc = Load(RepoPath("Main", "_Module", "GUI", "Prefabs", "Encyclopedia", "EncyclopediaSubPages", "EncyclopediaHeroPage.xml"));

        Assert.AreEqual(1, doc.SelectNodes(xpath)!.Count, xpath);
        var insert = (PrefabExtensionInsertPatch)Activator.CreateInstance(typeof(EncyclopediaHeroFamilyTreePrefab), true)!;
        Assert.AreEqual(InsertType.Prepend, insert.Type, "Prepend is InsertBefore on the matched node: the button sits above the Family grid");
        Assert.AreEqual("@HasFamilyTree", InsertedElement(typeof(EncyclopediaHeroFamilyTreePrefab)).GetAttribute("IsVisible"));
    }

    [TestMethod]
    public void TheClanAnchor_AppendsAsTheLastChild_AndTaomShipsNoClanMembersClone()
    {
        Assert.AreEqual("descendant::ListPanel[@Id='LastSeenLocationParent']/Children", XPathOf(typeof(ClanMembersFamilyTreePrefab)));
        var insert = (PrefabExtensionInsertPatch)Activator.CreateInstance(typeof(ClanMembersFamilyTreePrefab), true)!;
        Assert.AreEqual(InsertType.Child, insert.Type, "Append would make a sibling below the row, outside the selected member's row");
        Assert.AreEqual(int.MaxValue, insert.Index);
        var clones = Directory.GetFiles(RepoPath("Main", "_Module", "GUI", "Prefabs"), "ClanMembers.xml", SearchOption.AllDirectories);
        Assert.AreEqual(0, clones.Length, "a TAOM clone of ClanMembers would win over vanilla, so the live pin below would test the wrong file");
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TheClanAnchor_MatchesOnceInTheInstalledVanillaPrefab()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game install unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));
        var path = Path.Combine(GameAssemblies.GameDir, "Modules", "SandBox", "GUI", "Prefabs", "Clan", "ClanMembers.xml");
        if (!File.Exists(path))
            Assert.Inconclusive("ClanMembers.xml not found at " + path);

        Assert.AreEqual(1, Load(path).SelectNodes(XPathOf(typeof(ClanMembersFamilyTreePrefab)))!.Count);
    }

    [TestMethod]
    public void ThePopupScrollPanel_NamesBothScrollbars_AndPans()
    {
        var doc = Load(PrefabPath("TaomFamilyTreePopup.xml"));
        var panel = (XmlElement)doc.SelectSingleNode("//EncyclopediaTroopScrollablePanel")!;
        Assert.IsNotNull(panel);
        Assert.AreEqual("true", panel.GetAttribute("PanWithMouseEnabled"));
        // The drag code writes HorizontalScrollbar and VerticalScrollbar with no null check.
        foreach (var attribute in new[] { "HorizontalScrollbar", "VerticalScrollbar" })
        {
            var reference = panel.GetAttribute(attribute);
            Assert.IsTrue(reference.StartsWith("..\\", StringComparison.Ordinal), attribute + " must name a sibling by relative path");
            var id = reference.Substring(3);
            var scrollbar = (XmlElement?)doc.SelectSingleNode($"//ScrollbarWidget[@Id='{id}']");
            Assert.IsNotNull(scrollbar, attribute + " names " + id + ", which is not a ScrollbarWidget");
            Assert.IsNotNull(doc.SelectSingleNode($"//*[@Id='{scrollbar.GetAttribute("Handle")}']"), id + " has no handle widget");
        }

        Assert.IsNotNull(doc.SelectSingleNode("//*[@Id='InnerPanel']"));
        Assert.IsNotNull(doc.SelectSingleNode("//*[@Id='TreeRect']"));
    }

    [TestMethod]
    public void TheNodeTemplate_UsesTheTreeWidgetAndRefersToItself()
    {
        var doc = Load(PrefabPath("TaomFamilyTreeNode.xml"));
        var root = (XmlElement)doc.SelectSingleNode("/Prefab/Window/*")!;
        Assert.AreEqual("EncyclopediaUnitTreeNodeItemBrushWidget", root.Name);
        Assert.AreEqual("Encyclopedia.CharacterTree.Line", root.GetAttribute("LineBrush"));
        foreach (var attribute in new[] { "ChildContainer", "LineContainer" })
            Assert.IsNotNull(doc.SelectSingleNode($"//*[@Id='{root.GetAttribute(attribute)}']"), attribute);
        Assert.IsNotNull(doc.SelectSingleNode("//ListPanel[@Id='ChildContainer']/ItemTemplate/TaomFamilyTreeNode"));
    }

    [TestMethod]
    public void EveryPopupAndNodeBinding_ResolvesToADataSourcePropertyOrACommand()
    {
        var failures = new List<string>();
        var count = 0;
        Walk(Load(PrefabPath("TaomFamilyTreePopup.xml")).DocumentElement!, typeof(FamilyTreePopupVM), failures, ref count);
        Walk(Load(PrefabPath("TaomFamilyTreeNode.xml")).DocumentElement!, typeof(FamilyTreeNodeVM), failures, ref count);

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        Assert.IsTrue(count >= 9, "the walk found only " + count + " bindings; it is not reading the prefabs");
    }

    [TestMethod]
    public void EveryExtensionBinding_ResolvesOnItsMixin()
    {
        var failures = new List<string>();
        var count = 0;
        var pairs = new[]
        {
            (typeof(ClanMembersFamilyTreePrefab), typeof(ClanLordItemFamilyMixin)),
            (typeof(EncyclopediaHeroFamilyTreePrefab), typeof(EncyclopediaHeroFamilyTreeMixin)),
        };
        foreach (var (extension, mixin) in pairs)
        {
            foreach (var node in InsertedNodes(extension))
                foreach (var element in new[] { node }.Concat(node.SelectNodes(".//*")!.Cast<XmlElement>()))
                    CheckAttributes(element, mixin, failures, ref count);
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        Assert.IsTrue(count >= 4, "found only " + count + " bindings");
    }

    [TestMethod]
    public void TheMixins_HookTheEngineMethodsTheBriefNames()
    {
        Assert.AreEqual("UpdateProperties", typeof(ClanLordItemFamilyMixin).GetCustomAttribute<ViewModelMixinAttribute>()!.RefreshMethodName);
        Assert.AreEqual("Refresh", typeof(EncyclopediaHeroFamilyTreeMixin).GetCustomAttribute<ViewModelMixinAttribute>()!.RefreshMethodName);
        Assert.IsNotNull(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanLordItemVM).GetMethod("UpdateProperties", Type.EmptyTypes));
        Assert.IsNotNull(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages.EncyclopediaHeroPageVM).GetMethod("Refresh", Type.EmptyTypes));
    }

    [TestMethod]
    public void ThePopupIsRegisteredOnce_AndNothingTouchesTheSingleOwnerFiles()
    {
        var composition = ReadSource("Client/TaomClientComposition.cs", stripComments: true);
        Assert.AreEqual(1, composition.Split(new[] { "Register<FamilyTreePopup>" }, StringSplitOptions.None).Length - 1);
        foreach (var owner in new[] { "Main/IoC.cs", "Main/SubModule.cs" })
            StringAssert.DoesNotMatch(ReadSource(owner, stripComments: true), new System.Text.RegularExpressions.Regex("FamilyTree"), owner);
    }

    [TestMethod]
    public void TheLayer_UnregistersThePageEvent_AndSitsAboveTheEncyclopediaBar()
    {
        var layer = ReadSource("Client/Features/LinesOfDescent/UI/FamilyTreeLayer.cs", stripComments: true);
        StringAssert.Contains(layer, "RegisterEvent<EncyclopediaPageChangedEvent>");
        StringAssert.Contains(layer, "UnregisterEvent<EncyclopediaPageChangedEvent>");
        StringAssert.Contains(layer, "LayerOrder = 320");
    }

    private static void Walk(XmlElement element, Type scope, List<string> failures, ref int count)
    {
        CheckAttributes(element, scope, failures, ref count);
        var dataSource = element.GetAttribute("DataSource");
        if (dataSource.StartsWith("{", StringComparison.Ordinal))
        {
            var property = Property(scope, dataSource.Trim('{', '}'));
            scope = property == null ? scope : ElementType(property.PropertyType);
        }

        foreach (XmlNode child in element.ChildNodes)
        {
            if (child is not XmlElement childElement)
                continue;
            // The scope of an ItemTemplate's content is the list's item type, which the DataSource step already resolved.
            Walk(childElement, scope, failures, ref count);
        }
    }

    private static void CheckAttributes(XmlElement element, Type scope, List<string> failures, ref int count)
    {
        foreach (XmlAttribute attribute in element.Attributes)
        {
            var value = attribute.Value;
            if (attribute.Name == "DataSource" && value.StartsWith("{", StringComparison.Ordinal))
            {
                count++;
                if (Property(scope, value.Trim('{', '}')) == null)
                    failures.Add($"<{element.Name} DataSource=\"{value}\"> has no [DataSourceProperty] on {scope.Name}");
            }
            else if (value.StartsWith("@", StringComparison.Ordinal))
            {
                count++;
                if (Property(scope, value.Substring(1)) == null)
                    failures.Add($"<{element.Name} {attribute.Name}=\"{value}\"> has no [DataSourceProperty] on {scope.Name}");
            }
            else if (attribute.Name.StartsWith("Command.", StringComparison.Ordinal))
            {
                count++;
                var command = scope.GetMethod(value, Members, null, Type.EmptyTypes, null);
                var isMixinCommand = command != null && command.CustomAttributes.Any(a => a.AttributeType.Name == "DataSourceMethodAttribute");
                if (command == null || (!isMixinCommand && !command.IsPublic))
                    failures.Add($"<{element.Name} {attribute.Name}=\"{value}\"> has no public command on {scope.Name}");
            }
        }
    }

    private static PropertyInfo? Property(Type scope, string name)
    {
        var property = scope.GetProperty(name, Members);
        return property != null && property.CustomAttributes.Any(a => a.AttributeType.Name.StartsWith("DataSourceProperty", StringComparison.Ordinal)) ? property : null;
    }

    private static Type ElementType(Type type) =>
        type.IsGenericType && type.GetGenericArguments().Length == 1 ? type.GetGenericArguments()[0] : type;

    // ---- review fixes ------------------------------------------------------------------------------------------

    private const string UiDir = "Client/Features/LinesOfDescent/UI/";

    [TestMethod]
    public void NoMixinCallsOnRefreshInItsConstructor()
    {
        // UIExtenderEx 2.13.3 replays the view model constructor's refresh call on the mixin as OnRefresh, so a call here runs it twice.
        var files = Directory.GetFiles(RepoPath("Client", "Features", "LinesOfDescent", "UI"), "*Mixin.cs");
        Assert.AreEqual(2, files.Length);
        foreach (var file in files)
        {
            var source = ReadSource(UiDir + Path.GetFileName(file), stripComments: true);
            var constructor = System.Text.RegularExpressions.Regex.Match(source, @"public \w+\([^)]*\)\s*:\s*base\([^)]*\)\s*\{(?<body>.*?)\n    \}", System.Text.RegularExpressions.RegexOptions.Singleline);
            Assert.IsTrue(constructor.Success, file);
            StringAssert.DoesNotMatch(constructor.Groups["body"].Value, new System.Text.RegularExpressions.Regex(@"OnRefresh\s*\("), file);
        }
    }

    [TestMethod]
    public void TheCloseButton_RequestsTheCloseTheTickPerforms()
    {
        var layer = ReadSource(UiDir + "FamilyTreeLayer.cs", stripComments: true);
        StringAssert.Contains(layer, "new FamilyTreePopupVM(tree, RequestClose)");
        StringAssert.Matches(layer, new System.Text.RegularExpressions.Regex(@"void RequestClose\(\)\s*=>\s*_closeRequested = true;"));
        StringAssert.DoesNotMatch(layer, new System.Text.RegularExpressions.Regex(@"TopScreen\s*!="), "a layer is not ticked while its host is not on top");
    }

    [TestMethod]
    public void AnOpenThatFails_LeavesNothingBehind()
    {
        var layer = ReadSource(UiDir + "FamilyTreeLayer.cs", stripComments: true);
        Assert.IsTrue(layer.IndexOf("_host.AddLayer(this)", StringComparison.Ordinal) < layer.IndexOf("new FamilyTreePopupVM(", StringComparison.Ordinal),
            "the layer joins the host before the view model and movie exist, so Close can always remove it");
        var teardown = layer.Substring(layer.IndexOf("private void Teardown()", StringComparison.Ordinal));
        StringAssert.Contains(teardown, "finally");
        Assert.IsTrue(teardown.IndexOf("finally", StringComparison.Ordinal) < teardown.IndexOf("_onClosed(this)", StringComparison.Ordinal));

        var popup = ReadSource(UiDir + "FamilyTreePopup.cs", stripComments: true);
        var caught = popup.Substring(popup.IndexOf("catch (Exception", StringComparison.Ordinal));
        Assert.IsTrue(caught.IndexOf("_layer = null", StringComparison.Ordinal) < caught.IndexOf("layer?.Close()", StringComparison.Ordinal),
            "IsOpen must be false before Close runs, so a throwing Close cannot leave it stuck");
    }

    [TestMethod]
    public void TheTreeNodeCentresItsChildren_AndThePopupFrameHasNoDeadLayoutAttribute()
    {
        var node = Load(PrefabPath("TaomFamilyTreeNode.xml"));
        Assert.AreEqual("Center", ((XmlElement)node.SelectSingleNode("//ListPanel[@Id='ChildContainer']")!).GetAttribute("HorizontalAlignment"));
        var popup = Load(PrefabPath("TaomFamilyTreePopup.xml"));
        var frame = (XmlElement)popup.SelectSingleNode("//BrushWidget[@Brush='Frame1Canvas']")!;
        Assert.IsFalse(frame.HasAttribute("StackLayout.LayoutMethod"), "BrushWidget has no stack layout of its own");
    }

    [TestMethod]
    public void BothInjectedButtons_JoinGamepadNavigation()
    {
        foreach (var extension in new[] { typeof(ClanMembersFamilyTreePrefab), typeof(EncyclopediaHeroFamilyTreePrefab) })
        {
            var nodes = InsertedNodes(extension);
            var button = nodes.Single(n => n.Name == "ButtonWidget");
            Assert.AreEqual("0", button.GetAttribute("GamepadNavigationIndex"), extension.Name);
            var scope = nodes.Single(n => n.Name == "NavigationScopeTargeter");
            Assert.AreEqual("..\\" + button.GetAttribute("Id"), scope.GetAttribute("ScopeParent"), extension.Name);
            Assert.IsTrue(nodes.IndexOf(scope) < nodes.IndexOf(button), "the targeter precedes the button it scopes, as in vanilla");
        }
    }

    private static List<XmlElement> InsertedNodes(Type extension) => PrefabNodes.Of(extension);
}
