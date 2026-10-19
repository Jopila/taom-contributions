using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ShadowfaxScreens;

namespace TAOM.Tests.Features.ShadowfaxScreens;

/// <summary>
/// Tests for <see cref="InstantTransitionRewriter"/>, the pure XML transform that makes every
/// VisualDefinition in a prefab change state at once (Shadowfax Screens).
/// </summary>
[TestClass]
public class InstantTransitionRewriterTests
{
    private static XmlDocument Doc(string xml)
    {
        var d = new XmlDocument();
        d.LoadXml(xml);
        return d;
    }

    // Shape of vanilla v1.5.5 CharacterDeveloper.xml: two panels, one of them with a delay.
    private const string CharacterDeveloperXml =
        "<Prefab><VisualDefinitions>" +
        "<VisualDefinition Name=\"TopPanel\" EaseType=\"EaseOut\" EaseFunction=\"Quint\" TransitionDuration=\"0.45\">" +
        "<VisualState PositionYOffset=\"-6\" State=\"Default\" />" +
        "</VisualDefinition>" +
        "<VisualDefinition Name=\"TopPanelExtension\" EaseType=\"EaseOut\" EaseFunction=\"Quint\" DelayOnBegin=\"0.7\" TransitionDuration=\"0.3\">" +
        "<VisualState PositionYOffset=\"0\" State=\"Default\" />" +
        "</VisualDefinition>" +
        "</VisualDefinitions><Window><Widget /></Window></Prefab>";

    private static XmlElement Def(XmlDocument doc, string name) =>
        (XmlElement)doc.SelectSingleNode($"//VisualDefinition[@Name='{name}']")!;

    [TestMethod]
    public void MakeInstant_AnimatedDefinitions_ZeroesEveryTransitionDuration()
    {
        var doc = Doc(CharacterDeveloperXml);

        int changed = InstantTransitionRewriter.MakeInstant(doc);

        Assert.AreEqual(2, changed);
        Assert.AreEqual("0", Def(doc, "TopPanel").GetAttribute("TransitionDuration"));
        Assert.AreEqual("0", Def(doc, "TopPanelExtension").GetAttribute("TransitionDuration"));
    }

    [TestMethod]
    public void MakeInstant_DefinitionWithoutTransitionDuration_AddsZero()
    {
        // Gauntlet gives a definition without the attribute a 0.2 s default (VisualDefinitionTemplate).
        var doc = Doc(
            "<Prefab><VisualDefinitions>" +
            "<VisualDefinition Name=\"Panel\"><VisualState PositionXOffset=\"5\" State=\"Default\" /></VisualDefinition>" +
            "</VisualDefinitions></Prefab>");

        int changed = InstantTransitionRewriter.MakeInstant(doc);

        Assert.AreEqual(1, changed);
        Assert.AreEqual("0", Def(doc, "Panel").GetAttribute("TransitionDuration"));
    }

    [TestMethod]
    public void MakeInstant_AnimatedDefinitions_KeepsEaseAttributesAndVisualStates()
    {
        var doc = Doc(CharacterDeveloperXml);

        InstantTransitionRewriter.MakeInstant(doc);

        var top = Def(doc, "TopPanel");
        Assert.AreEqual("EaseOut", top.GetAttribute("EaseType"));
        Assert.AreEqual("Quint", top.GetAttribute("EaseFunction"));
        Assert.AreEqual(1, top.SelectNodes("VisualState")!.Count);
        Assert.AreEqual("-6", ((XmlElement)top.SelectSingleNode("VisualState")!).GetAttribute("PositionYOffset"));
    }

    [TestMethod]
    public void MakeInstant_SecondRun_ChangesNothing()
    {
        var doc = Doc(CharacterDeveloperXml);
        InstantTransitionRewriter.MakeInstant(doc);

        int changed = InstantTransitionRewriter.MakeInstant(doc);

        Assert.AreEqual(0, changed);
    }

    [TestMethod]
    public void MakeInstant_VisualDefinitionOutsideTheVisualDefinitionsBlock_IsLeftAlone()
    {
        // WidgetPrefab.LoadFrom reads only Prefab/VisualDefinitions; anything else is not a definition.
        var doc = Doc(
            "<Prefab><VisualDefinitions /><Window>" +
            "<VisualDefinition Name=\"Stray\" TransitionDuration=\"0.5\" />" +
            "</Window></Prefab>");

        int changed = InstantTransitionRewriter.MakeInstant(doc);

        Assert.AreEqual(0, changed);
        Assert.AreEqual("0.5", Def(doc, "Stray").GetAttribute("TransitionDuration"));
    }

    [TestMethod]
    public void MakeInstant_PrefabWithoutVisualDefinitions_IsANoOp()
    {
        var doc = Doc("<Prefab><Window><Widget TransitionDuration=\"0.5\" /></Window></Prefab>");

        int changed = InstantTransitionRewriter.MakeInstant(doc);

        Assert.AreEqual(0, changed);
        Assert.AreEqual("0.5", ((XmlElement)doc.SelectSingleNode("//Widget")!).GetAttribute("TransitionDuration"),
            "only VisualDefinition nodes are touched");
    }

    [TestMethod]
    public void MakeInstant_NullDocument_ReturnsZero()
    {
        Assert.AreEqual(0, InstantTransitionRewriter.MakeInstant(null));
    }
}
