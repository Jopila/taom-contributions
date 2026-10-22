using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// How TAOM.Client.dll reaches the install (ADR-013): into the three client bins of the TAOM module,
/// never the dedicated-server bin, and without Bannerlord.BuildResources.
/// </summary>
[TestClass]
public class ClientDeploymentTests
{
    private static XElement Target(string project, string name) =>
        XDocument.Load(RepoPath(project.Split('/'))).Descendants()
            .Single(t => t.Name.LocalName == "Target" && (string?)t.Attribute("Name") == name);

    [TestMethod]
    public void ClientCsproj_DoesNotUseBannerlordBuildResources()
    {
        var refs = XDocument.Load(RepoPath("Client", "TAOM.Client.csproj")).Descendants("PackageReference")
            .Select(p => (string?)p.Attribute("Include")).ToList();

        CollectionAssert.DoesNotContain(refs, "Bannerlord.BuildResources",
            "Its CopyBinariesWindows cleans every .dll/.pdb/.config in Modules\\TAOM\\bin\\Win64_Shipping_Client "
            + "(Basic.targets, Clean=\"true\"), which would delete TAOM.dll. Deploy through DeployTaomClientToTaomModule.");
    }

    [TestMethod]
    public void ClientCsproj_ReferencesCoreWithPrivateFalse()
    {
        var reference = XDocument.Load(RepoPath("Client", "TAOM.Client.csproj")).Descendants("ProjectReference")
            .Single(p => ((string?)p.Attribute("Include"))!.EndsWith("Main\\TAOM.csproj", System.StringComparison.Ordinal));

        Assert.AreEqual("False", reference.Element("Private")?.Value, "TAOM.dll ships from Main's own deploy.");
    }

    [TestMethod]
    public void ClientCsproj_DeploysIntoClientEditorAndGamePassBins_NeverTheServerBin()
    {
        var target = Target("Client/TAOM.Client.csproj", "DeployTaomClientToTaomModule");

        var destinations = target.Descendants("Copy").Select(c => (string?)c.Attribute("DestinationFolder") ?? "").ToList();
        foreach (var bin in new[] { "Win64_Shipping_Client", "Win64_Shipping_wEditor", "Gaming.Desktop.x64_Shipping_Client" })
            Assert.IsTrue(destinations.Any(d => d.Contains(bin)), $"No Copy into {bin}.");

        var attributeValues = target.DescendantsAndSelf().SelectMany(e => e.Attributes()).Select(a => a.Value).ToList();
        Assert.IsFalse(attributeValues.Any(v => v.Contains("Win64_Shipping_Server")),
            "A dedicated server must not carry TAOM.Client.dll.");

        var condition = (string?)target.Attribute("Condition") ?? "";
        StringAssert.Contains(condition, "DisableModuleCopy");
        StringAssert.Contains(condition, "ModuleId");
    }

    [TestMethod]
    public void ClientCsproj_DeploysExactlyItsOwnAssemblyAndPdb()
    {
        var target = Target("Client/TAOM.Client.csproj", "DeployTaomClientToTaomModule");

        var includes = target.Descendants("_TaomClientFiles").Select(f => (string?)f.Attribute("Include")).ToList();

        CollectionAssert.AreEqual(new[] { "$(TargetPath)", "$(TargetDir)$(TargetName).pdb" }, includes,
            "Anything else (MCMv5, TAOM.Dependencies, a wildcard) would land in the TAOM bin and shadow the real copy.");
    }

    [TestMethod]
    public void MainCsproj_ServerMirror_ExcludesTaomClientAndDeletesAStaleCopy()
    {
        var target = Target("Main/TAOM.csproj", "MirrorWin64ShippingClientToServer");

        var mirror = target.Descendants("_TaomServerMirrorFiles").Single();
        StringAssert.Contains((string?)mirror.Attribute("Exclude") ?? "", "TAOM.Client.");

        var stale = target.Descendants("_TaomServerClientFiles").Single();
        var include = (string?)stale.Attribute("Include") ?? "";
        StringAssert.Contains(include, "Win64_Shipping_Server");
        StringAssert.Contains(include, "TAOM.Client.");

        var deletes = target.Descendants("Delete").Select(d => (string?)d.Attribute("Files")).ToList();
        CollectionAssert.Contains(deletes, "@(_TaomServerClientFiles)");
    }

    [TestMethod]
    public void DirtyFlagPathspec_CoversTheClientProject()
    {
        var target = Target("Directory.Build.props", "TaomStampWorkingTreeState");
        var command = (string?)target.Descendants("Exec").First().Attribute("Command") ?? "";

        var tokens = command.Split(' ').ToList();
        var from = tokens.IndexOf("--");
        var to = tokens.FindIndex(t => t.StartsWith("2>", System.StringComparison.Ordinal));
        Assert.IsTrue(from >= 0 && to > from, "The git status pathspec shape changed.");
        CollectionAssert.Contains(tokens.GetRange(from + 1, to - from - 1), "Client",
            "A release stamp reads clean over uncommitted client edits without it.");
    }
}
