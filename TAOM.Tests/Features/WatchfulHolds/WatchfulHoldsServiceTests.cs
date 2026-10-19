using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.WatchfulHolds;
using TAOM.Features.WatchfulHolds.Models;

namespace TAOM.Tests.Features.WatchfulHolds;

[TestClass]
public class WatchfulHoldsServiceTests
{
    // ---- the positive controls ----------------------------------------------------------------

    [TestMethod]
    public void OnHourlyTickParty_PlayerNearAnEnemyFort_IsHarassed()
    {
        var rig = new WatchfulHoldsRig();

        rig.TickPlayer();

        CollectionAssert.AreEqual(new object[] { WatchfulHoldsRig.PlayerHandle }, rig.Adapter.Disorganized);
        Assert.AreEqual(1, rig.Notices.Harried.Count);
        Assert.AreEqual(WatchfulHoldsRig.Fort, rig.Notices.Harried[0].Fort);
    }

    [TestMethod]
    public void OnHourlyTickParty_AiLordNearAnEnemyFort_IsDisorganizedOnly()
    {
        var rig = new WatchfulHoldsRig();

        rig.TickAi();

        CollectionAssert.AreEqual(new object[] { WatchfulHoldsRig.AiHandle }, rig.Adapter.Disorganized);
        Assert.AreEqual(0, rig.Adapter.LossCalls.Count, "AI lords take no losses");
        Assert.AreEqual(0, rig.Adapter.FoodCalls.Count, "AI lords lose no food");
        Assert.AreEqual(0, rig.Adapter.MoraleCalls.Count, "AI lords lose no morale");
        Assert.AreEqual(0, rig.Adapter.CohesionCalls.Count);
        Assert.AreEqual(0, rig.Random.NextCalls.Count, "no loss rolls for an AI lord");
        Assert.AreEqual(0, rig.Notices.Harried.Count, "the player is told nothing about an AI lord");
        Assert.AreEqual(0, rig.Notices.Horns, "no horn for an AI lord");
    }

    // ---- the master toggle and the AI toggle --------------------------------------------------

    [TestMethod]
    public void OnHourlyTickParty_MasterToggleOff_ReadsNothing()
    {
        var rig = new WatchfulHoldsRig();
        rig.Settings.Enabled = false;

        rig.TickPlayer();
        rig.TickAi();

        Assert.AreEqual(0, rig.Adapter.TryReadPartyCalls, "the toggle is the first and cheapest check");
        Assert.IsFalse(rig.AnythingHappened);
    }

    [TestMethod]
    public void OnHourlyTickParty_AiToggleOff_SkipsAiLordsButStillHarassesThePlayer()
    {
        var rig = new WatchfulHoldsRig();
        rig.Settings.HarassAiLords = false;

        rig.TickAi();
        Assert.IsFalse(rig.AnythingHappened, "AI harassment is off");

        rig.TickPlayer();
        CollectionAssert.AreEqual(new object[] { WatchfulHoldsRig.PlayerHandle }, rig.Adapter.Disorganized);
    }

    [TestMethod]
    public void OnHourlyTickParty_UnreadableParty_DoesNothing()
    {
        var rig = new WatchfulHoldsRig();

        rig.Sut.OnHourlyTickParty("ghost");

        Assert.IsFalse(rig.AnythingHappened);
        Assert.AreEqual(0, rig.Adapter.GetFortSitesCalls);
    }

    // ---- every exemption ----------------------------------------------------------------------

    public static IEnumerable<object[]> Exemptions() => new[]
    {
        new object[] { "NotMoving" },
        new object[] { "InSettlement" },
        new object[] { "InMapEvent" },
        new object[] { "InSiegeEvent" },
        new object[] { "ArmyFollower" },
        new object[] { "AlreadyDisorganized" },
        new object[] { "CannotGetDisorganized" },
        new object[] { "TooSmall" },
        new object[] { "NotALordParty" },
    };

    private static WatchfulPartySnapshot Exempt(WatchfulPartySnapshot p, string what) => what switch
    {
        "NotMoving" => p with { IsMoving = false },
        "InSettlement" => p with { InSettlement = true },
        "InMapEvent" => p with { InMapEvent = true },
        "InSiegeEvent" => p with { InSiegeEvent = true },
        "ArmyFollower" => p with { IsArmyFollower = true },
        "AlreadyDisorganized" => p with { IsDisorganized = true },
        "CannotGetDisorganized" => p with { CanGetDisorganized = false },
        "TooSmall" => p with { MenCount = 29 },
        "NotALordParty" => p with { IsLordParty = false, IsMainParty = false },
        _ => throw new ArgumentException(what),
    };

    [DataTestMethod]
    [DynamicData(nameof(Exemptions), DynamicDataSourceType.Method)]
    public void OnHourlyTickParty_ExemptPlayerParty_IsLeftAlone(string what)
    {
        var rig = new WatchfulHoldsRig();
        rig.Player = Exempt(rig.Player, what);

        rig.TickPlayer();

        Assert.IsFalse(rig.AnythingHappened, what);
        Assert.AreEqual(0, rig.Random.DoubleCalls, what + ": an exempt party never rolls");
    }

    [DataTestMethod]
    [DynamicData(nameof(Exemptions), DynamicDataSourceType.Method)]
    public void OnHourlyTickParty_ExemptAiParty_IsLeftAlone(string what)
    {
        var rig = new WatchfulHoldsRig();
        rig.Ai = Exempt(rig.Ai, what);

        rig.TickAi();

        Assert.IsFalse(rig.AnythingHappened, what);
        Assert.AreEqual(0, rig.Random.DoubleCalls, what);
    }

    [TestMethod]
    public void OnHourlyTickParty_ExemptParty_NeverTouchesTheFortList()
    {
        var rig = new WatchfulHoldsRig();
        rig.Player = rig.Player with { IsMoving = false };

        rig.TickPlayer();

        Assert.AreEqual(0, rig.Adapter.GetFortSitesCalls, "the cheap filters run before any fort work");
    }

    [TestMethod]
    public void OnHourlyTickParty_PartyOfExactlyTheMinimum_IsHarassed()
    {
        var rig = new WatchfulHoldsRig();
        rig.Player = rig.Player with { MenCount = 30 };

        rig.TickPlayer();

        Assert.AreEqual(1, rig.Notices.Harried.Count);
    }

    [TestMethod]
    public void OnHourlyTickParty_MinimumPartySizeComesFromTheConfig()
    {
        var rig = new WatchfulHoldsRig();
        rig.Config.MinPartySize = 120;

        rig.TickPlayer();

        Assert.IsFalse(rig.AnythingHappened, "100 men is under a 120 minimum");
    }

    [TestMethod]
    public void OnHourlyTickParty_AMainPartyThatIsNotMarkedAsALordParty_IsStillHarassed()
    {
        var rig = new WatchfulHoldsRig();
        rig.Player = rig.Player with { IsLordParty = false };

        rig.TickPlayer();

        Assert.AreEqual(1, rig.Notices.Harried.Count, "the rule is lord party OR the player");
    }

    [TestMethod]
    public void OnHourlyTickParty_EnlistedPlayer_IsLeftAlone()
    {
        var rig = new WatchfulHoldsRig();
        rig.Enlistment.IsEnlisted.Returns(true);

        rig.TickPlayer();

        Assert.IsFalse(rig.AnythingHappened);
    }

    [TestMethod]
    public void OnHourlyTickParty_EnlistedPlayer_TheCommandersPartyIsStillAnAiLord()
    {
        var rig = new WatchfulHoldsRig();
        rig.Enlistment.IsEnlisted.Returns(true);

        rig.TickAi();

        CollectionAssert.AreEqual(new object[] { WatchfulHoldsRig.AiHandle }, rig.Adapter.Disorganized);
    }

    [TestMethod]
    public void OnHourlyTickParty_NonFinitePosition_IsLeftAlone()
    {
        var rig = new WatchfulHoldsRig();
        rig.Player = rig.Player with { X = float.NaN };
        rig.Ai = rig.Ai with { Y = float.PositiveInfinity };

        rig.TickPlayer();
        rig.TickAi();

        Assert.IsFalse(rig.AnythingHappened);
        Assert.AreEqual(0, rig.Adapter.GetFortSitesCalls);
    }

    // ---- radius, nearest fort -----------------------------------------------------------------

    [TestMethod]
    public void OnHourlyTickParty_FortExactlyAtTheRadius_Harasses()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();
        rig.AddFort("edge", 5f, 0f);

        rig.TickPlayer();

        Assert.AreEqual("edge", rig.Notices.Harried.Single().Fort);
    }

    [TestMethod]
    public void OnHourlyTickParty_FortJustBeyondTheRadius_DoesNot()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();
        rig.AddFort("far", 5.01f, 0f);

        rig.TickPlayer();

        Assert.IsFalse(rig.AnythingHappened);
        Assert.AreEqual(0, rig.Adapter.FortReads.Count, "no live fort read for a fort out of range");
        Assert.AreEqual(0, rig.Random.DoubleCalls);
    }

    [TestMethod]
    public void OnHourlyTickParty_RadiusComesFromTheConfig()
    {
        var rig = new WatchfulHoldsRig();
        rig.Config.Radius = 2f;

        rig.TickPlayer();

        Assert.IsFalse(rig.AnythingHappened, "the fort is 3 units away and the radius is 2");
    }

    [TestMethod]
    public void OnHourlyTickParty_DistanceIsMeasuredInTwoDimensions()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();
        rig.AddFort("diagonal", 3.6f, 3.6f);

        rig.TickPlayer();

        Assert.IsFalse(rig.AnythingHappened, "3.6 and 3.6 are 5.09 apart");
    }

    [TestMethod]
    public void OnHourlyTickParty_TwoForts_TheNearestQualifyingFortWins()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();
        rig.AddFort("farther", 4f, 0f);
        rig.AddFort("nearer", 1f, 0f);

        rig.TickPlayer();

        Assert.AreEqual("nearer", rig.Notices.Harried.Single().Fort);
        CollectionAssert.AreEqual(new[] { "nearer" }, rig.Adapter.FortReads, "the second fort is never read once the first qualifies");
    }

    [TestMethod]
    public void OnHourlyTickParty_NearestFortDoesNotQualify_TheNextOneIsUsed()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();
        rig.AddFort("besieged", 1f, 0f, siege: true);
        rig.AddFort("small", 2f, 0f, garrison: 10);
        rig.AddFort("friend", 3f, 0f);
        rig.AddFort("last", 4f, 0f);
        rig.Adapter.AtWar = (_, fort) => fort != "friend";

        rig.TickPlayer();

        Assert.AreEqual("last", rig.Notices.Harried.Single().Fort);
        CollectionAssert.AreEqual(new[] { "besieged", "small", "friend", "last" }, rig.Adapter.FortReads);
    }

    [TestMethod]
    public void OnHourlyTickParty_UnreadableNearestFort_FallsThroughToTheNext()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();
        rig.Adapter.Forts.Add(new FortSite("gone", 1f, 0f));
        rig.AddFort("here", 2f, 0f);

        rig.TickPlayer();

        Assert.AreEqual("here", rig.Notices.Harried.Single().Fort);
    }

    [TestMethod]
    public void OnHourlyTickParty_NoFortsAtAll_DoesNothing()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();

        rig.TickPlayer();

        Assert.IsFalse(rig.AnythingHappened);
    }

    // ---- fort conditions ----------------------------------------------------------------------

    [TestMethod]
    public void OnHourlyTickParty_FortUnderSiege_DoesNothing()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();
        rig.AddFort("held", 1f, 0f, siege: true);

        rig.TickPlayer();

        Assert.IsFalse(rig.AnythingHappened);
        Assert.AreEqual(0, rig.Adapter.WarChecks.Count, "the siege test comes before the diplomacy query");
    }

    [DataTestMethod]
    [DataRow(29, false)]
    [DataRow(30, true)]
    [DataRow(31, true)]
    public void OnHourlyTickParty_GarrisonSize_MustReachTheMinimum(int garrison, bool harasses)
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();
        rig.AddFort("post", 1f, 0f, garrison: garrison);

        rig.TickPlayer();

        Assert.AreEqual(harasses, rig.Notices.Harried.Count == 1);
    }

    [TestMethod]
    public void OnHourlyTickParty_MinimumGarrisonComesFromTheConfig()
    {
        var rig = new WatchfulHoldsRig();
        rig.Config.MinGarrison = 60;

        rig.TickPlayer();

        Assert.IsFalse(rig.AnythingHappened, "50 men is under a 60 minimum");
    }

    [TestMethod]
    public void OnHourlyTickParty_AtPeace_DoesNothing()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.AtWar = (_, _) => false;

        rig.TickPlayer();
        rig.TickAi();

        Assert.IsFalse(rig.AnythingHappened);
        Assert.AreEqual(0, rig.Random.DoubleCalls, "no roll without a war");
    }

    [TestMethod]
    public void OnHourlyTickParty_WarCheckGetsThePartyHandleAndTheFortId()
    {
        var rig = new WatchfulHoldsRig();
        object? seenParty = null;
        rig.Adapter.AtWar = (party, fort) => { seenParty = party; return true; };

        rig.TickAi();

        Assert.AreEqual(WatchfulHoldsRig.AiHandle, seenParty);
        CollectionAssert.AreEqual(new[] { WatchfulHoldsRig.Fort }, rig.Adapter.WarChecks);
    }

    // ---- the province gate --------------------------------------------------------------------

    [TestMethod]
    public void OnHourlyTickParty_InsideTheFortsProvince_Harasses()
    {
        var rig = new WatchfulHoldsRig();
        rig.Provinces.Available = true;
        rig.Provinces.FiefId = WatchfulHoldsRig.Fort;

        rig.TickPlayer();

        Assert.AreEqual(1, rig.Notices.Harried.Count);
        Assert.AreEqual(1, rig.Provinces.Lookups);
    }

    [TestMethod]
    public void OnHourlyTickParty_InAnotherFortsProvince_DoesNothing()
    {
        var rig = new WatchfulHoldsRig();
        rig.Provinces.Available = true;
        rig.Provinces.FiefId = "across_the_river";

        rig.TickPlayer();

        Assert.IsFalse(rig.AnythingHappened, "a fort does not reach across a province line");
        Assert.AreEqual(0, rig.Random.DoubleCalls);
    }

    [TestMethod]
    public void OnHourlyTickParty_InNoProvince_DoesNothing()
    {
        var rig = new WatchfulHoldsRig();
        rig.Provinces.Available = true;
        rig.Provinces.FiefId = string.Empty;

        rig.TickPlayer();

        Assert.IsFalse(rig.AnythingHappened);
    }

    [TestMethod]
    public void OnHourlyTickParty_NoProvincesExist_FallsBackToDistanceOnly()
    {
        var rig = new WatchfulHoldsRig();
        rig.Provinces.Available = false;

        rig.TickPlayer();

        Assert.AreEqual(1, rig.Notices.Harried.Count);
    }

    [TestMethod]
    public void OnHourlyTickParty_ProvinceGate_PicksTheFortWhoseProvinceThePartyStandsIn()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();
        rig.AddFort("nearer", 1f, 0f);
        rig.AddFort("farther", 4f, 0f);
        rig.Provinces.Available = true;
        rig.Provinces.FiefId = "farther";

        rig.TickPlayer();

        Assert.AreEqual("farther", rig.Notices.Harried.Single().Fort);
        Assert.AreEqual(1, rig.Provinces.Lookups, "one province lookup per party, not one per fort");
        CollectionAssert.AreEqual(new[] { "farther" }, rig.Adapter.FortReads, "a fort of another province is never read");
        CollectionAssert.AreEqual(new[] { "farther" }, rig.Adapter.WarChecks);
    }

    [TestMethod]
    public void OnHourlyTickParty_TwoFortsAtTheSameDistance_TheLowerIdWins()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();
        rig.AddFort("zeta", 2f, 0f);
        rig.AddFort("alpha", -2f, 0f);

        rig.TickPlayer();

        Assert.AreEqual("alpha", rig.Notices.Harried.Single().Fort);
        CollectionAssert.AreEqual(new[] { "alpha" }, rig.Adapter.FortReads);
    }

    // ---- the fort list ------------------------------------------------------------------------

    [TestMethod]
    public void OnHourlyTickParty_ReadsTheFortListOncePerCampaign()
    {
        var rig = new WatchfulHoldsRig();

        rig.TickPlayer();
        rig.TickAi();

        Assert.AreEqual(1, rig.Adapter.GetFortSitesCalls);
    }

    [TestMethod]
    public void ResetForNewSession_ReadsTheFortListAgain()
    {
        var rig = new WatchfulHoldsRig();
        rig.TickPlayer();

        rig.Sut.ResetForNewSession();
        rig.TickPlayer();

        Assert.AreEqual(2, rig.Adapter.GetFortSitesCalls);
    }

    // ---- the roll -----------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(0.0, true)]
    [DataRow(0.2999, true)]
    [DataRow(0.3001, false)]
    [DataRow(0.99, false)]
    public void OnHourlyTickParty_PlayerChance_IsThirtyPercentByDefault(double roll, bool harassed)
    {
        var rig = new WatchfulHoldsRig();
        rig.Random.Roll = roll;

        rig.TickPlayer();

        Assert.AreEqual(harassed, rig.Notices.Harried.Count == 1);
        Assert.AreEqual(1, rig.Random.DoubleCalls, "one roll per party that passes the gates");
    }

    [DataTestMethod]
    [DataRow(0.0, true)]
    [DataRow(0.1499, true)]
    [DataRow(0.1501, false)]
    [DataRow(0.29, false)]
    public void OnHourlyTickParty_AiChance_IsFifteenPercentByDefault(double roll, bool disorganized)
    {
        var rig = new WatchfulHoldsRig();
        rig.Random.Roll = roll;

        rig.TickAi();

        Assert.AreEqual(disorganized, rig.Adapter.Disorganized.Count == 1);
    }

    [TestMethod]
    public void OnHourlyTickParty_ChancesComeFromTheConfig()
    {
        var rig = new WatchfulHoldsRig();
        rig.Config.PlayerChancePerHour = 0.9f;
        rig.Config.AiChancePerHour = 0.5f;
        rig.Random.Roll = 0.6;

        rig.TickPlayer();
        rig.TickAi();

        Assert.AreEqual(1, rig.Notices.Harried.Count, "0.6 is under 0.9");
        Assert.AreEqual(1, rig.Adapter.Disorganized.Count(h => (string)h == WatchfulHoldsRig.PlayerHandle), "only the player was disorganized");
        Assert.AreEqual(0, rig.Adapter.Disorganized.Count(h => (string)h == WatchfulHoldsRig.AiHandle), "0.6 is over 0.5");
    }

    [TestMethod]
    public void OnHourlyTickParty_CultureChanceMultiplier_RaisesTheOwnersChance()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();
        rig.AddFort("barad", 1f, 0f, culture: "mordor");
        rig.Random.Roll = 0.35; // 0.30 x 1.25 = 0.375

        rig.TickPlayer();

        Assert.AreEqual(1, rig.Notices.Harried.Count);
    }

    [TestMethod]
    public void OnHourlyTickParty_CultureWithoutARow_UsesTheDefaultRow()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();
        rig.AddFort("edoras", 1f, 0f, culture: "rohan");
        rig.Random.Roll = 0.35;

        rig.TickPlayer();

        Assert.IsFalse(rig.AnythingHappened, "0.35 is over the plain 0.30");
    }

    [TestMethod]
    public void OnHourlyTickParty_UnknownOrBlankOwnerCulture_UsesTheDefaultRow()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();
        rig.AddFort("odd", 1f, 0f, culture: "");
        rig.Random.Roll = 0.35;

        rig.TickPlayer();

        Assert.IsFalse(rig.AnythingHappened);
    }

    [TestMethod]
    public void OnHourlyTickParty_CultureMultiplier_AppliesToAiLordsToo()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Forts.Clear();
        rig.AddFort("barad", 1f, 0f, culture: "isengard");
        rig.Random.Roll = 0.18; // 0.15 x 1.25 = 0.1875

        rig.TickAi();

        Assert.AreEqual(1, rig.Adapter.Disorganized.Count);
    }

    [TestMethod]
    public void OnHourlyTickParty_ChanceTimesMultiplier_IsClampedToOne()
    {
        var rig = new WatchfulHoldsRig();
        rig.Config.PlayerChancePerHour = 0.9f;
        rig.Config.Cultures["gondor"] = new WatchfulCultureRow { ChanceMultiplier = 5f };
        rig.Random.Roll = 0.9999;

        rig.TickPlayer();

        Assert.AreEqual(1, rig.Notices.Harried.Count, "0.9 x 5 is clamped to 1, and a roll below 1 always succeeds");
    }

    [TestMethod]
    public void OnHourlyTickParty_ZeroChance_NeverTriggers()
    {
        var rig = new WatchfulHoldsRig();
        rig.Config.PlayerChancePerHour = 0f;
        rig.Random.Roll = 0.0;

        rig.TickPlayer();

        Assert.IsFalse(rig.AnythingHappened);
    }

    [TestMethod]
    public void OnHourlyTickParty_NoQualifyingFort_NeverRolls()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.AtWar = (_, _) => false;

        rig.TickPlayer();

        Assert.AreEqual(0, rig.Random.DoubleCalls);
    }

    // ---- the player's losses ------------------------------------------------------------------

    [TestMethod]
    public void OnHourlyTickParty_Player_LosesMenFoodAndMoraleAndIsDisorganized()
    {
        var rig = new WatchfulHoldsRig();
        rig.Player = rig.Player with { MenCount = 200 };

        rig.TickPlayer();

        // 200 men: 2% = 4 wounded, 1% = 2 killed, the random extras are 0 here.
        CollectionAssert.AreEqual(new (object, int, int)[] { (WatchfulHoldsRig.PlayerHandle, 2, 4) }, rig.Adapter.LossCalls);
        CollectionAssert.AreEqual(new (object, float)[] { (WatchfulHoldsRig.PlayerHandle, 0.05f) }, rig.Adapter.FoodCalls);
        CollectionAssert.AreEqual(new (object, float)[] { (WatchfulHoldsRig.PlayerHandle, 10f) }, rig.Adapter.MoraleCalls);
        CollectionAssert.AreEqual(new object[] { WatchfulHoldsRig.PlayerHandle }, rig.Adapter.Disorganized);
        Assert.AreEqual(0, rig.Adapter.CohesionCalls.Count, "no army, no cohesion");
        Assert.AreEqual((WatchfulHoldsRig.Fort, 2, 4, 7), rig.Notices.Harried.Single());
    }

    [TestMethod]
    public void OnHourlyTickParty_Losses_AddARandomExtraWithinTheConfiguredMaximum()
    {
        var rig = new WatchfulHoldsRig();
        rig.Player = rig.Player with { MenCount = 200 };
        rig.Random.Extra = 100;

        rig.TickPlayer();

        // kill 2 + min(1, 100), wound 4 + min(5, 100)
        CollectionAssert.AreEqual(new (object, int, int)[] { (WatchfulHoldsRig.PlayerHandle, 3, 9) }, rig.Adapter.LossCalls);
        CollectionAssert.Contains(rig.Random.NextCalls, (0, 2), "kill extra is 0..1");
        CollectionAssert.Contains(rig.Random.NextCalls, (0, 6), "wound extra is 0..5");
    }

    [TestMethod]
    public void OnHourlyTickParty_Losses_RoundDownTheFractionalPart()
    {
        var rig = new WatchfulHoldsRig();
        rig.Player = rig.Player with { MenCount = 99 };

        rig.TickPlayer();

        // 99 x 1% = 0.99 -> 0 killed; 99 x 2% = 1.98 -> 1 wounded
        CollectionAssert.AreEqual(new (object, int, int)[] { (WatchfulHoldsRig.PlayerHandle, 0, 1) }, rig.Adapter.LossCalls);
    }

    [TestMethod]
    public void OnHourlyTickParty_Losses_AreNotShortOfOneBecauseOfFloatRounding()
    {
        var rig = new WatchfulHoldsRig();
        rig.Player = rig.Player with { MenCount = 300 };

        rig.TickPlayer();

        // 300 x 0.01f and 300 x 0.02f fall a hair under 3 and 6 in single precision
        CollectionAssert.AreEqual(new (object, int, int)[] { (WatchfulHoldsRig.PlayerHandle, 3, 6) }, rig.Adapter.LossCalls);
    }

    [TestMethod]
    public void OnHourlyTickParty_Losses_ScaleWithTheCultureLossMultiplier()
    {
        var rig = new WatchfulHoldsRig();
        rig.Player = rig.Player with { MenCount = 200 };
        rig.Config.Cultures["gondor"] = new WatchfulCultureRow { LossMultiplier = 2f };

        rig.TickPlayer();

        CollectionAssert.AreEqual(new (object, int, int)[] { (WatchfulHoldsRig.PlayerHandle, 4, 8) }, rig.Adapter.LossCalls);
    }

    [TestMethod]
    public void OnHourlyTickParty_Losses_ComeFromTheConfiguredFractions()
    {
        var rig = new WatchfulHoldsRig();
        rig.Player = rig.Player with { MenCount = 200 };
        rig.Config.KillFraction = 0.1f;
        rig.Config.WoundFraction = 0.25f;
        rig.Config.FoodLossFraction = 0.4f;
        rig.Config.MoraleLoss = 33f;

        rig.TickPlayer();

        CollectionAssert.AreEqual(new (object, int, int)[] { (WatchfulHoldsRig.PlayerHandle, 20, 50) }, rig.Adapter.LossCalls);
        Assert.AreEqual(0.4f, rig.Adapter.FoodCalls.Single().Fraction);
        Assert.AreEqual(33f, rig.Adapter.MoraleCalls.Single().Amount);
    }

    [TestMethod]
    public void OnHourlyTickParty_ExtraMaximumsComeFromTheConfig()
    {
        var rig = new WatchfulHoldsRig();
        rig.Player = rig.Player with { MenCount = 200 };
        rig.Config.KillExtraMax = 4;
        rig.Config.WoundExtraMax = 9;
        rig.Random.Extra = 100;

        rig.TickPlayer();

        CollectionAssert.AreEqual(new (object, int, int)[] { (WatchfulHoldsRig.PlayerHandle, 2 + 4, 4 + 9) }, rig.Adapter.LossCalls);
        CollectionAssert.Contains(rig.Random.NextCalls, (0, 5), "kill extra is 0..4");
        CollectionAssert.Contains(rig.Random.NextCalls, (0, 10), "wound extra is 0..9");
    }

    [TestMethod]
    public void OnHourlyTickParty_MessageReportsWhatWasAppliedNotWhatWasAsked()
    {
        var rig = new WatchfulHoldsRig();
        rig.Player = rig.Player with { MenCount = 200 };
        rig.Adapter.LossResult = (_, kill, wound) => (Math.Min(kill, 1), 0); // only one healthy regular was left
        rig.Adapter.FoodResult = (_, _) => 0;

        rig.TickPlayer();

        Assert.AreEqual((WatchfulHoldsRig.Fort, 1, 0, 0), rig.Notices.Harried.Single());
    }

    // ---- the player's army --------------------------------------------------------------------

    private static WatchfulHoldsRig ArmyRig(out WatchfulPartySnapshot leader, out WatchfulPartySnapshot second, out WatchfulPartySnapshot third)
    {
        var rig = new WatchfulHoldsRig();
        leader = WatchfulHoldsRig.Party(WatchfulHoldsRig.PlayerHandle, isMain: true, men: 100) with { IsArmyLeader = true };
        second = WatchfulHoldsRig.Party("lord_b", isMain: false, men: 200);
        third = WatchfulHoldsRig.Party("lord_c", isMain: false, men: 300);
        rig.Player = leader;
        rig.Adapter.Armies[WatchfulHoldsRig.PlayerHandle] = new[] { leader, second, third };
        return rig;
    }

    [TestMethod]
    public void OnHourlyTickParty_PlayerLeadsAnArmy_EveryMemberPartyTakesItsOwnLosses()
    {
        var rig = ArmyRig(out _, out _, out _);

        rig.TickPlayer();

        // kill 1%, wound 2% of each party's own men
        CollectionAssert.AreEqual(
            new (object, int, int)[] { (WatchfulHoldsRig.PlayerHandle, 1, 2), ("lord_b", 2, 4), ("lord_c", 3, 6) },
            rig.Adapter.LossCalls);
    }

    [TestMethod]
    public void OnHourlyTickParty_PlayerLeadsAnArmy_EveryMemberLosesFoodFromItsOwnStores()
    {
        // Regression: food taken from the leader alone leaves the followers' wagons full and the leader's empty.
        var rig = ArmyRig(out _, out _, out _);

        rig.TickPlayer();

        CollectionAssert.AreEqual(
            new (object, float)[] { (WatchfulHoldsRig.PlayerHandle, 0.05f), ("lord_b", 0.05f), ("lord_c", 0.05f) },
            rig.Adapter.FoodCalls);
    }

    [TestMethod]
    public void OnHourlyTickParty_PlayerLeadsAnArmy_EveryMemberLosesMorale()
    {
        var rig = ArmyRig(out _, out _, out _);

        rig.TickPlayer();

        CollectionAssert.AreEqual(
            new (object, float)[] { (WatchfulHoldsRig.PlayerHandle, 10f), ("lord_b", 10f), ("lord_c", 10f) },
            rig.Adapter.MoraleCalls);
    }

    [TestMethod]
    public void OnHourlyTickParty_PlayerLeadsAnArmy_CohesionDropsOnceOnTheLeader()
    {
        var rig = ArmyRig(out _, out _, out _);

        rig.TickPlayer();

        CollectionAssert.AreEqual(new (object, float, float)[] { (WatchfulHoldsRig.PlayerHandle, 5f, 10f) }, rig.Adapter.CohesionCalls);
    }

    [TestMethod]
    public void OnHourlyTickParty_PlayerLeadsAnArmy_CohesionLossAndFloorComeFromTheConfig()
    {
        var rig = ArmyRig(out _, out _, out _);
        rig.Config.CohesionLoss = 12f;
        rig.Config.CohesionFloor = 30f;

        rig.TickPlayer();

        CollectionAssert.AreEqual(new (object, float, float)[] { (WatchfulHoldsRig.PlayerHandle, 12f, 30f) }, rig.Adapter.CohesionCalls);
    }

    [TestMethod]
    public void OnHourlyTickParty_PlayerLeadsAnArmy_OnlyTheLeaderIsDisorganized()
    {
        var rig = ArmyRig(out _, out _, out _);

        rig.TickPlayer();

        CollectionAssert.AreEqual(new object[] { WatchfulHoldsRig.PlayerHandle }, rig.Adapter.Disorganized);
    }

    [TestMethod]
    public void OnHourlyTickParty_PlayerLeadsAnArmy_OneMessageWithTheTotalsAcrossTheArmy()
    {
        var rig = ArmyRig(out _, out _, out _);

        rig.TickPlayer();

        // killed 1 + 2 + 3, wounded 2 + 4 + 6, food 7 per party
        Assert.AreEqual((WatchfulHoldsRig.Fort, 6, 12, 21), rig.Notices.Harried.Single());
    }

    [TestMethod]
    public void OnHourlyTickParty_PlayerLeadsAnArmy_EachMembersLossesAreClampedByItsOwnRoster()
    {
        var rig = ArmyRig(out _, out _, out _);
        rig.Adapter.LossResult = (handle, kill, wound) => (string)handle == "lord_b" ? (0, 1) : (kill, wound);

        rig.TickPlayer();

        // leader 1/2 + lord_b 0/1 + lord_c 3/6
        Assert.AreEqual((WatchfulHoldsRig.Fort, 4, 9, 21), rig.Notices.Harried.Single());
    }

    [TestMethod]
    public void OnHourlyTickParty_PlayerLeadsAnArmy_TheLossMultiplierScalesEveryMember()
    {
        var rig = ArmyRig(out _, out _, out _);
        rig.Config.Cultures["gondor"] = new WatchfulCultureRow { LossMultiplier = 2f };

        rig.TickPlayer();

        CollectionAssert.AreEqual(
            new (object, int, int)[] { (WatchfulHoldsRig.PlayerHandle, 2, 4), ("lord_b", 4, 8), ("lord_c", 6, 12) },
            rig.Adapter.LossCalls);
    }

    [TestMethod]
    public void OnHourlyTickParty_PlayerLeadsAnArmyButNoMembersAreReported_ActsOnTheLeaderAlone()
    {
        var rig = ArmyRig(out _, out _, out _);
        rig.Adapter.Armies.Clear();

        rig.TickPlayer();

        CollectionAssert.AreEqual(new (object, int, int)[] { (WatchfulHoldsRig.PlayerHandle, 1, 2) }, rig.Adapter.LossCalls);
        Assert.AreEqual(1, rig.Adapter.CohesionCalls.Count);
    }

    [TestMethod]
    public void OnHourlyTickParty_AiArmyLeader_IsOnlyDisorganized()
    {
        var rig = new WatchfulHoldsRig();
        rig.Ai = rig.Ai with { IsArmyLeader = true };
        rig.Adapter.Armies[WatchfulHoldsRig.AiHandle] = new[] { rig.Ai, WatchfulHoldsRig.Party("follower", isMain: false) };

        rig.TickAi();

        CollectionAssert.AreEqual(new object[] { WatchfulHoldsRig.AiHandle }, rig.Adapter.Disorganized);
        Assert.AreEqual(0, rig.Adapter.CohesionCalls.Count, "an AI army's cohesion is left alone");
        Assert.AreEqual(0, rig.Adapter.LossCalls.Count);
    }

    // ---- the horn -----------------------------------------------------------------------------

    [TestMethod]
    public void OnHourlyTickParty_FirstHarassment_SoundsTheHorn()
    {
        var rig = new WatchfulHoldsRig();

        rig.TickPlayer();

        Assert.AreEqual(1, rig.Notices.Horns);
    }

    [TestMethod]
    public void OnHourlyTickParty_HornWithinTheCooldown_StaysSilentButTheMessageStillShows()
    {
        var rig = new WatchfulHoldsRig();
        rig.TickPlayer();

        rig.Adapter.Hours += 23.9;
        rig.TickPlayer();

        Assert.AreEqual(1, rig.Notices.Horns);
        Assert.AreEqual(2, rig.Notices.Harried.Count);
    }

    [TestMethod]
    public void OnHourlyTickParty_HornAfterTheCooldown_SoundsAgain()
    {
        var rig = new WatchfulHoldsRig();
        rig.TickPlayer();

        rig.Adapter.Hours += 24.0;
        rig.TickPlayer();

        Assert.AreEqual(2, rig.Notices.Horns);
    }

    [TestMethod]
    public void OnHourlyTickParty_HornCooldownComesFromTheConfig()
    {
        var rig = new WatchfulHoldsRig();
        rig.Config.HornCooldownHours = 2f;
        rig.TickPlayer();

        rig.Adapter.Hours += 2.0;
        rig.TickPlayer();

        Assert.AreEqual(2, rig.Notices.Horns);
    }

    [TestMethod]
    public void OnHourlyTickParty_ZeroHornCooldown_SoundsEveryTime()
    {
        var rig = new WatchfulHoldsRig();
        rig.Config.HornCooldownHours = 0f;

        rig.TickPlayer();
        rig.TickPlayer();

        Assert.AreEqual(2, rig.Notices.Horns);
    }

    [TestMethod]
    public void OnHourlyTickParty_AnAiHarassment_DoesNotStartTheHornClock()
    {
        var rig = new WatchfulHoldsRig();
        rig.TickAi();

        rig.TickPlayer();

        Assert.AreEqual(1, rig.Notices.Horns);
    }

    [TestMethod]
    public void ResetForNewSession_ForgetsTheHornClock()
    {
        var rig = new WatchfulHoldsRig();
        rig.TickPlayer();

        rig.Sut.ResetForNewSession();
        rig.Adapter.Hours += 1.0;
        rig.TickPlayer();

        Assert.AreEqual(2, rig.Notices.Horns, "a new campaign owes no cooldown");
    }

    [TestMethod]
    public void OnHourlyTickParty_AClockThatWentBackwards_StaysSilentUntilItCatchesUp()
    {
        var rig = new WatchfulHoldsRig();
        rig.TickPlayer();

        rig.Adapter.Hours -= 100.0; // a loaded earlier save without a reset: no horn rather than a stuck one
        rig.TickPlayer();

        Assert.AreEqual(1, rig.Notices.Horns);
    }

    // ---- diagnostics --------------------------------------------------------------------------

    [TestMethod]
    public void LogDailySummary_LogsOneDebugLineWithTheCountsAndResetsThem()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Hours = 48.5; // day 2
        rig.TickPlayer();
        rig.TickAi();
        rig.Player = rig.Player with { IsMoving = false };
        rig.TickPlayer();

        rig.Sut.LogDailySummary();

        rig.Logger.Received(1).LogDebug(Arg.Is<string>(s =>
            s.StartsWith("[WatchfulHolds] day 2: checks=3, aiTriggers=1, playerTriggers=1, maxHourMs=")));
    }

    [TestMethod]
    public void LogDailySummary_AfterALog_TheNextDayStartsFromZero()
    {
        var rig = new WatchfulHoldsRig();
        rig.TickPlayer();
        rig.Sut.LogDailySummary();
        rig.Logger.ClearReceivedCalls();

        rig.Sut.LogDailySummary();

        rig.Logger.Received(1).LogDebug(Arg.Is<string>(s =>
            s.Contains("checks=0, aiTriggers=0, playerTriggers=0, maxHourMs=0.000")));
    }

    [TestMethod]
    public void LogDailySummary_MaxHourMs_IsAFiniteNonNegativeNumberInInvariantFormat()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Hours = 10.2;
        rig.TickPlayer();
        rig.Adapter.Hours = 11.7;
        rig.TickAi();
        string? line = null;
        rig.Logger.When(l => l.LogDebug(Arg.Any<string>())).Do(c => line = c.Arg<string>());

        rig.Sut.LogDailySummary();

        Assert.IsNotNull(line);
        var tail = line!.Substring(line.IndexOf("maxHourMs=", StringComparison.Ordinal) + "maxHourMs=".Length);
        Assert.IsTrue(double.TryParse(tail, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ms), line);
        Assert.IsTrue(ms >= 0 && !double.IsInfinity(ms));
    }

    [TestMethod]
    public void OnHourlyTickParty_MasterToggleOff_CountsNoChecks()
    {
        var rig = new WatchfulHoldsRig();
        rig.Settings.Enabled = false;
        rig.TickPlayer();
        rig.Settings.Enabled = true;
        rig.TickAi();

        rig.Sut.LogDailySummary();

        rig.Logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("checks=1,")));
    }

    [TestMethod]
    public void ResetForNewSession_ClearsTheCounters()
    {
        var rig = new WatchfulHoldsRig();
        rig.TickPlayer();

        rig.Sut.ResetForNewSession();
        rig.Sut.LogDailySummary();

        rig.Logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("checks=0, aiTriggers=0, playerTriggers=0")));
    }

    [TestMethod]
    public void OnHourlyTickParty_EngineReadThrows_PropagatesAfterCountingTheCheck()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.ThrowOnRead = true;

        Assert.ThrowsException<InvalidOperationException>(() => rig.TickPlayer());
        rig.Adapter.ThrowOnRead = false;
        rig.Sut.LogDailySummary();

        rig.Logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("checks=1,")));
    }

    [TestMethod]
    public void OnHourlyTickParty_NonFiniteCampaignClock_StillWorks()
    {
        var rig = new WatchfulHoldsRig();
        rig.Adapter.Hours = double.NaN;

        rig.TickPlayer();
        rig.Sut.LogDailySummary();

        rig.Logger.Received(1).LogDebug(Arg.Is<string>(s => s.Contains("day -1:")));
        Assert.AreEqual(1, rig.Notices.Harried.Count, "a bad clock only costs the diagnostics");
    }
}
