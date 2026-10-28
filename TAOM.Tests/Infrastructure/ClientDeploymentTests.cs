using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// How TAOM.Client.dll reaches the install (ADR-013): into the Steam client and editor bins of the TAOM module (Game Pass is not supported),
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
    public void ClientCsproj_DeploysIntoClientAndEditorBins_NeverTheServerOrGamePassBin()
    {
        var target = Target("Client/TAOM.Client.csproj", "DeployTaomClientToTaomModule");

        var destinations = target.Descendants("Copy").Select(c => (string?)c.Attribute("DestinationFolder") ?? "").ToList();
        foreach (var bin in new[] { "Win64_Shipping_Client", "Win64_Shipping_wEditor" })
            Assert.IsTrue(destinations.Any(d => d.Contains(bin)), $"No Copy into {bin}.");

        var attributeValues = target.DescendantsAndSelf().SelectMany(e => e.Attributes()).Select(a => a.Value).ToList();
        Assert.IsFalse(attributeValues.Any(v => v.Contains("Win64_Shipping_Server")),
            "A dedicated server must not carry TAOM.Client.dll.");
        Assert.IsFalse(attributeValues.Any(v => v.Contains("Gaming.Desktop")),
            "Game Pass is not supported (Mike, 2026-10-22): nothing deploys into its bin.");

        var condition = (string?)target.Attribute("Condition") ?? "";
        StringAssert.Contains(condition, "DisableModuleCopy");
        StringAssert.Contains(condition, "ModuleId");
    }

    // Every attribute value and property text inside a Target, except the places a target only talks about
    // a path: a Condition, an Error and a Message. An item Include or Exclude is a write in waiting.
    private static System.Collections.Generic.List<string> ValuesThatCouldWrite(string project) =>
        XDocument.Load(RepoPath(project.Split('/'))).Descendants()
            .Where(t => t.Name.LocalName == "Target")
            .SelectMany(t => t.Descendants())
            .Where(e => e.Name.LocalName is not ("Error" or "Message"))
            .SelectMany(e => e.Attributes().Where(a => a.Name.LocalName != "Condition").Select(a => a.Value)
                .Concat(e.HasElements ? Enumerable.Empty<string>() : new[] { e.Value }))
            .ToList();

    [DataTestMethod]
    [DataRow("Main/TAOM.csproj")]
    [DataRow("Client/TAOM.Client.csproj")]
    [DataRow("Dependencies/TAOM.Dependencies.csproj")]
    public void NoRepoTarget_WritesIntoAGamingDesktopFolder(string project)
    {
        var writers = ValuesThatCouldWrite(project).Where(v => v.Contains("Gaming.Desktop")).ToList();

        Assert.AreEqual(0, writers.Count,
            "Game Pass is not supported (Mike, 2026-10-22); no target stages, deploys or deletes in a Gaming.Desktop folder: "
            + string.Join(" | ", writers));
    }

    [TestMethod]
    public void NoRepoTargetScan_SeesAWriteThroughAnItem()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllText(path, "<Project><Target Name=\"T\"><ItemGroup><F Include=\"a\\Gaming.Desktop\\x\" />"
                + "</ItemGroup><Error Condition=\"Exists('Gaming.Desktop')\" Text=\"Gaming.Desktop\" /></Target></Project>");
            var values = XDocument.Load(path).Descendants().Where(t => t.Name.LocalName == "Target")
                .SelectMany(t => t.Descendants()).Where(e => e.Name.LocalName is not ("Error" or "Message"))
                .SelectMany(e => e.Attributes().Where(a => a.Name.LocalName != "Condition").Select(a => a.Value))
                .ToList();

            Assert.AreEqual(1, values.Count(v => v.Contains("Gaming.Desktop")), "The item Include is scanned; the Error is exempt.");
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    private static XElement GuardTarget() => Target("Client/TAOM.Client.csproj", "DeployTaomClientToTaomModule");

    private static string Attr(XElement e, string name) => (string?)e.Attribute(name) ?? "";

    [TestMethod]
    public void ClientCsproj_Deploy_FailsWhenTheInstalledCoreDiffersFromTheBuiltOne_BeforeAnyCopy()
    {
        var target = GuardTarget();

        // The Error compares the hash of the core the client was BUILT against with the INSTALLED one. Exact property
        // names: an Error comparing a hash with itself would pass every other check here.
        var error = target.Descendants("Error").Single(e => Attr(e, "Condition") == "'$(_TaomBuiltCoreHash)' != '$(_TaomInstalledCoreHash)'");
        StringAssert.Contains(Attr(error, "Text"), "TAOM");
        StringAssert.Contains(Attr(error, "Text").ToLowerInvariant(), "rebuild");

        var hashes = target.Descendants().Where(e => e.Name.LocalName == "GetFileHash").ToList();
        var built = hashes.Single(h => Attr(h, "Files") == "@(_TaomBuiltCore)");
        Assert.AreEqual("_TaomBuiltCoreHash", (string?)built.Element("Output")!.Attribute("PropertyName"));
        var installed = hashes.Single(h => Attr(h, "Files") == "$(_TaomInstalledCore)");
        Assert.AreEqual("_TaomInstalledCoreHash", (string?)installed.Element("Output")!.Attribute("PropertyName"));

        var props = target.Descendants("PropertyGroup").Elements().ToDictionary(p => p.Name.LocalName, p => p.Value);
        StringAssert.EndsWith(props["_TaomInstalledCore"], @"Win64_Shipping_Client\TAOM.dll");
        var builtCore = target.Descendants("_TaomBuiltCore").Single(i => Attr(i, "Include") == "@(ReferencePath)");
        // Unqualified %(Name) in an item condition fails the build with MSB4096 when any ReferencePath item lacks that
        // metadata, and framework facades such as netstandard.dll carry no ReferenceSourceTarget. Only a deploying build
        // (Visual Studio, build.ps1) runs this target, so the agent builds with -p:ModuleId= never reach it.
        Assert.AreEqual("'%(ReferencePath.Filename)' == 'TAOM' AND '%(ReferencePath.ReferenceSourceTarget)' == 'ProjectReference'",
            Attr(builtCore, "Condition"),
            "The built core is the client's own resolved ProjectReference output, selected with qualified metadata.");

        var firstCopy = target.Descendants("Copy").First();
        Assert.IsTrue(error.IsBefore(firstCopy), "The guard runs before the first copy.");
        Assert.IsTrue(hashes.All(h => h.IsBefore(firstCopy)), "The hashes are taken before the first copy.");
    }

    [TestMethod]
    public void ClientCsproj_Deploy_FailsWhenTheInstalledCoreIsMissing_BeforeHashingIt()
    {
        var target = GuardTarget();

        // Main's CopyBinariesWindows has already deployed the core whenever this target runs, so a missing core
        // means Main was skipped. Hashing a missing file would fail with a worse message.
        var error = target.Descendants("Error").Single(e => Attr(e, "Condition") == "!Exists('$(_TaomInstalledCore)')");
        var text = Attr(error, "Text");
        StringAssert.Contains(text, "Visual Studio's up-to-date check");
        StringAssert.Contains(text, "--no-dependencies");
        StringAssert.Contains(text.ToLowerInvariant(), "another build");
        var installedHash = target.Descendants().Single(e => e.Name.LocalName == "GetFileHash" && Attr(e, "Files") == "$(_TaomInstalledCore)");
        Assert.IsTrue(error.IsBefore(installedHash));
    }

    [TestMethod]
    public void ClientCsproj_Deploy_FailsWhenTheEditorCoreDiffersFromTheBuiltOne()
    {
        var target = GuardTarget();

        var props = target.Descendants("PropertyGroup").Elements().ToDictionary(p => p.Name.LocalName, p => p.Value);
        StringAssert.EndsWith(props["_TaomEditorCore"], @"Win64_Shipping_wEditor\TAOM.dll");
        var hash = target.Descendants().Single(e => e.Name.LocalName == "GetFileHash" && Attr(e, "Files") == "$(_TaomEditorCore)");
        StringAssert.Contains(Attr(hash, "Condition"), "Exists('$(_TaomEditorCore)')", "No editor bin yet is not a failure.");
        Assert.AreEqual("_TaomEditorCoreHash", (string?)hash.Element("Output")!.Attribute("PropertyName"));

        var error = target.Descendants("Error").Single(e =>
            Attr(e, "Condition") == "Exists('$(_TaomEditorCore)') AND '$(_TaomBuiltCoreHash)' != '$(_TaomEditorCoreHash)'");
        StringAssert.Contains(Attr(error, "Text").ToLowerInvariant(), "rebuild");
        Assert.IsTrue(error.IsBefore(target.Descendants("Copy").First()));
    }

    [DataTestMethod]
    [DataRow("Main/TAOM.csproj")]
    [DataRow("Dependencies/TAOM.Dependencies.csproj")]
    public void Csproj_FailsWhenAGamePassBinIsStagedUnderModule(string project)
    {
        // Bannerlord.BuildResources' CopyModule deploys everything under _Module, so a Gaming.Desktop folder left by an
        // older checkout would reach the install. Same condition as CopyModule's, not DisableModuleCopy.
        var target = Target(project, "FailOnGamePassStagingInModule");

        Assert.AreEqual("PostBuildCopyToModules", Attr(target, "BeforeTargets"));
        Assert.AreEqual("'$(ModuleId)' != '' AND Exists('$(GameFolder)')", Attr(target, "Condition"));
        var error = target.Descendants("Error").Single();
        StringAssert.Contains(Attr(error, "Condition"), @"Exists('$(ProjectDir)_Module\bin\Gaming.Desktop.x64_Shipping_Client\')");
        StringAssert.Contains(Attr(error, "Text").ToLowerInvariant(), "delete");
        StringAssert.Contains(Attr(error, "Text"), "Game Pass is not supported");
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

    // The Delete items of a target whose Include names `folder` and TAOM.Client., in document order.
    private static System.Collections.Generic.List<XElement> StaleClientDeletes(XElement target, string folder)
    {
        var itemNames = target.Descendants("ItemGroup").Elements()
            .Where(i => ((string?)i.Attribute("Include") ?? "").Contains(folder)
                        && ((string?)i.Attribute("Include") ?? "").Contains("TAOM.Client."))
            .Select(i => i.Name.LocalName).ToList();

        return target.Descendants("Delete")
            .Where(d => itemNames.Any(n => ((string?)d.Attribute("Files") ?? "") == $"@({n})"))
            .ToList();
    }

    [TestMethod]
    public void MainCsproj_EditorMirror_DeletesAStaleTaomClientBeforeItsCopy()
    {
        var target = Target("Main/TAOM.csproj", "MirrorWin64ShippingClientToEditor");

        var deletes = StaleClientDeletes(target, "Win64_Shipping_wEditor");
        Assert.AreEqual(1, deletes.Count,
            "A Main-only build leaves the editor bin's TAOM.Client built against an older core; the pair then runs old client code "
            + "against the new core, or aborts the game (CriticalError) if core changed a type the client binds at load.");
        var copy = target.Descendants("Copy").Single();
        Assert.IsTrue(deletes[0].IsBefore(copy), "The Delete must run before the additive Copy re-mirrors the client folder.");
    }

    [TestMethod]
    public void MainCsproj_EditorMirror_ToleratesALockedTaomClient()
    {
        var target = Target("Main/TAOM.csproj", "MirrorWin64ShippingClientToEditor");

        var delete = StaleClientDeletes(target, "Win64_Shipping_wEditor").Single();
        Assert.AreEqual("true", (string?)delete.Attribute("TreatErrorsAsWarnings"),
            "The Modding Kit holds TAOM.Client.dll open; a locked delete must not fail a build in which nothing changed.");
    }

    [DataTestMethod]
    [DataRow("MirrorWin64ShippingClientToEditor")]
    [DataRow("MirrorWin64ShippingClientToServer")]
    public void MainCsproj_Mirrors_RunAfterTheModuleCopy(string targetName)
    {
        var target = Target("Main/TAOM.csproj", targetName);

        Assert.AreEqual("PostBuildCopyToModules", (string?)target.Attribute("AfterTargets"),
            "The source bin is only populated once Bannerlord.BuildResources has copied into it.");
    }

    [TestMethod]
    public void DependenciesCsproj_EditorMirror_CopiesTheAssembledClientBinIntoTheEditorBin()
    {
        var target = Target("Dependencies/TAOM.Dependencies.csproj", "MirrorWin64ShippingClientToEditor");

        Assert.AreEqual("PostBuildCopyToModules", (string?)target.Attribute("AfterTargets"),
            "The source bin is only populated once Bannerlord.BuildResources has copied into it.");
        var copy = target.Descendants("Copy").Single();
        StringAssert.Contains((string?)copy.Attribute("DestinationFolder") ?? "", "Win64_Shipping_wEditor",
            "Without it the Modding Kit finds no Harmony, MCM or UIExtenderEx, and TAOM fails to load in the editor.");
        var source = target.Descendants("ItemGroup").Elements()
            .Single(i => $"@({i.Name.LocalName})" == (string?)copy.Attribute("SourceFiles"));
        StringAssert.Contains((string?)source.Attribute("Include") ?? "", @"bin\Win64_Shipping_Client\*.*");
    }

    [TestMethod]
    public void ClientCsproj_Deploy_RunsAfterBuild()
    {
        var target = Target("Client/TAOM.Client.csproj", "DeployTaomClientToTaomModule");

        Assert.AreEqual("Build", (string?)target.Attribute("AfterTargets"));
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
