using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;

namespace TAOM.Tests.Features.ServerSettings;

[TestClass]
public class ServerSettingsDefaultsRendererTests
{
    [TestCleanup]
    public void Cleanup() => ServerSettingsSource.ResetForTests();

    [TestMethod]
    public void DefaultsReference_RoundTripsWithNothingReverted()
    {
        var text = ServerSettingsDefaultsRenderer.DefaultsReference(new ServerSettingsSet());
        var target = new ServerSettingsSet();

        var report = ServerSettingsReader.Read(text, target);

        var expected = ServerSettingsSchema.All.Count(d => d.IsSettableFromFile);
        Assert.AreEqual(ServerFileState.Loaded, report.State, string.Join("\n", report.Lines.Select(l => l.Text)));
        Assert.AreEqual(0, report.Reverted, string.Join("\n", report.Lines.Where(l => l.Level != ServerSettingsLevel.Info).Select(l => l.Text)));
        Assert.AreEqual(0, report.Unknown);
        Assert.AreEqual(expected, report.Applied);
    }

    [TestMethod]
    public void DefaultsReference_ListsEveryKeyOnce_WithAKindComment()
    {
        var text = ServerSettingsDefaultsRenderer.DefaultsReference(new ServerSettingsSet());

        foreach (var d in ServerSettingsSchema.All.Where(d => d.IsSettableFromFile))
            Assert.AreEqual(1, CountOf(text, "\"" + d.Name + "\":"), d.Name);
        Assert.IsTrue(text.Contains("/* int, 1 to 5, Castle Recruitment"), "the kind, range and group comment is missing for CastleNotablesPerCastle");
        Assert.IsTrue(text.Contains("never reads it"), "the header must say TAOM never reads this file");
    }

    [TestMethod]
    public void DefaultsReference_ShowsTheServerHitchProbeDefault()
    {
        var text = ServerSettingsDefaultsRenderer.DefaultsReference(new ServerSettingsSet());

        Assert.IsTrue(text.Contains("\"EnableHitchProbe\": false"), "a server keeps the hitch probe off unless the file sets it");
    }

    [TestMethod]
    public void DefaultsReference_IsDeterministic()
    {
        Assert.AreEqual(
            ServerSettingsDefaultsRenderer.DefaultsReference(new ServerSettingsSet()),
            ServerSettingsDefaultsRenderer.DefaultsReference(new ServerSettingsSet()));
    }

    [TestMethod]
    public void Template_IsAnEmptyObjectBehindComments_AndReadsAsZeroKeys()
    {
        var template = ServerSettingsDefaultsRenderer.Template();

        var report = ServerSettingsReader.Read(template, new ServerSettingsSet());

        Assert.AreEqual(ServerFileState.Loaded, report.State);
        Assert.AreEqual(0, report.Applied + report.Reverted + report.Unknown);
        Assert.IsTrue(template.Contains("taom-server-settings.defaults.json"), "the template should point at the reference file");
    }

    private static int CountOf(string text, string needle)
    {
        int count = 0, at = 0;
        while ((at = text.IndexOf(needle, at, System.StringComparison.Ordinal)) >= 0) { count++; at += needle.Length; }
        return count;
    }
}
