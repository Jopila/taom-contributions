using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Bannerlord.UIExtenderEx.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TAOM.Features.ReevesLedger.UI;
using TAOM.Tests.Migration;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.ReevesLedger;

/// <summary>
/// The XML the Reeve's Ledger inserts into the TownManagement movie. A binding that names nothing is a silent no-op in
/// Gauntlet, and a brush or sprite that does not exist hangs or blanks the widget, so every <c>@Binding</c>,
/// <c>{DataSource}</c> and <c>Command.*</c> in the inserted XML must resolve to a mixin member or a vanilla one, and every
/// brush and sprite must exist in the installed game's data. That the two XPaths still match the vanilla movie is the job of
/// <c>PrefabExtensionBindingTests</c>; this class pins that the targets are the intended ones.
/// </summary>
[TestClass]
public class ReevesLedgerPrefabTests
{
    private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly (Type Prefab, Type Mixin, Type Vm)[] Pairs =
    {
        (typeof(ReevesLedgerIconsPrefab), typeof(TownManagementLedgerMixin), typeof(TownManagementVM)),
        (typeof(ReserveStepButtonsPrefab), typeof(ReserveStepMixin), typeof(TownManagementReserveControlVM)),
    };

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static List<XmlElement> NodesOf(Type prefab)
    {
        var instance = Activator.CreateInstance(prefab, nonPublic: true)!;
        var method = prefab.GetMethods().Single(m => m.GetCustomAttributes(inherit: false).Any(a => a.GetType().Name == "PrefabExtensionXmlNodesAttribute"));
        return ((IEnumerable<XmlNode>)method.Invoke(instance, null)!).Cast<XmlElement>().ToList();
    }

    [TestMethod]
    public void BothExtensions_TargetTheTownManagementMovie_AtTheIntendedAnchors_AppendingAsSiblings()
    {
        var icons = (PrefabExtensionAttribute)typeof(ReevesLedgerIconsPrefab).GetCustomAttributes(typeof(PrefabExtensionAttribute), false).Single();
        var buttons = (PrefabExtensionAttribute)typeof(ReserveStepButtonsPrefab).GetCustomAttributes(typeof(PrefabExtensionAttribute), false).Single();

        Assert.AreEqual("TownManagement", icons.Movie);
        Assert.AreEqual("descendant::Widget[@Id='Consumptions']", icons.XPath);
        Assert.AreEqual("TownManagement", buttons.Movie);
        Assert.AreEqual("descendant::ListPanel[@Id='ReserveListPanel']", buttons.XPath);
        foreach (var (prefab, _, _) in Pairs)
            Assert.AreEqual(Bannerlord.UIExtenderEx.Prefabs2.InsertType.Append,
                ((Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch)Activator.CreateInstance(prefab, true)!).Type, prefab.Name);
    }

    [TestMethod]
    public void TheInsertedNodes_AreTwoIconsAndTwoButtons_WithTheDocumentedIds()
    {
        CollectionAssert.AreEqual(new[] { "TaomStashIcon", "TaomPrisonIcon" }, NodesOf(typeof(ReevesLedgerIconsPrefab)).Select(n => n.GetAttribute("Id")).ToArray());
        CollectionAssert.AreEqual(new[] { "TaomReserveWithdrawStep", "TaomReserveAddStep" }, NodesOf(typeof(ReserveStepButtonsPrefab)).Select(n => n.GetAttribute("Id")).ToArray());
        foreach (var wrapper in NodesOf(typeof(ReserveStepButtonsPrefab)))
        {
            var button = (XmlElement)wrapper.SelectSingleNode("Children/ButtonWidget")!;
            Assert.AreEqual("ButtonBrush2", button.GetAttribute("Brush"));
            Assert.AreEqual("TownManagement.Reserve.Amount.Text", button.SelectSingleNode("descendant::TextWidget")!.Attributes!["Brush"]!.Value);
        }
    }

    // Vanilla's governor button (SandBox TownManagement.xml:105-111): a fixed wrapper holds a HintWidget beside the button.
    // Gauntlet never hovers a disabled widget, so a hint inside the ButtonWidget cannot say why the button is disabled; the
    // wrapper's own hint, outside the button, can.
    [TestMethod]
    public void EachStepButton_IsAFixedWrapper_WithItsOwnHintOutsideTheButtonWidget()
    {
        var wrappers = NodesOf(typeof(ReserveStepButtonsPrefab));
        Assert.AreEqual(2, wrappers.Count);
        foreach (var wrapper in wrappers)
        {
            var id = wrapper.GetAttribute("Id");
            Assert.AreEqual("Widget", wrapper.Name, id);
            Assert.AreEqual("80", wrapper.GetAttribute("SuggestedWidth"), id);
            Assert.AreEqual("44", wrapper.GetAttribute("SuggestedHeight"), id);
            Assert.AreEqual("@IsReserveStepVisible", wrapper.GetAttribute("IsVisible"), id);
            Assert.AreEqual("", wrapper.GetAttribute("Brush"), id + ": the brush belongs to the inner button");
            foreach (var attr in new[] { "IsEnabled", "IsDisabled", "DoNotAcceptEvents" })
                Assert.IsFalse(wrapper.HasAttribute(attr), id + ": the wrapper must stay hoverable, so it carries no " + attr);

            var outerHint = (XmlElement?)wrapper.SelectSingleNode("Children/HintWidget");
            var button = (XmlElement?)wrapper.SelectSingleNode("Children/ButtonWidget");
            Assert.IsNotNull(outerHint, id + ": the wrapper holds its own HintWidget");
            Assert.IsNotNull(button, id + ": the wrapper holds the ButtonWidget");
            Assert.IsNull(outerHint.SelectSingleNode("ancestor::ButtonWidget"), id + ": the wrapper's hint must not sit inside the disabled button");
            Assert.IsTrue(outerHint.GetAttribute("DataSource").EndsWith("DisabledHint}", StringComparison.Ordinal), id);
            Assert.AreEqual("StretchToParent", button.GetAttribute("WidthSizePolicy"), id);
            Assert.AreEqual("StretchToParent", button.GetAttribute("HeightSizePolicy"), id);
            Assert.IsTrue(button.GetAttribute("IsEnabled").StartsWith("@Can", StringComparison.Ordinal), id);
            Assert.IsNotNull(button.SelectSingleNode("Children/HintWidget"), id + ": the enabled state keeps its own hint inside the button");
        }
    }

    [TestMethod]
    public void EveryBindingCommandAndDataSource_ResolvesToAMixinOrVanillaMember()
    {
        var failures = new List<string>();
        var bindings = 0;
        foreach (var (prefab, mixin, vm) in Pairs)
            foreach (var node in NodesOf(prefab))
                Walk(node, vm, mixin, prefab.Name, failures, ref bindings);

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        Assert.IsTrue(bindings >= 20, "the walk found only " + bindings + " bindings; it is not reading the inserted XML");
    }

    private static void Walk(XmlElement element, Type scope, Type? mixin, string owner, List<string> failures, ref int count)
    {
        // A DataSource on an element scopes the element's own bindings too.
        var dataSource = element.GetAttribute("DataSource");
        if (dataSource.StartsWith("{", StringComparison.Ordinal))
        {
            var member = Find(scope, dataSource.Trim('{', '}'), mixin);
            count++;
            if (member is not PropertyInfo property)
            {
                failures.Add($"{owner}: DataSource {dataSource} resolves to no property on {scope.Name} or its mixin.");
                return;
            }

            scope = property.PropertyType;
            mixin = null;
        }

        foreach (XmlAttribute attribute in element.Attributes)
        {
            var isBinding = attribute.Value.StartsWith("@", StringComparison.Ordinal);
            var isCommand = attribute.Name.StartsWith("Command.", StringComparison.Ordinal);
            if (!isBinding && !isCommand)
                continue;
            count++;
            var name = isBinding ? attribute.Value.Substring(1) : attribute.Value;
            if (Find(scope, name, mixin) == null)
                failures.Add($"{owner}: {attribute.Name}=\"{attribute.Value}\" on <{element.Name}> resolves to no member of {scope.Name} or its mixin.");
        }

        foreach (var child in element.SelectNodes("Children/*")!.Cast<XmlElement>())
            Walk(child, scope, mixin, owner, failures, ref count);
    }

    // A mixin member counts only with its UIExtenderEx attribute (the wrapper exposes nothing else); a vanilla member counts
    // at any visibility, as the Gauntlet binder reads private view model methods too.
    private static MemberInfo? Find(Type scope, string name, Type? mixin)
    {
        if (mixin != null)
        {
            var onMixin = mixin.GetMember(name, BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.CustomAttributes.Any(a => a.AttributeType == typeof(DataSourceProperty) || a.AttributeType == typeof(DataSourceMethodAttribute)));
            if (onMixin != null)
                return onMixin;
        }

        for (var type = scope; type != null; type = type.BaseType)
        {
            var member = type.GetMember(name, AnyInstance).FirstOrDefault(m => m is PropertyInfo || m is MethodInfo);
            if (member != null)
                return member;
        }

        return null;
    }

    [TestMethod]
    public void TheMixinsDataSources_AreTheTypesTheHintWidgetsNeed()
    {
        Assert.AreEqual(typeof(BasicTooltipViewModel), typeof(TownManagementLedgerMixin).GetProperty("StashTooltip")!.PropertyType);
        Assert.AreEqual(typeof(BasicTooltipViewModel), typeof(TownManagementLedgerMixin).GetProperty("PrisonTooltip")!.PropertyType);
        Assert.AreEqual(typeof(HintViewModel), typeof(ReserveStepMixin).GetProperty("AddReserveStepHint")!.PropertyType);
        Assert.AreEqual(typeof(HintViewModel), typeof(ReserveStepMixin).GetProperty("WithdrawReserveStepHint")!.PropertyType);
        Assert.AreEqual(typeof(HintViewModel), typeof(ReserveStepMixin).GetProperty("AddReserveStepDisabledHint")!.PropertyType);
        Assert.AreEqual(typeof(HintViewModel), typeof(ReserveStepMixin).GetProperty("WithdrawReserveStepDisabledHint")!.PropertyType);
        Assert.IsNotNull(typeof(BasicTooltipViewModel).GetMethod("ExecuteBeginHint"));
        Assert.IsNotNull(typeof(HintViewModel).GetMethod("ExecuteEndHint"));
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void BothBrushes_ExistInTheInstalledGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game install unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));

        var brushFiles = new[] { "Native", "SandBoxCore", "SandBox" }
            .Select(m => Path.Combine(GameAssemblies.GameDir, "Modules", m, "GUI", "Brushes"))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, "*.xml", SearchOption.AllDirectories))
            .Select(File.ReadAllText)
            .ToList();
        Assert.IsTrue(brushFiles.Count > 0, "no vanilla brush files found");

        var wanted = AllElements().Select(e => e.GetAttribute("Brush")).Where(b => b.Length > 0).Distinct().ToList();
        CollectionAssert.AreEquivalent(new[] { "ButtonBrush2", "TownManagement.Reserve.Amount.Text" }, wanted);
        foreach (var brush in wanted)
            Assert.IsTrue(brushFiles.Any(text => text.Contains($"<Brush Name=\"{brush}\"")), $"brush '{brush}' is in no vanilla Brushes file");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void BothSprites_ExistInTheAlwaysLoadedUiGroup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game install unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));

        var path = Path.Combine(GameAssemblies.GameDir, "Modules", "Native", "GUI", "NativeSpriteData.xml");
        Assert.IsTrue(File.Exists(path), path);
        var data = XDocument.Load(path).Root!;
        var alwaysLoaded = data.Descendants("SpriteCategory").Where(c => c.Element("AlwaysLoad") != null).Select(c => (string)c.Element("Name")!).ToList();

        var wanted = AllElements().Select(e => e.GetAttribute("Sprite")).Where(s => s.Length > 0).Distinct().ToList();
        CollectionAssert.AreEquivalent(new[] { "SPGeneral\\GameMenu\\stash_icon", "SPGeneral\\GameMenu\\manage_prisoners" }, wanted);
        foreach (var sprite in wanted)
        {
            var parts = data.Descendants("SpritePart").Where(p => (string?)p.Element("Name") == sprite).ToList();
            Assert.AreEqual(1, parts.Count, $"sprite '{sprite}' should be defined once");
            Assert.AreEqual("56", (string)parts[0].Element("Width")!, sprite);
            Assert.AreEqual("42", (string)parts[0].Element("Height")!, sprite);
            CollectionAssert.Contains(alwaysLoaded, (string)parts[0].Element("CategoryName")!, $"'{sprite}' is not in an AlwaysLoad sprite category");
        }
    }

    private static IEnumerable<XmlElement> AllElements() =>
        Pairs.SelectMany(p => NodesOf(p.Prefab)).SelectMany(n => new[] { n }.Concat(n.SelectNodes(".//*")!.Cast<XmlElement>()));
}
