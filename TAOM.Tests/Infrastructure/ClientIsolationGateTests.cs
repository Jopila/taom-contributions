using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Core never references TAOM.Client (ADR-013). The client submodule reaches core, never the reverse;
/// core sees the client only through TAOM.Composition.ClientComposition.
/// </summary>
[TestClass]
public class ClientIsolationGateTests
{
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void CoreAssemblies_DoNotReferenceTaomClient()
    {
        var core = new[] { typeof(TAOM.SubModule).Assembly, typeof(TAOM.Dependencies.SubModule).Assembly };

        var offenders = core
            .SelectMany(a => a.GetReferencedAssemblies().Select(r => $"{a.GetName().Name} -> {r.Name}"))
            .Where(s => s.EndsWith("-> TAOM.Client", StringComparison.Ordinal))
            .ToList();

        CollectionAssert.AreEqual(new string[0], offenders,
            "Core reaches the client only through ClientComposition (ADR-013).");
    }

    // Each of these is found by a scan over Main (or the built assembly) that does not read Client/.
    // Wire the scan first, then move the declaration.
    private static readonly Regex ForbiddenInTheClient = new Regex(
        @"\[\s*(HarmonyPatch|PrefabExtension|ViewModelMixin)\b|\bSaveableTypeDefiner\b"
        + @"|\b(AttributeGlobalSettings|AttributePerCampaignSettings|AttributePerSaveSettings)\b|\bMCM\.Abstractions\b");

    [TestMethod]
    public void ClientSource_DeclaresNoPatchUiExtensionSaveDefinerOrSettingsType()
    {
        var files = RepoPaths.CSharpFiles("Client").ToList();
        Assert.IsTrue(files.Count > 0, "The scan read no file under Client/.");

        var offenders = files
            .Where(f => ForbiddenInTheClient.IsMatch(RepoPaths.StripComments(File.ReadAllText(f))))
            .Select(Path.GetFileName)
            .ToList();

        CollectionAssert.AreEqual(new string[0], offenders,
            "TAOM.Client may not declare a [HarmonyPatch] class, a UIExtenderEx PrefabExtension or ViewModelMixin, a "
            + "SaveableTypeDefiner or an MCM settings type until the scan that finds it also reads Client/: "
            + "PatchCategoryIndex.Build (Main/SubModule.cs, the patch-category pass), RegisterUiExtensions "
            + "(Main/SubModule.cs, the UIExtenderEx registration), the save definer registration and the localization scans.");
    }

    [DataTestMethod]
    [DataRow("[HarmonyPatch(typeof(X), \"Y\")]")]
    [DataRow("[PrefabExtension]")]
    [DataRow("[ViewModelMixin(\"Z\")]")]
    [DataRow("class D : SaveableTypeDefiner")]
    [DataRow("class S : AttributeGlobalSettings<S>")]
    [DataRow("class S : AttributePerCampaignSettings<S>")]
    [DataRow("class S : AttributePerSaveSettings<S>")]
    [DataRow("using MCM.Abstractions.Base;")]
    public void TripwirePattern_MatchesEachForbiddenDeclaration(string source) =>
        Assert.IsTrue(ForbiddenInTheClient.IsMatch(source), source);

    [TestMethod]
    public void CoreSource_NamesTaomClientOnlyInComments()
    {
        var repoDir = RepoPaths.RepoPath();
        var offenders = RepoPaths.CSharpFiles("Main", "Dependencies")
            .Select(f => f.Substring(repoDir.Length).TrimStart('\\', '/').Replace('\\', '/'))
            .Where(rel => RepoPaths.MentionsOutsideComments(Path.Combine(repoDir, rel), "\"TAOM.Client"))
            .ToList();

        CollectionAssert.AreEqual(new string[0], offenders,
            "A string literal naming TAOM.Client (Type.GetType, Assembly.Load) is a back door; core reaches the client only through ClientComposition (ADR-013).");
    }
}
