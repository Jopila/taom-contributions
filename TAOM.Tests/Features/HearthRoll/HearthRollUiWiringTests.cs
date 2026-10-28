using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features.HearthRoll;
using TAOM.Features.HearthRoll.UI;
using TAOM.Features.LinesOfDescent.UI;
using TAOM.Tests.Migration;
using static TAOM.Tests.Infrastructure.RepoPaths;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HearthRoll;

/// <summary>
/// Wiring pins for the Hearth Roll clan list badges (#906). Vanilla ClanLordTuple has no widget ids, so every XPath is
/// structural and each is pinned to match exactly once in the installed prefab. What no test can show is the look (the tint
/// strength, the label beside the name), and that is the in-game check.
/// </summary>
[TestClass]
public class HearthRollUiWiringTests
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
    private const string TupleChildren = "/Prefab/Window/ButtonWidget/Children";
    private const string NameWidget = "/Prefab/Window/ButtonWidget/Children/ListPanel/Children/TextWidget[1]";

    private static readonly Type[] Extensions =
    {
        typeof(ClanLordTupleTintPrefab), typeof(ClanLordTupleNameWidthPrefab), typeof(ClanLordTupleAgeLabelPrefab),
    };

    private static string XPathOf(Type extension) =>
        CustomAttributeData.GetCustomAttributes(extension).Single(a => a.AttributeType.Name == "PrefabExtensionAttribute")
            .ConstructorArguments[1].Value!.ToString()!;

    private static string MovieOf(Type extension) =>
        CustomAttributeData.GetCustomAttributes(extension).Single(a => a.AttributeType.Name == "PrefabExtensionAttribute")
            .ConstructorArguments[0].Value!.ToString()!;

    private static XmlDocument InstalledTuple()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game install unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));
        var path = Path.Combine(GameAssemblies.GameDir, "Modules", "SandBox", "GUI", "Prefabs", "Clan", "Management", "ClanLordTuple.xml");
        if (!File.Exists(path))
            Assert.Inconclusive("ClanLordTuple.xml not found at " + path);
        var doc = new XmlDocument();
        doc.Load(path);
        return doc;
    }

    private static List<XmlElement> InsertedNodes(Type extension) => PrefabNodes.Of(extension);

    [TestMethod]
    public void EveryExtension_TargetsClanLordTuple_WithAStructuralXPath()
    {
        foreach (var extension in Extensions)
        {
            Assert.AreEqual("ClanLordTuple", MovieOf(extension), extension.Name);
            StringAssert.DoesNotMatch(XPathOf(extension), new Regex(@"\[@"), extension.Name + ": the vanilla prefab has no ids to select by");
        }

        Assert.AreEqual(TupleChildren, XPathOf(typeof(ClanLordTupleTintPrefab)));
        Assert.AreEqual(NameWidget, XPathOf(typeof(ClanLordTupleNameWidthPrefab)));
        Assert.AreEqual(NameWidget, XPathOf(typeof(ClanLordTupleAgeLabelPrefab)));
    }

    [TestMethod]
    public void TaomShipsNoClanLordTupleClone()
    {
        var clones = Directory.GetFiles(RepoPath("Main", "_Module", "GUI", "Prefabs"), "ClanLordTuple.xml", SearchOption.AllDirectories);
        Assert.AreEqual(0, clones.Length, "a TAOM clone would win over vanilla, so the live pins below would test the wrong file");
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void EveryXPath_MatchesExactlyOnceInTheInstalledPrefab()
    {
        var doc = InstalledTuple();

        foreach (var extension in Extensions)
            Assert.AreEqual(1, doc.SelectNodes(XPathOf(extension))!.Count, extension.Name + " " + XPathOf(extension));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TheNameNode_BindsName_AndIsTheFirstTextWidgetOfTheRow()
    {
        var node = (XmlElement)InstalledTuple().SelectSingleNode(NameWidget)!;

        Assert.AreEqual("TextWidget", node.Name);
        Assert.AreEqual("@Name", node.GetAttribute("Text"));
        Assert.AreEqual("Clan.Tuple.Name.Text", node.GetAttribute("Brush"));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TheTintAnchor_HoldsTheRowsListPanel_SoIndexZeroSitsBehindIt()
    {
        var children = (XmlElement)InstalledTuple().SelectSingleNode(TupleChildren)!;

        Assert.AreEqual("ListPanel", children.ChildNodes.OfType<XmlElement>().First().Name);
        Assert.AreEqual("ButtonWidget", children.ParentNode!.Name);
    }

    [TestMethod]
    public void TheTint_IsTheFirstChild_AndTheLabelASiblingAfterTheName()
    {
        var tint = (PrefabExtensionInsertPatch)Activator.CreateInstance(typeof(ClanLordTupleTintPrefab), true)!;
        Assert.AreEqual(InsertType.Child, tint.Type, "the path ends in /Children, so Child lands inside it");
        Assert.AreEqual(0, tint.Index, "index zero draws behind the row's ListPanel");

        var label = (PrefabExtensionInsertPatch)Activator.CreateInstance(typeof(ClanLordTupleAgeLabelPrefab), true)!;
        Assert.AreEqual(InsertType.Append, label.Type, "Append is a sibling right after the name widget, which is what is wanted");
    }

    [TestMethod]
    public void TheNameWidthPatch_SetsExactlyTheWidthPolicyAndTheMaxWidth()
    {
        var patch = (PrefabExtensionSetAttributePatch)Activator.CreateInstance(typeof(ClanLordTupleNameWidthPrefab), true)!;

        var attributes = patch.Attributes;
        Assert.AreEqual(2, attributes.Count);
        Assert.AreEqual("CoverChildren", attributes.Single(a => a.Name == "WidthSizePolicy").Value);
        Assert.AreEqual("226", attributes.Single(a => a.Name == "MaxWidth").Value, "a long name wraps at the vanilla width");
    }

    [TestMethod]
    public void TheMixin_ReadsTheRelationFromTheMainHero_NotThroughTheCompanionAdapter()
    {
        // The adapter scans Hero.AllAliveHeroes, which omits a Disabled companion (away on an alternative-solution quest).
        var source = ReadSource("Client/Features/HearthRoll/UI/ClanLordItemBadgeMixin.cs", stripComments: true);

        StringAssert.DoesNotMatch(source, new Regex("INamedCompanionAdapter"));
        StringAssert.DoesNotMatch(source, new Regex(@"IoC\.Resolve<INamed"));
        StringAssert.Contains(source, "Hero.MainHero");
        StringAssert.Contains(source, "GetRelation(hero)");
    }

    [TestMethod]
    public void TheOverlay_IsInertAndBoundToTheTintProperty()
    {
        var overlay = InsertedNodes(typeof(ClanLordTupleTintPrefab)).Single();

        Assert.AreEqual("Widget", overlay.Name);
        Assert.AreEqual("@IsTaomUnhappyCompanion", overlay.GetAttribute("IsVisible"));
        Assert.AreEqual("true", overlay.GetAttribute("DoNotAcceptEvents"), "a click must reach the row underneath");
        Assert.AreEqual("StretchToParent", overlay.GetAttribute("WidthSizePolicy"));
        Assert.AreEqual("StretchToParent", overlay.GetAttribute("HeightSizePolicy"));
        Assert.AreEqual("BlankWhiteSquare_9", overlay.GetAttribute("Sprite"));
        Assert.AreEqual("#680A0DFF", overlay.GetAttribute("Color"));
        Assert.AreEqual("0.25", overlay.GetAttribute("AlphaFactor"));
    }

    [TestMethod]
    public void TheLabel_HidesWhenEmpty_AndIsInert()
    {
        var label = InsertedNodes(typeof(ClanLordTupleAgeLabelPrefab)).Single();

        Assert.AreEqual("TextWidget", label.Name);
        Assert.AreEqual("@TaomAgeText", label.GetAttribute("Text"));
        Assert.AreEqual("true", label.GetAttribute("AutoHideIfEmpty"));
        Assert.AreEqual("true", label.GetAttribute("DoNotAcceptEvents"));
        Assert.AreEqual("Clan.Tuple.Name.Text", label.GetAttribute("Brush"));
        Assert.AreEqual("CoverChildren", label.GetAttribute("WidthSizePolicy"));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TheSpriteAndTheBrush_ExistInTheInstalledGame()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game install unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));
        var modules = Path.Combine(GameAssemblies.GameDir, "Modules");
        var sprites = Path.Combine(modules, "Native", "GUI", "NativeSpriteData.xml");
        var brushes = Path.Combine(modules, "SandBox", "GUI", "Brushes", "Clan.xml");
        if (!File.Exists(sprites) || !File.Exists(brushes))
            Assert.Inconclusive("vanilla GUI files not found under " + modules);

        StringAssert.Contains(File.ReadAllText(sprites), "<Name>BlankWhiteSquare_9</Name>");
        StringAssert.Contains(File.ReadAllText(brushes), "<Brush Name=\"Clan.Tuple.Name.Text\"");
    }

    [TestMethod]
    public void EveryBinding_ResolvesToADataSourcePropertyOnTheMixin()
    {
        var failures = new List<string>();
        var count = 0;
        foreach (var extension in new[] { typeof(ClanLordTupleTintPrefab), typeof(ClanLordTupleAgeLabelPrefab) })
        {
            foreach (var node in InsertedNodes(extension))
            {
                foreach (var element in new[] { node }.Concat(node.SelectNodes(".//*")!.Cast<XmlElement>()))
                {
                    foreach (XmlAttribute attribute in element.Attributes.Cast<XmlAttribute>().Where(a => a.Value.StartsWith("@", StringComparison.Ordinal)))
                    {
                        count++;
                        var property = typeof(ClanLordItemBadgeMixin).GetProperty(attribute.Value.Substring(1), Members);
                        if (property == null || property.CustomAttributes.All(a => !a.AttributeType.Name.StartsWith("DataSourceProperty", StringComparison.Ordinal)))
                            failures.Add($"{extension.Name} {attribute.Name}=\"{attribute.Value}\" has no [DataSourceProperty] on the mixin");
                    }
                }
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        Assert.AreEqual(2, count, "one binding on the overlay, one on the label");
    }

    [TestMethod]
    public void TheMixin_HooksUpdateProperties_OnTheClanRowViewModel()
    {
        var attribute = typeof(ClanLordItemBadgeMixin).GetCustomAttribute<ViewModelMixinAttribute>()!;

        Assert.AreEqual("UpdateProperties", attribute.RefreshMethodName);
        Assert.IsNotNull(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanLordItemVM).GetMethod("UpdateProperties", Type.EmptyTypes));
        Assert.AreSame(typeof(TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanLordItemVM),
            typeof(ClanLordItemBadgeMixin).BaseType!.GetGenericArguments().Single());
    }

    [TestMethod]
    public void TheMixinConstructor_DoesNotCallOnRefresh()
    {
        // UIExtenderEx 2.13.3 replays the view model constructor's refresh call on the mixin as OnRefresh, so a call here runs it twice.
        var source = ReadSource("Client/Features/HearthRoll/UI/ClanLordItemBadgeMixin.cs", stripComments: true);
        var constructor = Regex.Match(source, @"public \w+\([^)]*\)\s*:\s*base\([^)]*\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline);

        Assert.IsTrue(constructor.Success);
        StringAssert.DoesNotMatch(constructor.Groups["body"].Value, new Regex(@"OnRefresh\s*\("));
    }

    [TestMethod]
    public void TheMixin_ResolvesItsServicesInTheConstructor_NotOnEachRefresh()
    {
        var source = ReadSource("Client/Features/HearthRoll/UI/ClanLordItemBadgeMixin.cs", stripComments: true);
        var constructor = Regex.Match(source, @"public \w+\([^)]*\)\s*:\s*base\([^)]*\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline);
        Assert.IsTrue(constructor.Success);
        StringAssert.Contains(constructor.Groups["body"].Value, "IoC.Resolve<HearthRollSettingsProvider>()");
        StringAssert.Contains(constructor.Groups["body"].Value, "IoC.Resolve<IModLogger>()");

        var outside = source.Remove(constructor.Index, constructor.Length);
        StringAssert.DoesNotMatch(outside, new Regex(@"IoC\.Resolve"));
    }

    [TestMethod]
    public void TheMixinProperties_ClashWithNeitherTheRowViewModelNorTheFamilyMixin()
    {
        var mine = typeof(ClanLordItemBadgeMixin).GetProperties(Members | BindingFlags.DeclaredOnly)
            .Where(p => p.CustomAttributes.Any(a => a.AttributeType.Name.StartsWith("DataSourceProperty", StringComparison.Ordinal)))
            .Select(p => p.Name)
            .ToList();
        Assert.IsTrue(mine.Contains("IsTaomUnhappyCompanion") && mine.Contains("TaomAgeText"));

        var taken = typeof(TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanLordItemVM).GetProperties(Members)
            .Concat(typeof(ClanLordItemFamilyMixin).GetProperties(Members))
            .Select(p => p.Name)
            .ToHashSet();
        CollectionAssert.AreEqual(new string[0], mine.Where(taken.Contains).ToArray());
    }

    [TestMethod]
    public void TheModule_IsInFeatureModulesOnce_AndNothingTouchesTheSingleOwnerFiles()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<HearthRollModule>().Count());
        foreach (var owner in new[] { "Main/IoC.cs", "Main/SubModule.cs", "Client/TaomClientComposition.cs" })
            StringAssert.DoesNotMatch(ReadSource(owner, stripComments: true), new Regex("HearthRoll"), owner);
    }

    [TestMethod]
    public void TheModule_RegistersTheProvider_AndOwnsNoSaveData()
    {
        var module = new HearthRollModule();

        Assert.AreEqual("HearthRoll", module.Id);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.IsNull(module.ParkedReason);
        using var container = new Container();
        module.RegisterServices(container);
        Assert.AreSame(container.Resolve<HearthRollSettingsProvider>(), container.Resolve<HearthRollSettingsProvider>());
    }
}
