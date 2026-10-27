using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.InnRounds.Domain;
using TAOM.Features.NotableServices.Domain;

namespace TAOM.Tests.Features.InnRounds;

[TestClass]
public sealed class InnRoundsServiceTests
{
    // ---- price per head ----

    [TestMethod]
    public void PricePerHead_IsTheTownMarketPrice_OfTheCulturesDrink()
    {
        var f = new InnRoundsFixture();
        f.Config.CultureDrinks["gondor"] = "wine";

        var quote = f.Service.QuoteParty(f.Ledger)!;

        Assert.AreEqual(20, quote.PricePerHead);
        f.Adapter.Received().DrinkPrice(InnRoundsFixture.TownId, "wine");
    }

    [TestMethod]
    public void PricePerHead_IsCappedAtMaxPricePerHead()
    {
        var f = new InnRoundsFixture();
        f.Config.CultureDrinks["gondor"] = "wine";
        f.Adapter.DrinkPrice(InnRoundsFixture.TownId, "wine").Returns(90);

        Assert.AreEqual(35, f.Service.QuoteParty(f.Ledger)!.PricePerHead);
    }

    [TestMethod]
    public void PricePerHead_ARaisedCap_LetsTheMarketPriceThrough()
    {
        var f = new InnRoundsFixture();
        f.Config.CultureDrinks["gondor"] = "wine";
        f.Config.MaxPricePerHead = 120f;
        f.Adapter.DrinkPrice(InnRoundsFixture.TownId, "wine").Returns(90);

        Assert.AreEqual(90, f.Service.QuoteParty(f.Ledger)!.PricePerHead);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow(0)]
    [DataRow(-4)]
    public void PricePerHead_AMissingItemMarketOrTown_UsesTheFallback(int? marketPrice)
    {
        var f = new InnRoundsFixture();
        f.Config.CultureDrinks["gondor"] = "wine";
        f.Adapter.DrinkPrice(InnRoundsFixture.TownId, "wine").Returns(marketPrice);

        Assert.AreEqual(27, f.Service.QuoteParty(f.Ledger)!.PricePerHead);
    }

    [TestMethod]
    public void PricePerHead_TheFallbackFollowsTheConfig()
    {
        var f = new InnRoundsFixture();
        f.Config.CultureDrinks["gondor"] = "wine";
        f.Config.FallbackPricePerHead = 31.6f;
        f.Adapter.DrinkPrice(InnRoundsFixture.TownId, "wine").Returns((int?)null);

        Assert.AreEqual(32, f.Service.QuoteParty(f.Ledger)!.PricePerHead);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("someplace_unlisted")]
    public void TheDrink_OfAnUnlistedCulture_IsBeer(string cultureId)
    {
        var f = new InnRoundsFixture();
        f.Keeper.CultureId = cultureId;

        var quote = f.Service.QuoteParty(f.Ledger)!;

        Assert.AreEqual(8, quote.PricePerHead);
        f.Adapter.Received().DrinkPrice(InnRoundsFixture.TownId, "beer");
        f.Adapter.DidNotReceive().DrinkPrice(InnRoundsFixture.TownId, "wine");
    }

    [TestMethod]
    public void TheDrink_OfAListedCulture_IsItsRow_WhateverTheCase()
    {
        var f = new InnRoundsFixture();
        f.Config.CultureDrinks["gondor"] = "wine";
        f.Keeper.CultureId = "Gondor";

        f.Service.QuoteParty(f.Ledger);

        f.Adapter.Received().DrinkPrice(InnRoundsFixture.TownId, "wine");
    }

    // ---- the two quotes ----

    [TestMethod]
    public void PartyCost_IsTheWholePartyTimesThePrice()
    {
        var f = new InnRoundsFixture();
        f.Config.CultureDrinks["gondor"] = "wine";

        var quote = f.Service.QuoteParty(f.Ledger)!;

        Assert.AreEqual(12, quote.Heads);
        Assert.AreEqual(240, quote.Price);
        Assert.IsTrue(quote.CanAfford);
        Assert.IsFalse(quote.CapReached);
    }

    [TestMethod]
    public void HouseCost_IsTheHeadcountOfTheTavernTimesThePrice()
    {
        var f = new InnRoundsFixture();
        f.Config.CultureDrinks["gondor"] = "wine";

        var quote = f.Service.QuoteHouse(f.Ledger)!;

        Assert.AreEqual(5, quote.Heads);
        Assert.AreEqual(100, quote.Price);
    }

    [TestMethod]
    public void HouseLine_IsHidden_WhenNobodyIsInTheTavern()
    {
        var f = new InnRoundsFixture();
        f.Adapter.TavernHeadcount(InnRoundsFixture.TownId).Returns(0);

        Assert.IsNull(f.Service.QuoteHouse(f.Ledger));
        Assert.IsNotNull(f.Service.QuoteParty(f.Ledger));
        Assert.IsFalse(f.Service.StandHouse(f.Ledger));
        f.Adapter.DidNotReceiveWithAnyArgs().PayTown(default!, default);
    }

    [TestMethod]
    public void PartyLine_IsHidden_WhenThePartyCountsNobody()
    {
        var f = new InnRoundsFixture();
        f.Adapter.PartyHeadcount().Returns(0);

        Assert.IsNull(f.Service.QuoteParty(f.Ledger));
    }

    [TestMethod]
    public void CanAfford_IsFalse_WhenThePurseIsShortOfThePrice()
    {
        var f = new InnRoundsFixture();
        f.Config.CultureDrinks["gondor"] = "wine";
        f.Adapter.PlayerGold.Returns(239);

        Assert.IsFalse(f.Service.QuoteParty(f.Ledger)!.CanAfford);
        f.Adapter.PlayerGold.Returns(240);
        Assert.IsTrue(f.Service.QuoteParty(f.Ledger)!.CanAfford);
    }

    // ---- the daily cap ----

    private static void StandThree(InnRoundsFixture f)
    {
        Assert.IsTrue(f.Service.StandParty(f.Ledger));
        Assert.IsTrue(f.Service.StandHouse(f.Ledger));
        Assert.IsTrue(f.Service.StandParty(f.Ledger));
    }

    [TestMethod]
    public void TheCap_IsSharedByBothKinds_AndGreysBothLines()
    {
        var f = new InnRoundsFixture();
        StandThree(f);

        Assert.IsTrue(f.Service.QuoteParty(f.Ledger)!.CapReached);
        Assert.IsTrue(f.Service.QuoteHouse(f.Ledger)!.CapReached);
        Assert.IsFalse(f.Service.StandParty(f.Ledger));
        Assert.IsFalse(f.Service.StandHouse(f.Ledger));
    }

    [TestMethod]
    public void TheCap_FollowsTheConfig()
    {
        var f = new InnRoundsFixture();
        f.Config.MaxRoundsPerDay = 1.4f;

        Assert.IsTrue(f.Service.StandHouse(f.Ledger));

        Assert.IsTrue(f.Service.QuoteParty(f.Ledger)!.CapReached);
    }

    [TestMethod]
    public void AnOverTheCapAsk_ChargesNothingAndChangesNothing()
    {
        var f = new InnRoundsFixture();
        StandThree(f);
        f.Adapter.ClearReceivedCalls();
        f.Skills.ClearReceivedCalls();

        Assert.IsFalse(f.Service.StandParty(f.Ledger));

        f.Adapter.DidNotReceiveWithAnyArgs().PayTown(default!, default);
        f.Adapter.DidNotReceiveWithAnyArgs().AddMorale(default);
        f.Skills.DidNotReceiveWithAnyArgs().AddSkillXpLanded(default!, default!, default);
        Assert.AreEqual(3, f.Ledger.Count);
    }

    [TestMethod]
    public void TheCap_ResetsOnANewDay()
    {
        var f = new InnRoundsFixture();
        StandThree(f);

        f.Adapter.Today.Returns(11);

        Assert.IsFalse(f.Service.QuoteParty(f.Ledger)!.CapReached);
        Assert.IsTrue(f.Service.StandHouse(f.Ledger));
        Assert.AreEqual(1, f.Ledger.Count);
        Assert.AreEqual(11, f.Ledger.Day);
    }

    [TestMethod]
    public void AReveal_DoesNotCountAgainstTheCap()
    {
        var f = new InnRoundsFixture();
        f.HoldsAreNear();

        Assert.AreEqual(RevealOutcome.Revealed, f.Service.Reveal());

        Assert.AreEqual(0, f.Ledger.Count);
    }

    [TestMethod]
    public void AFailedPayment_DoesNotCountAgainstTheCap()
    {
        var f = new InnRoundsFixture();
        f.Adapter.PayTown(InnRoundsFixture.TownId, Arg.Any<int>()).Returns(false);

        Assert.IsFalse(f.Service.StandParty(f.Ledger));

        Assert.AreEqual(0, f.Ledger.Count);
        Assert.AreEqual(RoundLedger.NoDay, f.Ledger.Day);
    }

    // ---- effects ----

    [TestMethod]
    public void APartyRound_PaysTheTown_RaisesMorale_GivesCharmXp_AndShowsTheLandedValues()
    {
        var f = new InnRoundsFixture();
        f.Config.CultureDrinks["gondor"] = "wine";

        Assert.IsTrue(f.Service.StandParty(f.Ledger));

        // The draw is 0.5: morale 20..40 gives 30, Charm 20..60 gives 40.
        Received.InOrder(() =>
        {
            f.Adapter.PayTown(InnRoundsFixture.TownId, 240);
            f.Adapter.AddMorale(30f);
        });
        f.Skills.Received(1).AddSkillXpLanded(InnRoundsFixture.PlayerId, "Charm", 40f);
        f.Adapter.Received(1).ShowPartyRoundNotice("gondor", 30);
        f.Adapter.Received(1).ShowCharmXpNotice(40);
        f.Renown.DidNotReceiveWithAnyArgs().AddClanRenown(default!, default);
        Assert.AreEqual(1, f.Ledger.Count);
        Assert.AreEqual(10, f.Ledger.Day);
    }

    [TestMethod]
    public void AHouseRound_PaysTheTown_GivesRenown_AndCharmXp_ButNoMorale()
    {
        var f = new InnRoundsFixture();
        f.Config.CultureDrinks["gondor"] = "wine";

        Assert.IsTrue(f.Service.StandHouse(f.Ledger));

        f.Adapter.Received(1).PayTown(InnRoundsFixture.TownId, 100);
        f.Renown.Received(1).AddClanRenown(InnRoundsFixture.PlayerId, 2);
        f.Skills.Received(1).AddSkillXpLanded(InnRoundsFixture.PlayerId, "Charm", 40f);
        f.Adapter.Received(1).ShowHouseRoundNotice("gondor");
        f.Adapter.Received(1).ShowCharmXpNotice(40);
        f.Adapter.DidNotReceiveWithAnyArgs().AddMorale(default);
        Assert.AreEqual(1, f.Ledger.Count);
    }

    [TestMethod]
    public void AFailedPayment_ChangesNothing()
    {
        var f = new InnRoundsFixture();
        f.Adapter.PayTown(InnRoundsFixture.TownId, Arg.Any<int>()).Returns(false);

        Assert.IsFalse(f.Service.StandParty(f.Ledger));
        Assert.IsFalse(f.Service.StandHouse(f.Ledger));

        f.Adapter.DidNotReceiveWithAnyArgs().AddMorale(default);
        f.Skills.DidNotReceiveWithAnyArgs().AddSkillXpLanded(default!, default!, default);
        f.Renown.DidNotReceiveWithAnyArgs().AddClanRenown(default!, default);
        f.Adapter.DidNotReceiveWithAnyArgs().ShowPartyRoundNotice(default!, default);
        f.Adapter.DidNotReceiveWithAnyArgs().ShowCharmXpNotice(default);
        f.Adapter.DidNotReceiveWithAnyArgs().ShowHouseRoundNotice(default!);
        Assert.AreEqual(0, f.Ledger.Count);
    }

    [TestMethod]
    public void AShortPurse_IsRefused_BeforeAnyPayment()
    {
        var f = new InnRoundsFixture();
        f.Adapter.PlayerGold.Returns(10);

        Assert.IsFalse(f.Service.StandParty(f.Ledger));

        f.Adapter.DidNotReceiveWithAnyArgs().PayTown(default!, default);
    }

    [TestMethod]
    public void TheMoraleNotice_ShowsWhatLanded_NotWhatWasAsked()
    {
        var f = new InnRoundsFixture();
        f.Adapter.AddMorale(Arg.Any<float>()).Returns(12.4f);

        f.Service.StandParty(f.Ledger);

        f.Adapter.Received(1).AddMorale(30f);
        f.Adapter.Received(1).ShowPartyRoundNotice("gondor", 12);
    }

    [TestMethod]
    public void ThePartyCharmNotice_ShowsTheExperienceThatLanded_NotTheAsk()
    {
        var f = new InnRoundsFixture();
        f.Skills.AddSkillXpLanded(InnRoundsFixture.PlayerId, "Charm", Arg.Any<float>()).Returns(87.4f);

        f.Service.StandParty(f.Ledger);

        f.Skills.Received(1).AddSkillXpLanded(InnRoundsFixture.PlayerId, "Charm", 40f);
        f.Adapter.Received(1).ShowCharmXpNotice(87);
        f.Adapter.DidNotReceive().ShowCharmXpNotice(40);
    }

    [TestMethod]
    public void TheHouseCharmNotice_ShowsTheExperienceThatLanded_NotTheAsk()
    {
        var f = new InnRoundsFixture();
        f.Skills.AddSkillXpLanded(InnRoundsFixture.PlayerId, "Charm", Arg.Any<float>()).Returns(12.6f);

        f.Service.StandHouse(f.Ledger);

        f.Adapter.Received(1).ShowCharmXpNotice(13);
        f.Adapter.DidNotReceive().ShowCharmXpNotice(40);
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(0.4f)]
    public void TheCharmNotice_IsSkipped_WhenNothingLanded(float landed)
    {
        var f = new InnRoundsFixture();
        f.Skills.AddSkillXpLanded(InnRoundsFixture.PlayerId, "Charm", Arg.Any<float>()).Returns(landed);

        f.Service.StandParty(f.Ledger);
        f.Service.StandHouse(f.Ledger);

        f.Adapter.DidNotReceiveWithAnyArgs().ShowCharmXpNotice(default);
        f.Adapter.Received(1).ShowPartyRoundNotice("gondor", 30);
        f.Adapter.Received(1).ShowHouseRoundNotice("gondor");
    }

    [TestMethod]
    public void TheMoraleScale_Applies()
    {
        var f = new InnRoundsFixture();
        f.Settings.MoraleScale.Returns(2f);

        f.Service.StandParty(f.Ledger);

        f.Adapter.Received(1).AddMorale(60f);
    }

    [TestMethod]
    public void TheRenownScale_Applies_AndRoundsToAWholeNumber()
    {
        var f = new InnRoundsFixture();
        f.Settings.RenownScale.Returns(1.75f);

        f.Service.StandHouse(f.Ledger);

        f.Renown.Received(1).AddClanRenown(InnRoundsFixture.PlayerId, 4);
    }

    [TestMethod]
    public void ARenownScaleOfZero_GrantsNoRenown_ButStillTakesThePayment()
    {
        var f = new InnRoundsFixture();
        f.Settings.RenownScale.Returns(0f);

        Assert.IsTrue(f.Service.StandHouse(f.Ledger));

        f.Renown.DidNotReceiveWithAnyArgs().AddClanRenown(default!, default);
        f.Adapter.Received(1).PayTown(InnRoundsFixture.TownId, Arg.Any<int>());
    }

    [TestMethod]
    public void AMoraleScaleOfZero_StillShowsAMoraleOfZero()
    {
        var f = new InnRoundsFixture();
        f.Settings.MoraleScale.Returns(0f);
        f.Adapter.AddMorale(0f).Returns(0f);

        f.Service.StandParty(f.Ledger);

        f.Adapter.Received(1).ShowPartyRoundNotice("gondor", 0);
    }

    [DataTestMethod]
    [DataRow(0.0, 20f, 20f)]
    [DataRow(1.0, 40f, 60f)]
    [DataRow(0.25, 25f, 30f)]
    public void TheDraws_SpanTheConfiguredRanges(double draw, float morale, float xp)
    {
        var f = new InnRoundsFixture();
        f.Random.NextDouble().Returns(draw);

        f.Service.StandParty(f.Ledger);

        f.Adapter.Received(1).AddMorale(morale);
        f.Skills.Received(1).AddSkillXpLanded(InnRoundsFixture.PlayerId, "Charm", xp);
    }

    [TestMethod]
    public void TheRanges_FollowTheConfig()
    {
        var f = new InnRoundsFixture();
        f.Config.MoraleMin = 10f;
        f.Config.MoraleMax = 10f;
        f.Config.CharmXpMin = 5f;
        f.Config.CharmXpMax = 5f;

        f.Service.StandParty(f.Ledger);

        f.Adapter.Received(1).AddMorale(10f);
        f.Skills.Received(1).AddSkillXpLanded(InnRoundsFixture.PlayerId, "Charm", 5f);
    }

    [TestMethod]
    public void ACharmXpOfZero_GrantsNoExperience()
    {
        var f = new InnRoundsFixture();
        f.Config.CharmXpMin = 0f;
        f.Config.CharmXpMax = 0f;

        f.Service.StandParty(f.Ledger);

        f.Skills.DidNotReceiveWithAnyArgs().AddSkillXpLanded(default!, default!, default);
        f.Adapter.Received(1).ShowPartyRoundNotice("gondor", 30);
        f.Adapter.DidNotReceiveWithAnyArgs().ShowCharmXpNotice(default);
    }

    [TestMethod]
    public void AnEngineFault_AfterThePayment_IsLogged_AndTheRoundStillCounts()
    {
        var f = new InnRoundsFixture();
        f.Skills.AddSkillXpLanded(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>()).Returns(_ => throw new InvalidOperationException("boom"));

        Assert.IsTrue(f.Service.StandParty(f.Ledger));

        f.Logger.Received().LogError(Arg.Is<string>(s => s.Contains("boom")));
        Assert.AreEqual(1, f.Ledger.Count);
    }

    [TestMethod]
    public void AnEngineFault_BeforeThePayment_IsLogged_AndAnswersFalse()
    {
        var f = new InnRoundsFixture();
        f.Adapter.PayTown(InnRoundsFixture.TownId, Arg.Any<int>()).Returns(_ => throw new InvalidOperationException("boom"));

        Assert.IsFalse(f.Service.StandHouse(f.Ledger));

        f.Logger.Received().LogError(Arg.Is<string>(s => s.Contains("boom")));
        Assert.AreEqual(0, f.Ledger.Count);
    }

    // ---- the reveal ----

    [TestMethod]
    public void TheReveal_ChargesTheRevealPrice_ToTheTown_ThenMarksTheHolds()
    {
        var f = new InnRoundsFixture();
        f.HoldsAreNear();

        Assert.AreEqual(RevealOutcome.Revealed, f.Service.Reveal());

        Received.InOrder(() =>
        {
            f.Adapter.PayTown(InnRoundsFixture.TownId, 100);
            f.Map.Track(Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "town_a", "town_b" })));
        });
        f.Map.Received(1).ShowHoldsMarkedNotice(2);
    }

    [TestMethod]
    public void TheRevealPrice_FollowsTheNotableServicesConfig()
    {
        var f = new InnRoundsFixture();
        f.RevealConfig.RevealPrice = 99.6f;

        Assert.AreEqual(100, f.Service.RevealPrice);
        f.RevealConfig.RevealPrice = 60f;
        f.HoldsAreNear();
        f.Service.Reveal();
        f.Adapter.Received(1).PayTown(InnRoundsFixture.TownId, 60);
    }

    [TestMethod]
    public void TheReveal_AtWar_IsRefused_WithNoCharge()
    {
        var f = new InnRoundsFixture();
        f.HoldsAreNear();
        f.Keeper.AtWarWithPlayer = true;

        Assert.AreEqual(RevealOutcome.AtWar, f.Service.Reveal());

        f.Adapter.DidNotReceiveWithAnyArgs().PayTown(default!, default);
        f.Map.DidNotReceiveWithAnyArgs().Track(default!);
    }

    [TestMethod]
    public void TheReveal_BelowTheMinimumRelation_IsCold_WithNoCharge()
    {
        var f = new InnRoundsFixture();
        f.HoldsAreNear();
        f.RevealConfig.RevealMinRelation = 5f;
        f.Keeper.Relation = 4f;

        Assert.AreEqual(RevealOutcome.Cold, f.Service.Reveal());

        f.Adapter.DidNotReceiveWithAnyArgs().PayTown(default!, default);
    }

    [TestMethod]
    public void TheReveal_AtTheMinimumRelation_Works()
    {
        var f = new InnRoundsFixture();
        f.HoldsAreNear();
        f.RevealConfig.RevealMinRelation = 5f;
        f.Keeper.Relation = 5f;

        Assert.AreEqual(RevealOutcome.Revealed, f.Service.Reveal());
    }

    [TestMethod]
    public void TheReveal_WithNothingNew_ChargesNothing()
    {
        var f = new InnRoundsFixture();
        f.HoldsAround(new RevealCandidate("town_a", 40f, true));

        Assert.AreEqual(RevealOutcome.NothingNew, f.Service.Reveal());

        f.Adapter.DidNotReceiveWithAnyArgs().PayTown(default!, default);
    }

    [TestMethod]
    public void TheReveal_WithAShortPurse_IsUnavailable_WithNoPayment()
    {
        var f = new InnRoundsFixture();
        f.HoldsAreNear();
        f.Adapter.PlayerGold.Returns(99);

        Assert.AreEqual(RevealOutcome.Unavailable, f.Service.Reveal());
        Assert.IsFalse(f.Service.CanAffordReveal());

        f.Adapter.DidNotReceiveWithAnyArgs().PayTown(default!, default);
        f.Map.DidNotReceiveWithAnyArgs().Track(default!);
    }

    [TestMethod]
    public void TheReveal_AFailedPayment_MarksNothing()
    {
        var f = new InnRoundsFixture();
        f.HoldsAreNear();
        f.Adapter.PayTown(InnRoundsFixture.TownId, Arg.Any<int>()).Returns(false);

        Assert.AreEqual(RevealOutcome.Unavailable, f.Service.Reveal());

        f.Map.DidNotReceiveWithAnyArgs().Track(default!);
    }

    [TestMethod]
    public void CanAffordReveal_IsThePurseAgainstThePrice()
    {
        var f = new InnRoundsFixture();

        Assert.IsTrue(f.Service.CanAffordReveal());
        f.Adapter.PlayerGold.Returns(99);
        Assert.IsFalse(f.Service.CanAffordReveal());
        f.Adapter.PlayerGold.Returns(100);
        Assert.IsTrue(f.Service.CanAffordReveal());
    }

    // ---- the gates ----

    [TestMethod]
    public void EachGate_ClosesEveryLine_AndChangesNothing()
    {
        foreach (var (name, close) in InnRoundsFixture.Gates())
        {
            var f = new InnRoundsFixture();
            f.HoldsAreNear();
            close(f);

            Assert.IsNull(f.Service.QuoteParty(f.Ledger), name + ": party quote");
            Assert.IsNull(f.Service.QuoteHouse(f.Ledger), name + ": house quote");
            Assert.IsFalse(f.Service.OffersReveal(), name + ": reveal line");
            Assert.IsFalse(f.Service.StandParty(f.Ledger), name + ": party round");
            Assert.IsFalse(f.Service.StandHouse(f.Ledger), name + ": house round");
            Assert.AreEqual(RevealOutcome.Unavailable, f.Service.Reveal(), name + ": reveal");

            f.Adapter.DidNotReceiveWithAnyArgs().PayTown(default!, default);
            f.Adapter.DidNotReceiveWithAnyArgs().AddMorale(default);
            f.Skills.DidNotReceiveWithAnyArgs().AddSkillXpLanded(default!, default!, default);
            f.Renown.DidNotReceiveWithAnyArgs().AddClanRenown(default!, default);
            f.Map.DidNotReceiveWithAnyArgs().Track(default!);
            Assert.AreEqual(0, f.Ledger.Count, name);
        }
    }

    [TestMethod]
    public void WithEveryGateOpen_AllThreeLinesShow()
    {
        var f = new InnRoundsFixture();

        Assert.IsNotNull(f.Service.QuoteParty(f.Ledger));
        Assert.IsNotNull(f.Service.QuoteHouse(f.Ledger));
        Assert.IsTrue(f.Service.OffersReveal());
    }

    [TestMethod]
    public void TheConditions_NeverWrite()
    {
        var f = new InnRoundsFixture();

        f.Service.QuoteParty(f.Ledger);
        f.Service.QuoteHouse(f.Ledger);
        f.Service.OffersReveal();
        f.Service.CanAffordReveal();
        _ = f.Service.RevealPrice;

        f.Adapter.DidNotReceiveWithAnyArgs().PayTown(default!, default);
        f.Adapter.DidNotReceiveWithAnyArgs().AddMorale(default);
        f.Map.DidNotReceiveWithAnyArgs().Track(default!);
        Assert.AreEqual(0, f.Ledger.Count);
    }
}
