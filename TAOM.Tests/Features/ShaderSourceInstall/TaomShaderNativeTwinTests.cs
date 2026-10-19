using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.ShaderSourceInstall;

/// <summary>
/// The engine-bump re-check for the bow shaders (#916): each <c>taom_deformer_*</c> source must stay the
/// installed vanilla <c>{kind}_deformer*</c> source plus exactly three differences, so a vanilla shader fix
/// in a bump is not silently lost in TAOM's copy. A failure after a bump is the prompt to re-derive the
/// copies from the new vanilla files.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class TaomShaderNativeTwinTests
{
    private static readonly string[] Kinds = { "bow", "longbow", "crossbow" };

    private static readonly string[] Passes =
        { "", "_gbuffer", "_shadowmap", "_pointlight", "_constant_output", "_overdraw" };

    private const string Standard = "#define my_material_id MATERIAL_ID_STANDART";
    private const string Metallic = "#define my_material_id MATERIAL_ID_METALLIC";
    private static readonly string[] Added = { "#define TableauMaskMap\ttexture10", "#define USE_METALLIC_WORKFLOW 1" };

    private static string ModuleFolder => RepoPaths.RepoPath("Main", "_Module", "Shaders", "Sources");

    private static string GameFolder()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("No Bannerlord install resolved; the vanilla shader sources are not available.");
        var folder = Path.Combine(GameAssemblies.GameDir, "Shaders", "Sources");
        if (!Directory.Exists(folder))
            Assert.Inconclusive("No Shaders\\Sources folder under " + GameAssemblies.GameDir);
        return folder;
    }

    private static List<string> Lines(string path) =>
        File.ReadAllText(path).Replace("\r\n", "\n").Split('\n').ToList();

    [TestMethod]
    public void ModuleFolder_HoldsExactlyTheEighteenTwinSources()
    {
        var expected = Kinds.SelectMany(k => Passes.Select(p => $"taom_deformer_{k}{p}.rs"))
            .OrderBy(n => n, System.StringComparer.Ordinal).ToList();
        var actual = Directory.GetFiles(ModuleFolder).Select(Path.GetFileName)
            .OrderBy(n => n, System.StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(expected, actual);
        Assert.AreEqual(18, actual.Count);
    }

    [TestMethod]
    public void EachSource_DiffersFromItsVanillaTwinByExactlyTheThreeKnownEdits()
    {
        var game = GameFolder();
        foreach (var kind in Kinds)
        {
            foreach (var pass in Passes)
            {
                var twin = Lines(Path.Combine(game, $"{kind}_deformer{pass}.rs"));
                var ours = Lines(Path.Combine(ModuleFolder, $"taom_deformer_{kind}{pass}.rs"));
                var label = $"taom_deformer_{kind}{pass}.rs";

                var expected = new List<string>(twin);
                var materialAt = expected.FindIndex(l => l.TrimEnd() == Standard);
                Assert.IsTrue(materialAt >= 0, label + ": the vanilla twin no longer holds '" + Standard + "'");
                expected[materialAt] = Metallic;
                expected.AddRange(Added);

                var onlyOurs = ours.Except(expected).ToList();
                var onlyTwin = expected.Except(ours).ToList();
                Assert.AreEqual(0, onlyOurs.Count, label + ": lines only in TAOM's copy: " + string.Join(" | ", onlyOurs));
                Assert.AreEqual(0, onlyTwin.Count, label + ": expected lines missing from TAOM's copy: " + string.Join(" | ", onlyTwin));
                CollectionAssert.AreEqual(expected.OrderBy(l => l, System.StringComparer.Ordinal).ToList(),
                    ours.OrderBy(l => l, System.StringComparer.Ordinal).ToList(), label + ": line multiset");

                // Order matters too: the defines only take effect above the first #include.
                var stripped = ours.Where(l => !Added.Contains(l)).Select(l => l.TrimEnd() == Metallic ? Standard : l).ToList();
                CollectionAssert.AreEqual(twin, stripped, label + ": order differs from the vanilla twin");
                var firstInclude = ours.FindIndex(l => l.TrimStart().StartsWith("#include"));
                foreach (var define in Added)
                    Assert.IsTrue(ours.IndexOf(define) < firstInclude, label + ": '" + define + "' sits below the first #include");
            }
        }
    }
}
