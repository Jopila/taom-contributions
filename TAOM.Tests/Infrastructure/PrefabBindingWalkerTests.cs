using System.Linq;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>Pins the walker's scope stack: each <c>{..}</c> pops one level, as Gauntlet does, however deep the nesting.</summary>
[TestClass]
public class PrefabBindingWalkerTests
{
    private class RootVm { public ChildVm Child { get; set; } = new(); public string OnRoot { get; set; } = ""; }
    private class ChildVm { public LeafVm Leaf { get; set; } = new(); public string OnChild { get; set; } = ""; }
    private class LeafVm { public string OnLeaf { get; set; } = ""; }
    private class OuterVm { public string OnOuter { get; set; } = ""; }
    private class BaseVm { private string Hidden { get; set; } = ""; protected string Shared { get; set; } = ""; private void ExecuteHidden() { } }
    private class KindsVm : BaseVm { public string Name { get; set; } = ""; public void ExecuteGo() { } private string Own { get; set; } = ""; }

    private static XmlElement Parse(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return doc.DocumentElement!;
    }

    [TestMethod]
    public void Walk_DotDotUnderTwoPathSteps_PopsOneLevelEach()
    {
        // Root > {Child} > {Leaf} > {..} is Child's scope, then {..} again is Root's.
        var root = Parse(@"<Widget DataSource=""{Child}""><Children>
            <Widget DataSource=""{Leaf}""><Children>
              <Widget DataSource=""{..}"" Text=""@OnChild""><Children>
                <Widget DataSource=""{..}"" Text=""@OnRoot"" />
              </Children></Widget>
              <Widget Text=""@OnLeaf"" />
            </Children></Widget>
          </Children></Widget>");

        var (failures, count) = PrefabBindingWalker.Walk(root, typeof(RootVm), null);

        Assert.AreEqual(0, failures.Count, string.Join("; ", failures));
        Assert.AreEqual(7, count);
    }

    [TestMethod]
    public void Walk_DotDotUnderTwoPathSteps_DoesNotReachTheRootWithOnePop()
    {
        var root = Parse(@"<Widget DataSource=""{Child}""><Children>
            <Widget DataSource=""{Leaf}""><Children>
              <Widget DataSource=""{..}"" Text=""@OnRoot"" />
            </Children></Widget>
          </Children></Widget>");

        var (failures, _) = PrefabBindingWalker.Walk(root, typeof(RootVm), null);

        Assert.AreEqual(1, failures.Count);
        StringAssert.Contains(failures.Single(), "ChildVm");
    }

    [TestMethod]
    public void Walk_DotDotBelowTheSuppliedParent_ReturnsToTheParent()
    {
        var root = Parse(@"<Widget><Children><Widget DataSource=""{..}"" Text=""@OnOuter"" /></Children></Widget>");

        var (failures, _) = PrefabBindingWalker.Walk(root, typeof(RootVm), null, typeof(OuterVm), null);

        Assert.AreEqual(0, failures.Count, string.Join("; ", failures));
    }

    [TestMethod]
    public void Walk_DotDotWithNoParent_Fails()
    {
        var root = Parse(@"<Widget DataSource=""{..}"" />");

        var (failures, _) = PrefabBindingWalker.Walk(root, typeof(RootVm), null);

        Assert.AreEqual(1, failures.Count);
    }

    [TestMethod]
    public void Walk_SiblingBranches_KeepIndependentScopes()
    {
        // A pop in one branch must not leak into the next sibling.
        var root = Parse(@"<Widget><Children>
            <Widget DataSource=""{Child}""><Children><Widget DataSource=""{..}"" Text=""@OnRoot"" /></Children></Widget>
            <Widget DataSource=""{Child}"" Text=""@OnChild"" />
          </Children></Widget>");

        var (failures, _) = PrefabBindingWalker.Walk(root, typeof(RootVm), null);

        Assert.AreEqual(0, failures.Count, string.Join("; ", failures));
    }

    [TestMethod]
    public void Walk_ABindingNamingAMethod_Fails_GauntletReadsPropertiesOnly()
    {
        var (failures, _) = PrefabBindingWalker.Walk(Parse(@"<Widget Text=""@ExecuteGo"" />"), typeof(KindsVm), null);
        Assert.AreEqual(1, failures.Count);
    }

    [TestMethod]
    public void Walk_ACommandNamingAProperty_Fails_GauntletRunsMethodsOnly()
    {
        var (failures, _) = PrefabBindingWalker.Walk(Parse(@"<Widget Command.Click=""Name"" />"), typeof(KindsVm), null);
        Assert.AreEqual(1, failures.Count);
    }

    [TestMethod]
    public void Walk_APrivateMemberOfABaseClass_Fails_TheBinderNeverSeesIt()
    {
        var (failures, _) = PrefabBindingWalker.Walk(
            Parse(@"<Widget Text=""@Hidden"" Command.Click=""ExecuteHidden"" />"), typeof(KindsVm), null);
        Assert.AreEqual(2, failures.Count, string.Join("; ", failures));
    }

    [TestMethod]
    public void Walk_OwnPrivateAndInheritedProtectedMembers_Resolve()
    {
        var (failures, count) = PrefabBindingWalker.Walk(
            Parse(@"<Widget Text=""@Own"" Hint=""@Shared"" Value=""@Name"" Command.Click=""ExecuteGo"" />"), typeof(KindsVm), null);
        Assert.AreEqual(0, failures.Count, string.Join("; ", failures));
        Assert.AreEqual(4, count);
    }
}
