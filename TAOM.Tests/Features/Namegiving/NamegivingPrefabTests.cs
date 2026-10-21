using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Namegiving.UI;

namespace TAOM.Tests.Features.Namegiving;

/// <summary>
/// The XML Namegiving inserts into the kingdom Fiefs panel. A binding that names nothing is a silent no-op in Gauntlet, so
/// every <c>@Binding</c>, <c>{DataSource}</c> and <c>Command.*</c> must resolve to a mixin member or a vanilla one. The walk
/// reads attribute data and constructs no TaleWorlds attribute, so it runs without the game. That the XPath still matches the
/// vanilla prefab is <c>PrefabExtensionBindingTests</c> and <c>NamegivingLiveDataTests</c>.
/// </summary>
[TestClass]
public class NamegivingPrefabTests
{
    private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private static readonly Type Vm = typeof(TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.KingdomManagementVM);

    private static List<XmlElement> Nodes()
    {
        var prefab = typeof(FiefActionsRenamePrefab);
        var instance = Activator.CreateInstance(prefab, nonPublic: true)!;
        var method = prefab.GetMethods().Single(m => m.CustomAttributes.Any(a => a.AttributeType.Name == "PrefabExtensionXmlNodesAttribute"));
        return ((IEnumerable<XmlNode>)method.Invoke(instance, null)!).Cast<XmlElement>().ToList();
    }

    private static IEnumerable<XmlElement> All() =>
        Nodes().SelectMany(n => new[] { n }.Concat(n.SelectNodes(".//*")!.Cast<XmlElement>()));

    [TestMethod]
    public void TheExtension_InsertsAsTheLastChildOfTheActionButtonsContainer()
    {
        var attribute = CustomAttributeData.GetCustomAttributes(typeof(FiefActionsRenamePrefab))
            .Single(a => a.AttributeType.Name == "PrefabExtensionAttribute");

        Assert.AreEqual("FiefsPanel", attribute.ConstructorArguments[0].Value);
        Assert.AreEqual("descendant::ListPanel[@Id='ActionButtonsContainer']/Children", attribute.ConstructorArguments[1].Value);
        var insert = (Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionInsertPatch)Activator.CreateInstance(typeof(FiefActionsRenamePrefab), true)!;
        // Append would insert a SIBLING of the container (PrefabComponent), outside the action buttons' gamepad scope.
        Assert.AreEqual(Bannerlord.UIExtenderEx.Prefabs2.InsertType.Child, insert.Type);
        Assert.AreEqual(int.MaxValue, insert.Index, "Child inserts before the child at Index; int.MaxValue puts the block last");
    }

    [TestMethod]
    public void TheInsert_IsOneRootThatLeavesTheSettlementScope_ForTheKingdomManagementViewModel()
    {
        var nodes = Nodes();

        Assert.AreEqual(1, nodes.Count, "one root element");
        Assert.AreEqual("{..}", nodes[0].GetAttribute("DataSource"), "FiefsPanel is mounted with DataSource {Settlement}; the mixin lives one level up");
        Assert.AreEqual("@IsRenameVisible", nodes[0].GetAttribute("IsVisible"));
    }

    // Vanilla's governor-button shape (FiefsPanel.xml): a fixed, always-enabled wrapper holds the disabled-reason HintWidget beside the
    // button. Gauntlet never hovers a disabled widget, so a hint inside the disabled button could not say why (RCA reeves-ledger finding 1).
    [TestMethod]
    public void TheButton_IsAFixedEnabledWrapper_WithTheDisabledHintOutsideTheButtonWidget()
    {
        var wrapper = (XmlElement)Nodes()[0].SelectSingleNode(".//Widget[@Id='TaomRenameFiefFrame']")!;
        Assert.IsNotNull(wrapper);
        Assert.AreEqual("Fixed", wrapper.GetAttribute("WidthSizePolicy"));
        Assert.AreEqual("Fixed", wrapper.GetAttribute("HeightSizePolicy"));
        Assert.AreEqual("", wrapper.GetAttribute("Brush"), "the brush belongs to the inner button");
        foreach (var attr in new[] { "IsEnabled", "IsDisabled", "DoNotAcceptEvents", "DoNotPassEventsToChildren" })
            Assert.IsFalse(wrapper.HasAttribute(attr), "the wrapper must stay hoverable, so it carries no " + attr);

        var outerHint = (XmlElement?)wrapper.SelectSingleNode("Children/HintWidget");
        var button = (XmlElement?)wrapper.SelectSingleNode("Children/ButtonWidget");
        Assert.IsNotNull(outerHint);
        Assert.IsNotNull(button);
        Assert.IsNull(outerHint.SelectSingleNode("ancestor::ButtonWidget"), "the disabled hint's parent is not a button");
        Assert.AreEqual("{RenameDisabledHint}", outerHint.GetAttribute("DataSource"));
        Assert.AreEqual("@CanRename", button.GetAttribute("IsEnabled"));
        Assert.AreEqual("ExecuteRenameFief", button.GetAttribute("Command.Click"));
        Assert.AreEqual("{RenameHint}", ((XmlElement)button.SelectSingleNode("Children/HintWidget")!).GetAttribute("DataSource"), "the enabled state keeps its own hint inside the button");
    }

    [TestMethod]
    public void TheCostRow_ShowsTheCostAndTheInfluenceIcon()
    {
        var row = Nodes()[0];

        Assert.AreEqual("@RenameCost", row.SelectSingleNode(".//TextWidget[@IntText]")!.Attributes!["IntText"]!.Value);
        Assert.IsNotNull(row.SelectSingleNode(".//Widget[@Sprite='SPKingdom\\influence_icon_small']"));
    }

    [TestMethod]
    public void EveryBindingCommandAndDataSource_ResolvesToAMixinOrVanillaMember()
    {
        var failures = new List<string>();
        var count = 0;
        Walk(Nodes()[0], Vm, typeof(FiefNamingMixin), failures, ref count);

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        Assert.IsTrue(count >= 10, "the walk found only " + count + " bindings; it is not reading the inserted XML");
    }

    private static void Walk(XmlElement element, Type scope, Type? mixin, List<string> failures, ref int count)
    {
        var dataSource = element.GetAttribute("DataSource");
        if (dataSource.StartsWith("{", StringComparison.Ordinal))
        {
            count++;
            var path = dataSource.Trim('{', '}');
            if (path == "..")
            {
                // One level up from the FiefsPanel's Settlement view model is the KingdomManagementVM: the scope the walk started in.
                scope = Vm;
                mixin = typeof(FiefNamingMixin);
            }
            else
            {
                if (Find(scope, path, mixin) is not PropertyInfo property)
                {
                    failures.Add($"DataSource {dataSource} resolves to no property on {scope.Name} or its mixin.");
                    return;
                }

                scope = property.PropertyType;
                mixin = null;
            }
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
                failures.Add($"{attribute.Name}=\"{attribute.Value}\" on <{element.Name}> resolves to no member of {scope.Name} or its mixin.");
        }

        foreach (var child in element.SelectNodes("Children/*")!.Cast<XmlElement>())
            Walk(child, scope, mixin, failures, ref count);
    }

    // A mixin member counts only with its UIExtenderEx attribute; a vanilla member counts at any visibility.
    private static MemberInfo? Find(Type scope, string name, Type? mixin)
    {
        if (mixin != null)
        {
            var onMixin = mixin.GetMember(name, BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.CustomAttributes.Any(a => a.AttributeType.Name == "DataSourceProperty" || a.AttributeType.Name == "DataSourceMethodAttribute"));
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
        Assert.AreEqual("HintViewModel", typeof(FiefNamingMixin).GetProperty("RenameHint")!.PropertyType.Name);
        Assert.AreEqual("HintViewModel", typeof(FiefNamingMixin).GetProperty("RenameDisabledHint")!.PropertyType.Name);
        Assert.AreEqual(typeof(int), typeof(FiefNamingMixin).GetProperty("RenameCost")!.PropertyType);
        Assert.AreEqual(typeof(bool), typeof(FiefNamingMixin).GetProperty("IsRenameVisible")!.PropertyType);
        Assert.AreEqual(typeof(bool), typeof(FiefNamingMixin).GetProperty("CanRename")!.PropertyType);
    }

    [TestMethod]
    public void TheMixin_HooksRefreshValues_OnTheKingdomManagementViewModel()
    {
        var attribute = CustomAttributeData.GetCustomAttributes(typeof(FiefNamingMixin)).Single(a => a.AttributeType.Name == "ViewModelMixinAttribute");

        Assert.AreEqual("RefreshValues", attribute.ConstructorArguments[0].Value);
        Assert.IsNotNull(Vm.GetMethod("RefreshValues"));
        Assert.IsNotNull(Vm.GetMethod("OnFinalize"), "KingdomManagementVM declares OnFinalize, so the mixin does not stack a hook on the base ViewModel.OnFinalize");
        Assert.AreEqual(Vm, Vm.GetMethod("OnFinalize")!.DeclaringType);
    }
}
