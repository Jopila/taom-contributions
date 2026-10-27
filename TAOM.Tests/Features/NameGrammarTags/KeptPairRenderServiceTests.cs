using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.NameGrammarTags;

namespace TAOM.Tests.Features.NameGrammarTags;

[TestClass]
public class KeptPairRenderServiceTests
{
    private IKeptPairSourceAdapter _source = null!;
    private IGrammarRendererAdapter _renderer = null!;
    private IReportFileAdapter _files = null!;
    private KeptPairRenderService _sut = null!;
    private string _report = "";

    [TestInitialize]
    public void Setup()
    {
        _source = Substitute.For<IKeptPairSourceAdapter>();
        _renderer = Substitute.For<IGrammarRendererAdapter>();
        _files = Substitute.For<IReportFileAdapter>();
        _source.LanguageId("IT").Returns("Italiano");
        _source.TaomRows("IT").Returns(new Dictionary<string, string> { ["a"] = "taom {KINGDOM}", ["taom_only"] = "x", ["b"] = "taom {KINGDOM_LINK}" });
        _source.VanillaRows("IT").Returns(new Dictionary<string, string> { ["a"] = "vanilla {KINGDOM}", ["b"] = "vanilla {KINGDOM_LINK}" });
        _renderer.ActiveLanguageId.Returns("Italiano");
        _renderer.ActiveTextProcessor.Returns("ItalianTextProcessor");
        _renderer.KingdomIds().Returns(new[] { "k0", "k1" });
        _renderer.KingdomLabel(Arg.Any<string>()).Returns(ci => "Label " + ci.Arg<string>());
        _renderer.ClanIds(Arg.Any<string>()).Returns(new[] { "c0" });
        _renderer.AllClanIds().Returns(new[] { "cAll" });
        _renderer.FiefIds(Arg.Any<string>()).Returns(new[] { "s0" });
        _renderer.AllFiefIds().Returns(new[] { "sAll" });
        _renderer.CultureId(Arg.Any<string>()).Returns("culture");
        _renderer.Render(Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<RenderBinding>>())
            .Returns(ci => (IReadOnlyList<string>)ci.Arg<IReadOnlyList<string>>().Select(t => t.StartsWith("{=") ? "T" : "V").ToList());
        _renderer.RenderCanary(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns("di Roma");
        _files.Write(Arg.Any<string>(), Arg.Any<string>()).Returns(ci => { _report = ci.ArgAt<string>(1); return "C:\\logs\\report.txt"; });
        _sut = new KeptPairRenderService(_source, _renderer, _files);
    }

    private IEnumerable<IReadOnlyList<RenderBinding>> BindingsOfRenders() =>
        _renderer.ReceivedCalls().Where(c => c.GetMethodInfo().Name == "Render")
            .Select(c => (IReadOnlyList<RenderBinding>)c.GetArguments()[1]!);

    [TestMethod]
    public void Run_TwoSubjects_RendersEachPairForEachSubjectAndWritesOneReport()
    {
        var result = _sut.Run("IT", 2);

        _files.Received(1).Write(Arg.Is<string>(n => n.Contains("IT")), Arg.Any<string>());
        StringAssert.Contains(result, "C:\\logs\\report.txt");
        StringAssert.Contains(result, "2 kept pair(s)");
        foreach (var block in new[] { "a [Label k0]", "a [Label k1]", "b [Label k0]", "b [Label k1]" })
            StringAssert.Contains(_report, block);
        // Pair a has a plain kingdom variable (Name and InformalName), pair b only a link: 2 x 2 + 2 x 1 renders.
        _renderer.Received(6).Render(Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<RenderBinding>>());
    }

    [TestMethod]
    public void Run_Pair_RendersVanillaLiterallyAndTaomThroughItsKey()
    {
        _sut.Run("IT", 1);

        _renderer.Received().Render(Arg.Is<IReadOnlyList<string>>(t => t.Count == 2 && t[0] == "vanilla {KINGDOM}" && t[1] == "{=a}vanilla {KINGDOM}"),
            Arg.Any<IReadOnlyList<RenderBinding>>());
    }

    [TestMethod]
    public void Run_MoreKingdomsThanSubjects_PicksAnEvenSpread()
    {
        _renderer.KingdomIds().Returns(Enumerable.Range(0, 10).Select(i => "k" + i).ToArray());
        _renderer.KingdomLabel(Arg.Any<string>()).Returns(ci => ci.Arg<string>());

        _sut.Run("IT", 4);

        StringAssert.Contains(_report, "Subjects: k0, k2, k5, k7");
    }

    [TestMethod]
    public void Run_MoreSubjectsThanKingdoms_UsesEveryKingdomOnce()
    {
        _sut.Run("IT", 12);

        StringAssert.Contains(_report, "Subjects: Label k0, Label k1");
    }

    [TestMethod]
    public void Run_TwoKingdomVariables_BindDifferentKingdoms()
    {
        _source.TaomRows("IT").Returns(new Dictionary<string, string> { ["a"] = "t" });
        _source.VanillaRows("IT").Returns(new Dictionary<string, string> { ["a"] = "{KINGDOM} {OTHER_KINGDOM_LINK}" });

        _sut.Run("IT", 1);

        var first = BindingsOfRenders().First();
        Assert.AreEqual("k0", first.Single(b => b.Variable == "KINGDOM").ObjectId);
        Assert.AreEqual("k1", first.Single(b => b.Variable == "OTHER_KINGDOM_LINK").ObjectId);
    }

    [TestMethod]
    public void Run_KingdomWithNoClan_BindsAClanOfTheWholeCampaign()
    {
        _renderer.ClanIds("k0").Returns(Array.Empty<string>());
        _source.VanillaRows("IT").Returns(new Dictionary<string, string> { ["a"] = "{CLAN_NAME}" });
        _source.TaomRows("IT").Returns(new Dictionary<string, string> { ["a"] = "t" });

        _sut.Run("IT", 1);

        Assert.IsTrue(BindingsOfRenders().All(b => b.Single(x => x.Variable == "CLAN_NAME").ObjectId == "cAll"));
    }

    [TestMethod]
    public void Run_KingdomWithNoFief_BindsAFiefOfTheWholeCampaign()
    {
        _renderer.FiefIds("k0").Returns(Array.Empty<string>());
        _source.VanillaRows("IT").Returns(new Dictionary<string, string> { ["a"] = "{SETTLEMENT_LINK}" });
        _source.TaomRows("IT").Returns(new Dictionary<string, string> { ["a"] = "t" });

        _sut.Run("IT", 1);

        Assert.AreEqual("sAll", BindingsOfRenders().First().Single().ObjectId);
    }

    [TestMethod]
    public void Run_NoClanAnywhere_FailsTheLineInsteadOfThrowing()
    {
        _renderer.ClanIds(Arg.Any<string>()).Returns(Array.Empty<string>());
        _renderer.AllClanIds().Returns(Array.Empty<string>());
        _source.VanillaRows("IT").Returns(new Dictionary<string, string> { ["a"] = "{CLAN_NAME}" });
        _source.TaomRows("IT").Returns(new Dictionary<string, string> { ["a"] = "t" });

        _sut.Run("IT", 1);

        StringAssert.Contains(_report, "a [Label k0] FAILED");
    }

    [TestMethod]
    public void Run_Culture_BindsTheCultureOfTheKingdom()
    {
        _source.VanillaRows("IT").Returns(new Dictionary<string, string> { ["a"] = "{SETTLEMENT_CULTURE_NAME}" });
        _source.TaomRows("IT").Returns(new Dictionary<string, string> { ["a"] = "t" });

        _sut.Run("IT", 1);

        var binding = BindingsOfRenders().First().Single();
        Assert.AreEqual(NameKind.Culture, binding.Kind);
        Assert.AreEqual("culture", binding.ObjectId);
    }

    [TestMethod]
    public void Run_PlainKingdomVariable_RendersANameAndAnInformalNameVariant()
    {
        _source.TaomRows("IT").Returns(new Dictionary<string, string> { ["a"] = "t" });
        _source.VanillaRows("IT").Returns(new Dictionary<string, string> { ["a"] = "{KINGDOM}" });

        _sut.Run("IT", 1);

        var forms = BindingsOfRenders().Select(b => b.Single().Form).Distinct().OrderBy(f => f).ToArray();
        CollectionAssert.AreEqual(new[] { NameForm.Name, NameForm.Informal }, forms);
        StringAssert.Contains(_report, "[Name]");
        StringAssert.Contains(_report, "[InformalName]");
    }

    [TestMethod]
    public void Run_OnlyLinkVariables_RendersOneVariantAsBound()
    {
        _source.TaomRows("IT").Returns(new Dictionary<string, string> { ["a"] = "t" });
        _source.VanillaRows("IT").Returns(new Dictionary<string, string> { ["a"] = "{KINGDOM_LINK}" });

        _sut.Run("IT", 1);

        Assert.AreEqual(1, BindingsOfRenders().Count());
        StringAssert.Contains(_report, "[as bound]");
        Assert.IsFalse(_report.Contains("[InformalName]"));
    }

    [TestMethod]
    public void Run_FormalVariable_BindsTheFormalName()
    {
        _source.TaomRows("IT").Returns(new Dictionary<string, string> { ["a"] = "t" });
        _source.VanillaRows("IT").Returns(new Dictionary<string, string> { ["a"] = "{KINGDOM_FORMAL_NAME}" });

        _sut.Run("IT", 1);

        Assert.AreEqual(NameForm.Formal, BindingsOfRenders().First().Single().Form);
    }

    [TestMethod]
    public void Run_FactionVariable_IsAlsoRenderedAsAClan()
    {
        _source.TaomRows("IT").Returns(new Dictionary<string, string> { ["a"] = "t" });
        _source.VanillaRows("IT").Returns(new Dictionary<string, string> { ["a"] = "{FACTION_NAME}" });

        _sut.Run("IT", 1);

        var kinds = BindingsOfRenders().Select(b => b.Single().Kind).Distinct().ToArray();
        CollectionAssert.AreEquivalent(new[] { NameKind.Kingdom, NameKind.Clan }, kinds);
        StringAssert.Contains(_report, "faction as clan");
    }

    [TestMethod]
    public void Run_FactionVariableAlongsideAClanVariable_BindsADifferentClan()
    {
        _renderer.ClanIds("k0").Returns(new[] { "c0", "c1" });
        _source.TaomRows("IT").Returns(new Dictionary<string, string> { ["a"] = "t" });
        _source.VanillaRows("IT").Returns(new Dictionary<string, string> { ["a"] = "{CLAN_NAME} {FACTION_NAME}" });

        _sut.Run("IT", 1);

        var asClan = BindingsOfRenders().Where(b => b.Single(x => x.Variable == "FACTION_NAME").Kind == NameKind.Clan).ToList();
        Assert.IsTrue(asClan.Count > 0);
        Assert.IsTrue(asClan.All(b => b.Single(x => x.Variable == "CLAN_NAME").ObjectId == "c0"));
        Assert.IsTrue(asClan.All(b => b.Single(x => x.Variable == "FACTION_NAME").ObjectId == "c1"));
    }

    [TestMethod]
    public void Run_RendererReturnsTheEngineErrorText_IsMarkedFailed()
    {
        _renderer.Render(Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<RenderBinding>>())
            .Returns(ci => (IReadOnlyList<string>)new[] { "Error at id: x. Lang: Italiano", "ok" });

        var result = _sut.Run("IT", 1);

        StringAssert.Contains(_report, "a [Label k0] FAILED");
        Assert.IsFalse(_report.Contains(" SAME"));
        StringAssert.Contains(result, "render(s) failed");
    }

    [TestMethod]
    public void Run_RenderThatThrows_IsMarkedFailedAndTheRestStillRender()
    {
        _renderer.Render(Arg.Is<IReadOnlyList<string>>(t => t[0] == "vanilla {KINGDOM}"), Arg.Any<IReadOnlyList<RenderBinding>>())
            .Returns(_ => throw new InvalidOperationException("bad token"));

        var result = _sut.Run("IT", 1);

        StringAssert.Contains(_report, "render failed: bad token");
        StringAssert.Contains(_report, "a [Label k0] FAILED");
        StringAssert.Contains(_report, "b [Label k0] DIFFERENT");
        StringAssert.Contains(result, "render(s) failed");
    }

    [TestMethod]
    public void Run_TwoThrowingRendersWithTheSameMessage_AreNeverReportedSame()
    {
        _renderer.Render(Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<RenderBinding>>()).Returns(_ => throw new InvalidOperationException("same failure"));

        _sut.Run("IT", 1);

        Assert.IsFalse(_report.Contains(" SAME"));
        StringAssert.Contains(_report, "FAILED");
    }

    [TestMethod]
    public void Run_GameInAnotherLanguage_RendersNothingAndSaysWhichToSwitchTo()
    {
        _renderer.ActiveLanguageId.Returns("English");

        var result = _sut.Run("IT", 2);

        StringAssert.Contains(result, "Italiano");
        StringAssert.Contains(result, "English");
        _renderer.DidNotReceive().Render(Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<RenderBinding>>());
        _files.DidNotReceive().Write(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Run_UnknownLanguageFolder_SaysSo()
    {
        _source.LanguageId("XX").Returns((string?)null);

        var result = _sut.Run("XX", 2);

        StringAssert.Contains(result, "XX");
        _files.DidNotReceive().Write(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Run_NoKingdoms_SaysTheCampaignHasNone()
    {
        _renderer.KingdomIds().Returns(Array.Empty<string>());

        var result = _sut.Run("IT", 2);

        StringAssert.Contains(result, "no kingdom");
        _files.DidNotReceive().Write(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Run_NoKeptPairs_SaysSo()
    {
        _source.TaomRows("IT").Returns(new Dictionary<string, string> { ["taom_only"] = "x" });

        var result = _sut.Run("IT", 2);

        StringAssert.Contains(result, "No kept pair");
        _files.DidNotReceive().Write(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Run_ReportWriteFails_ReturnsTheErrorInsteadOfThrowing()
    {
        _files.Write(Arg.Any<string>(), Arg.Any<string>()).Returns(_ => throw new System.IO.IOException("disk full"));

        var result = _sut.Run("IT", 1);

        StringAssert.Contains(result, "disk full");
    }

    [TestMethod]
    public void Run_Report_HeaderNamesTheTextProcessorAndAPassingCanary()
    {
        _sut.Run("IT", 1);

        StringAssert.Contains(_report, "Text processor: ItalianTextProcessor");
        StringAssert.Contains(_report, "Canary: PASS");
    }

    [TestMethod]
    public void Run_ProcessorPrintsNoPreposition_ReportsAFailedCanaryInTheHeaderAndTheResult()
    {
        _renderer.ActiveTextProcessor.Returns("EnglishTextProcessor");
        _renderer.RenderCanary(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns("Roma");

        var result = _sut.Run("IT", 1);

        StringAssert.Contains(_report, "Canary: FAIL");
        StringAssert.Contains(result, "CANARY FAILED");
    }

    [TestMethod]
    public void Run_TwoSubjects_ReadsEachSubjectsClansAndFiefsOnce()
    {
        _source.VanillaRows("IT").Returns(new Dictionary<string, string> { ["a"] = "{CLAN_NAME} {SETTLEMENT}", ["b"] = "{CLAN_NAME}" });
        _source.TaomRows("IT").Returns(new Dictionary<string, string> { ["a"] = "t", ["b"] = "t" });

        _sut.Run("IT", 2);

        _renderer.Received(1).ClanIds("k0");
        _renderer.Received(1).FiefIds("k1");
    }

    [TestMethod]
    public void Run_CalledTwice_WritesTheSameReport()
    {
        // The renderer takes only ids and texts, so a second run needs nothing the first left behind.
        _sut.Run("IT", 2);
        var first = _report;
        _sut.Run("IT", 2);

        Assert.AreEqual(first, _report);
    }
}
