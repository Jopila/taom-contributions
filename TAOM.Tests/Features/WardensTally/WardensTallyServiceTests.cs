using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WardensTally;

namespace TAOM.Tests.Features.WardensTally;

/// <summary>
/// The pure tally (docs/features/wardens-tally.md, #903): what the dungeon menu says about its prisoners and the lords'
/// daily escape odds, built from one <see cref="DungeonReading"/>. The odds come from <see cref="WarEscapeService"/>, the
/// one copy of vanilla's constants, so these tests pin the shape and the arithmetic over it, not 0.04 a second time.
/// </summary>
[TestClass]
public class WardensTallyServiceTests
{
    private readonly WarEscapeService _escape = new WarEscapeService();
    private readonly WardensTallyService _sut;

    public WardensTallyServiceTests() => _sut = new WardensTallyService(_escape);

    private static DungeonLord Lord(string name, string kingdom = "empire_w", float factor = 1f)
    {
        return new DungeonLord
        {
            Name = name,
            Snapshot = new PrisonerEscapeSnapshot
            {
                HeroId = "lord_" + name,
                KingdomId = kingdom,
                IsAlive = true,
                IsPrisoner = true,
                CanBeReleased = true,
                EscapeFactor = factor,
            },
        };
    }

    private static DungeonReading Reading(int regulars, params DungeonLord[] lords) => new DungeonReading
    {
        HeldRegulars = regulars,
        HeldLords = lords.Length,
        Capacity = 100,
        Lords = lords.ToList(),
    };

    private static float NoBoost(string kingdom) => 1f;

    private WardensTallyView Build(DungeonReading reading, Func<string, float>? boost = null) =>
        _sut.Build(reading, boost ?? NoBoost)!;

    [TestMethod]
    public void Build_NoReading_IsNull()
    {
        Assert.IsNull(_sut.Build(null, NoBoost));
    }

    [TestMethod]
    public void Build_EmptyDungeon_IsNull()
    {
        Assert.IsNull(_sut.Build(Reading(0), NoBoost));
    }

    [TestMethod]
    public void Build_NegativeCounts_IsNull()
    {
        var reading = Reading(0, Lord("Boromir"));
        reading.HeldRegulars = -4;

        Assert.IsNull(_sut.Build(reading, NoBoost));
    }

    [TestMethod]
    public void Build_RegularsOnly_IsNull_SoVanillasNoPrisonersSentenceIsNotLeftAboveACount()
    {
        // Vanilla's sentence counts prisoner heroes only, so with no lord it says "There are no prisoners here."
        Assert.IsNull(_sut.Build(Reading(34), NoBoost));
    }

    [TestMethod]
    public void Build_NegativeLordCount_IsNull()
    {
        var reading = Reading(5, Lord("Boromir"));
        reading.HeldLords = -1;

        Assert.IsNull(_sut.Build(reading, NoBoost));
    }

    [TestMethod]
    public void Build_LordsAreCountedAmongTheHeld()
    {
        var view = Build(Reading(31, Lord("Boromir"), Lord("Faramir"), Lord("Beregond")));

        Assert.AreEqual(34, view.Held);
        Assert.AreEqual(3, view.Lords);
    }

    [TestMethod]
    public void Build_BaseOnly_SharesVanillasChance_WithNoTerms()
    {
        var view = Build(Reading(0, Lord("Boromir"), Lord("Faramir")));

        Assert.IsNotNull(view.Odds);
        Assert.AreEqual(_escape.VanillaChance(Lord("x").Snapshot), view.Odds!.Chance, 1e-6f);
        Assert.AreEqual(view.Odds.Chance, view.Odds.Base, 1e-6f);
        Assert.IsFalse(view.Odds.Halved);
        Assert.AreEqual(0, view.Odds.Terms.Count);
    }

    [TestMethod]
    public void Build_HeldByThePlayersClan_IsHalved()
    {
        var held = Lord("Boromir");
        held.Snapshot.PlayerHeld = true;
        var free = _escape.VanillaChance(Lord("x").Snapshot);

        var view = Build(Reading(0, held));

        Assert.IsTrue(view.Odds!.Halved);
        Assert.AreEqual(free / 2f, view.Odds.Chance, 1e-6f);
        Assert.AreEqual(free, view.Odds.Base, 1e-6f, "the base is shown before the halving");
    }

    [TestMethod]
    public void Build_EachPerkTerm_KeepsItsDescriptionAndSign_InTheEnginesOrder()
    {
        var lord = Lord("Boromir", factor: 1f + 0.1f - 0.25f);
        lord.Terms.Add(new EscapeTermLine("Sweet Talker", 0.1f));
        lord.Terms.Add(new EscapeTermLine("Dungeon Architect", -0.25f));

        var view = Build(Reading(0, lord));

        Assert.AreEqual(2, view.Odds!.Terms.Count);
        Assert.AreEqual("Sweet Talker", view.Odds.Terms[0].Description);
        Assert.AreEqual(0.1f, view.Odds.Terms[0].Change, 1e-6f);
        Assert.AreEqual("Dungeon Architect", view.Odds.Terms[1].Description);
        Assert.AreEqual(-0.25f, view.Odds.Terms[1].Change, 1e-6f);
        Assert.AreEqual(_escape.VanillaChance(lord.Snapshot), view.Odds.Chance, 1e-6f);
    }

    [TestMethod]
    public void Build_PerksCancellingTheChance_ShowAZeroChance_NotAVeto()
    {
        var view = Build(Reading(0, Lord("Boromir", factor: 0f)));

        Assert.AreEqual(0f, view.Odds!.Chance);
        Assert.AreEqual(0, view.Vetoed.Count);
    }

    [TestMethod]
    public void Build_AVetoedLord_IsListedAndLeftOutOfTheOdds()
    {
        var vetoed = Lord("Denethor");
        vetoed.Snapshot.CanBeReleased = false;

        var view = Build(Reading(0, vetoed, Lord("Boromir")));

        CollectionAssert.AreEqual(new[] { "Denethor" }, view.Vetoed);
        Assert.IsNotNull(view.Odds);
        Assert.AreEqual(_escape.VanillaChance(Lord("x").Snapshot), view.Odds!.Chance, 1e-6f);
    }

    [TestMethod]
    public void Build_OnlyVetoedLords_HaveNoOdds()
    {
        var vetoed = Lord("Denethor");
        vetoed.Snapshot.CanBeReleased = false;

        var view = Build(Reading(0, vetoed));

        Assert.IsNull(view.Odds);
        CollectionAssert.AreEqual(new[] { "Denethor" }, view.Vetoed);
    }

    [TestMethod]
    public void Build_ABoostedLord_ShowsTheCombinedChance()
    {
        var lord = Lord("Boromir", "gondor");
        var p = _escape.VanillaChance(lord.Snapshot);
        var extra = _escape.ExtraChance(lord.Snapshot, 3f);

        var view = Build(Reading(0, lord), k => k == "gondor" ? 3f : 1f);

        Assert.AreEqual(1, view.Boosts.Count);
        Assert.AreEqual("Boromir", view.Boosts[0].Name);
        Assert.AreEqual(1f - (1f - p) * (1f - extra), view.Boosts[0].CombinedChance, 1e-6f);
        Assert.IsTrue(view.Boosts[0].CombinedChance > p);
    }

    [TestMethod]
    public void Build_OnlyTheBoostedKingdomsLords_GetABoostLine()
    {
        var view = Build(Reading(0, Lord("Boromir", "gondor"), Lord("Eomer", "rohan")), k => k == "gondor" ? 2f : 1f);

        CollectionAssert.AreEqual(new[] { "Boromir" }, view.Boosts.Select(b => b.Name).ToArray());
    }

    [TestMethod]
    public void Build_ABoostedLordHeldInASiege_GetsNoBoostLine()
    {
        var lord = Lord("Boromir", "gondor");
        lord.Snapshot.CaptorInMapEventOrSiege = true;

        var view = Build(Reading(0, lord), _ => 3f);

        Assert.AreEqual(0, view.Boosts.Count);
        Assert.IsNotNull(view.Odds, "vanilla's odds still apply to him");
    }

    [TestMethod]
    public void Build_ABoostedLordOfThePlayersClan_GetsNoBoostLine()
    {
        var lord = Lord("Boromir", "gondor");
        lord.Snapshot.IsPlayerClan = true;

        Assert.AreEqual(0, Build(Reading(0, lord), _ => 3f).Boosts.Count);
    }

    [TestMethod]
    public void Build_AVetoedBoostedLord_GetsTheVetoLineOnly()
    {
        var lord = Lord("Denethor", "gondor");
        lord.Snapshot.CanBeReleased = false;

        var view = Build(Reading(0, lord), _ => 3f);

        Assert.AreEqual(0, view.Boosts.Count);
        CollectionAssert.AreEqual(new[] { "Denethor" }, view.Vetoed);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(-2f)]
    [DataRow(0f)]
    [DataRow(1f)]
    [DataRow(0.5f)]
    public void Build_AMultiplierThatIsNotAFiniteBoost_FailsClosed_WithNoBoostLine(float multiplier)
    {
        var view = Build(Reading(0, Lord("Boromir", "gondor")), _ => multiplier);

        Assert.AreEqual(0, view.Boosts.Count);
        Assert.IsNotNull(view.Odds);
    }

    [TestMethod]
    public void Build_ALordWithNoKingdom_IsNotLookedUp()
    {
        var asked = new List<string>();

        var view = Build(Reading(0, Lord("Wanderer", kingdom: "")), k => { asked.Add(k); return 3f; });

        Assert.AreEqual(0, asked.Count);
        Assert.AreEqual(0, view.Boosts.Count);
    }

    [TestMethod]
    public void Build_NoBoostLookup_ShowsNoBoost()
    {
        var view = _sut.Build(Reading(0, Lord("Boromir", "gondor")), null)!;

        Assert.AreEqual(0, view.Boosts.Count);
    }

    [TestMethod]
    public void Build_ABoostThatZeroesVanillasChance_HasNoBoostLine()
    {
        var view = Build(Reading(0, Lord("Boromir", "gondor", factor: 0f)), _ => 3f);

        Assert.AreEqual(0, view.Boosts.Count);
    }

    [TestMethod]
    public void Build_ANullEntryOrSnapshot_IsSkipped()
    {
        var reading = Reading(0, Lord("Boromir"));
        reading.Lords.Add(null!);
        reading.Lords.Add(new DungeonLord { Name = "Ghost", Snapshot = null! });

        var view = Build(reading);

        Assert.IsNotNull(view.Odds);
    }
}
