using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Main/_Module/SubModule.xml declares TAOM and, after it, the client-only TAOM.Client (ADR-013). The
/// tags are compared case-sensitively by the engine (IsNoRenderModeElement is true only for "false").
/// </summary>
[TestClass]
public class ClientSubModuleManifestTests
{
    private static List<XElement> SubModules() =>
        XDocument.Load(RepoPath("Main", "_Module", "SubModule.xml")).Root!
            .Element("SubModules")!.Elements("SubModule").ToList();

    private static XElement Named(string name) =>
        SubModules().Single(s => (string?)s.Element("Name")!.Attribute("value") == name);

    private static string? Value(XElement sub, string element) => (string?)sub.Element(element)?.Attribute("value");

    private static string? Tag(XElement sub, string key) => sub.Element("Tags")?.Elements("Tag")
        .FirstOrDefault(t => (string?)t.Attribute("key") == key)?.Attribute("value")?.Value;

    [TestMethod]
    public void Manifest_SubModules_AreTaomThenTaomClient()
    {
        var names = SubModules().Select(s => Value(s, "Name")).ToList();

        CollectionAssert.AreEqual(new[] { "TAOM", "TAOM.Client" }, names);
    }

    [TestMethod]
    public void Manifest_TaomClient_NamesItsDllAndClassAndCarriesBothClientOnlyTags()
    {
        var sub = Named("TAOM.Client");

        var type = typeof(TAOM.Client.ClientSubModule);
        Assert.AreEqual(type.Assembly.GetName().Name + ".dll", Value(sub, "DLLName"));
        Assert.AreEqual(type.FullName, Value(sub, "SubModuleClassType"));
        Assert.IsNotNull(type.GetConstructor(System.Type.EmptyTypes), "The engine calls GetConstructor with no parameters.");
        Assert.AreEqual("none", Tag(sub, "DedicatedServerType"));
        Assert.AreEqual("false", Tag(sub, "IsNoRenderModeElement"));
    }

    [TestMethod]
    public void Manifest_TaomCore_KeepsItsDllClassAndTagsUntilB9()
    {
        // B9 (the batch that makes core server-safe) flips this test with the tags.
        var sub = Named("TAOM");

        Assert.AreEqual("TAOM.dll", Value(sub, "DLLName"));
        Assert.AreEqual("TAOM.SubModule", Value(sub, "SubModuleClassType"));
        Assert.AreEqual("none", Tag(sub, "DedicatedServerType"));
        Assert.AreEqual("false", Tag(sub, "IsNoRenderModeElement"));
    }

    [TestMethod]
    public void Manifest_DependedModules_AreUnchanged()
    {
        var ids = XDocument.Load(RepoPath("Main", "_Module", "SubModule.xml")).Root!
            .Element("DependedModules")!.Elements("DependedModule")
            .Select(e => (string?)e.Attribute("Id")).ToList();

        CollectionAssert.AreEqual(new[] { "TAOM.Dependencies", "Native", "SandBoxCore", "Sandbox", "CustomBattle" }, ids);
    }
}
