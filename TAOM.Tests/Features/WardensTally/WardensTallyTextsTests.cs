using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.WardensTally;

namespace TAOM.Tests.Features.WardensTally;

/// <summary>
/// The words of the tally (docs/features/wardens-tally.md): what each view becomes, with percents to one decimal and the
/// engine's own term text carried through untouched. The default English of every key is the text the player reads until
/// a translation lands.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class WardensTallyTextsTests
{
    private static string[] Lines(WardensTallyView view) =>
        WardensTallyTexts.Lines(view).Select(t => t.ToString()).ToArray();

    private static WardensTallyView View(int held = 34, int lords = 3) => new WardensTallyView { Held = held, Capacity = 60, Lords = lords };

    [DataTestMethod]
    [DataRow(0.04f, "4.0")]
    [DataRow(0.02f, "2.0")]
    [DataRow(0.0346f, "3.5")]
    [DataRow(0f, "0.0")]
    [DataRow(-0.00001f, "0.0")]
    [DataRow(0.00001f, "0.0")]
    [DataRow(1f, "100.0")]
    public void Percent_IsOneDecimal_WithoutTheSign_WhichTheRowHolds(float fraction, string expected)
    {
        Assert.AreEqual(expected, WardensTallyTexts.Percent(fraction));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void Percent_NonFinite_IsZero(float fraction)
    {
        Assert.AreEqual("0.0", WardensTallyTexts.Percent(fraction));
    }

    [DataTestMethod]
    [DataRow(-0.25f, "-25")]
    [DataRow(0.1f, "+10")]
    [DataRow(-0.125f, "-12.5")]
    [DataRow(0.125f, "+12.5")]
    [DataRow(0.5f, "+50")]
    [DataRow(-1f, "-100")]
    [DataRow(0.0002f, "0")]
    [DataRow(-0.0002f, "0")]
    [DataRow(float.NaN, "0")]
    [DataRow(float.PositiveInfinity, "0")]
    public void SignedPercent_IsOneDecimalAtMost_WithAnExplicitPlus_AndTheRowHoldsThePercentSign(float change, string expected)
    {
        Assert.AreEqual(expected, WardensTallyTexts.SignedPercent(change));
    }

    [TestMethod]
    public void Lines_HeldWithLords_SaysHowFullAndHowManyAreLords()
    {
        CollectionAssert.AreEqual(new[] { "Held: 34 of 60, 3 of them lords." }, Lines(View()));
    }

    [TestMethod]
    public void Lines_OneLord_SaysOneOfThemIsALord()
    {
        CollectionAssert.AreEqual(new[] { "Held: 12 of 60, 1 of them a lord." }, Lines(View(12, 1)));
    }

    [TestMethod]
    public void Lines_SharedOddsWithNothingElse_IsOneLine()
    {
        var view = View();
        view.Odds = new TallyOdds { Chance = 0.04f, Base = 0.04f };

        CollectionAssert.AreEqual(
            new[] { "Held: 34 of 60, 3 of them lords.", "Each lord's daily chance to escape: 4.0%" }, Lines(view));
    }

    [TestMethod]
    public void Lines_HalvedOddsWithTerms_ListTheBaseTheHalvingAndEachTerm()
    {
        var view = View();
        view.Odds = new TallyOdds { Chance = 0.0153f, Base = 0.04f, Halved = true };
        view.Odds.Terms.Add(new EscapeTermLine("Sweet Talker", 0.1f));
        view.Odds.Terms.Add(new EscapeTermLine("Dungeon Architect", -0.25f));

        CollectionAssert.AreEqual(
            new[]
            {
                "Held: 34 of 60, 3 of them lords.",
                "Each lord's daily chance to escape: 1.5%",
                "Base chance: 4.0%",
                "Held by your clan: halved",
                "Sweet Talker: +10% of the base chance",
                "Dungeon Architect: -25% of the base chance",
            },
            Lines(view));
    }

    [TestMethod]
    public void Lines_ABoostedLord_SaysHisCombinedChance()
    {
        var view = View();
        view.Boosts.Add(new TallyBoost("Boromir", 0.0784f));

        Assert.AreEqual("Boromir: 7.8% a day with the war's escape boost.", Lines(view).Last());
    }

    [TestMethod]
    public void Lines_AVetoedLord_CannotEscape()
    {
        var view = View();
        view.Vetoed.Add("Denethor");

        Assert.AreEqual("Denethor cannot escape.", Lines(view).Last());
    }

    [TestMethod]
    public void Lines_EverythingAtOnce_KeepsTheOrder_HeldOddsBoostVeto()
    {
        var view = View();
        view.Odds = new TallyOdds { Chance = 0.04f, Base = 0.04f };
        view.Boosts.Add(new TallyBoost("Boromir", 0.0784f));
        view.Vetoed.Add("Denethor");

        var lines = Lines(view);

        Assert.AreEqual(4, lines.Length);
        StringAssert.StartsWith(lines[0], "Held:");
        StringAssert.StartsWith(lines[1], "Each lord's");
        StringAssert.StartsWith(lines[2], "Boromir:");
        StringAssert.StartsWith(lines[3], "Denethor");
    }

    [TestMethod]
    public void Intro_PutsVanillasSentenceFirst_ThenTheTallyOnTheNextLine()
    {
        var view = View();
        view.Odds = new TallyOdds { Chance = 0.04f, Base = 0.04f };

        var text = WardensTallyTexts.Intro(view).ToString();

        Assert.AreEqual(
            "There are 3 prisoners here.\nHeld: 34 of 60, 3 of them lords.\nEach lord's daily chance to escape: 4.0%", text);
    }

    [DataTestMethod]
    [DataRow(1, "There is 1 prisoner here.")]
    [DataRow(2, "There are 2 prisoners here.")]
    public void VanillaIntro_RebuildsTheEnginesSentence(int lords, string expected)
    {
        Assert.AreEqual(expected, WardensTallyTexts.VanillaIntro(lords).ToString());
    }

    [TestMethod]
    public void VanillaIntro_UsesTheEnginesOwnKey_SoItsTranslationsApply()
    {
        Assert.AreEqual("gAc8SWDt", WardensTallyTexts.VanillaPrisonersKey);
        Assert.AreEqual("gAc8SWDt", WardensTallyTexts.VanillaIntro(2).GetID());
    }
}
