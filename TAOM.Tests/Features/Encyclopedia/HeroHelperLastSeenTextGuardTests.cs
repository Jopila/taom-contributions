using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.Encyclopedia;
using TAOM.Features.Encyclopedia.Hooks;

namespace TAOM.Tests.Features.Encyclopedia;

/// <summary>
/// Crash 9b2a103e: v1.5.x <c>HeroHelper.GetLastSeenText</c> reads <c>partyBase.LeaderHero</c> and
/// <c>Army.LeaderParty.LeaderHero</c> unguarded. Patch123's prefix answers only the states that reach
/// those reads and leaves every other call, and every exception, to vanilla.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HeroHelperLastSeenTextGuardTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        HeroHelper_GetLastSeenText_Patch.Initialize(_logger);
    }

    [TestCleanup]
    public void Cleanup() => GameTexts.Initialize(null);

    // --- the state rule, one test per vanilla branch (v1.5.5 HeroHelper.cs) ---

    [TestMethod]
    public void Reaches_PrisonerOfLeaderlessPartyWithoutArmy_IsTrue() =>
        Assert.IsTrue(State(isPrisoner: true, partyLeaderMissing: true).ReachesUnguardedLeaderRead);

    [TestMethod]
    public void Reaches_PrisonerOfLedParty_IsFalse() =>
        Assert.IsFalse(State(isPrisoner: true).ReachesUnguardedLeaderRead);

    [TestMethod]
    public void Reaches_PrisonerInArmyWithLeaderlessLeadParty_IsTrue() =>
        Assert.IsTrue(State(isPrisoner: true, hasArmy: true, armyLeaderMissing: true).ReachesUnguardedLeaderRead);

    [TestMethod]
    public void Reaches_PrisonerOfLeaderlessPartyInLedArmy_IsFalse() =>
        Assert.IsFalse(State(isPrisoner: true, partyLeaderMissing: true, hasArmy: true).ReachesUnguardedLeaderRead);

    [TestMethod]
    public void Reaches_PrisonerInSettlement_IsFalse() =>
        Assert.IsFalse(State(isPrisoner: true, partyIsMobile: false, partyLeaderMissing: true).ReachesUnguardedLeaderRead);

    [TestMethod]
    public void Reaches_FreeHeroInLeaderlessPartyWithoutArmy_IsFalse() =>
        Assert.IsFalse(State(partyLeaderMissing: true).ReachesUnguardedLeaderRead);

    [TestMethod]
    public void Reaches_FreeHeroInArmyWithLeaderlessLeadParty_IsTrue() =>
        Assert.IsTrue(State(hasArmy: true, armyLeaderMissing: true).ReachesUnguardedLeaderRead);

    [TestMethod]
    public void Reaches_FreeHeroWhosePartyIsASettlement_IsTrue() =>
        Assert.IsTrue(State(partyIsMobile: false).ReachesUnguardedLeaderRead);

    [TestMethod]
    public void Reaches_UnknownDeadOrFugitiveHero_IsFalse() =>
        Assert.IsFalse(State(knownAndAlive: false, isPrisoner: true, partyLeaderMissing: true).ReachesUnguardedLeaderRead);

    [TestMethod]
    public void Reaches_NeverSeenHero_IsFalse() =>
        Assert.IsFalse(State(hasLastSeen: false, isPrisoner: true, partyLeaderMissing: true).ReachesUnguardedLeaderRead);

    [TestMethod]
    public void Reaches_HeroWithNoParty_IsFalse() =>
        Assert.IsFalse(State(hasParty: false, isPrisoner: true, partyLeaderMissing: true).ReachesUnguardedLeaderRead);

    [TestMethod]
    public void Capture_NullHero_LeavesTheCallToVanilla() =>
        Assert.IsFalse(LastSeenState.Capture(null).ReachesUnguardedLeaderRead);

    // --- the production decision ---

    [TestMethod]
    public void Decide_ReachingState_SkipsVanillaWithTheLastSeenText()
    {
        InitializeTexts();
        var link = new TextObject("Bree");
        TextObject result = null!;

        var runVanilla = HeroHelper_GetLastSeenText_Patch.Decide(
            State(isPrisoner: true, partyLeaderMissing: true, isInSettlement: true), () => link, ref result);

        Assert.IsFalse(runVanilla, "The state reaches a missing leader: vanilla must not run.");
        StringAssert.Contains(result.Value, "LAST_SEEN_RAW");
        Assert.AreSame(link, result.Attributes["SETTLEMENT"]);
        Assert.AreEqual(1, result.Attributes["IS_IN_SETTLEMENT"]);
    }

    [TestMethod]
    public void Decide_SafeState_RunsVanillaAndLeavesTheResult()
    {
        var vanilla = new TextObject("vanilla");
        var result = vanilla;
        var linkRead = false;

        var runVanilla = HeroHelper_GetLastSeenText_Patch.Decide(
            State(isPrisoner: true), () => { linkRead = true; return null; }, ref result);

        Assert.IsTrue(runVanilla);
        Assert.AreSame(vanilla, result);
        Assert.IsFalse(linkRead, "A safe state must not touch the settlement link.");
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Decide_LinkReadThrows_StillSkipsVanillaWithTheNeverSeenText()
    {
        InitializeTexts();
        TextObject result = null!;

        var runVanilla = HeroHelper_GetLastSeenText_Patch.Decide(
            State(isPrisoner: true, partyLeaderMissing: true),
            () => throw new NullReferenceException("link formatting"), ref result);

        Assert.IsFalse(runVanilla, "A formatting failure must not reopen the crash (Codex 2026-10-26 MEDIUM).");
        StringAssert.Contains(result.Value, "NEVER_SEEN_RAW");
    }

    [TestMethod]
    public void Decide_GameTextsNotInitialized_GivesEmptyTextNotNull()
    {
        TextObject result = null!;

        var runVanilla = HeroHelper_GetLastSeenText_Patch.Decide(
            State(hasArmy: true, armyLeaderMissing: true), () => new TextObject("Bree"), ref result);

        Assert.IsFalse(runVanilla);
        Assert.IsNotNull(result, "The page calls ToString() on the result, so it must never be null.");
    }

    [TestMethod]
    public void Decide_SameHeroTwice_LogsOnce()
    {
        TextObject result = null!;
        HeroHelper_GetLastSeenText_Patch.Decide(State(heroId: "lord_a", partyIsMobile: false), () => null, ref result);
        HeroHelper_GetLastSeenText_Patch.Decide(State(heroId: "lord_a", partyIsMobile: false), () => null, ref result);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("[LastSeenGuard]") && m.Contains("lord_a")));
    }

    [TestMethod]
    public void Decide_TwoHeroes_LogsEach()
    {
        TextObject result = null!;
        HeroHelper_GetLastSeenText_Patch.Decide(State(heroId: "lord_a", partyIsMobile: false), () => null, ref result);
        HeroHelper_GetLastSeenText_Patch.Decide(State(heroId: "lord_b", partyIsMobile: false), () => null, ref result);

        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("[LastSeenGuard]")));
    }

    [TestMethod]
    public void Decide_NoLogger_StillAnswers()
    {
        HeroHelper_GetLastSeenText_Patch.Initialize(null);
        TextObject result = null!;

        Assert.IsFalse(HeroHelper_GetLastSeenText_Patch.Decide(State(partyIsMobile: false), () => null, ref result));
        Assert.IsNotNull(result);
    }

    // --- the fallback text ---

    [TestMethod]
    public void FallbackText_NearSettlement_SetsIsInSettlementZero()
    {
        InitializeTexts();

        var (text, kind) = HeroHelper_GetLastSeenText_Patch.FallbackText(new TextObject("Bree"), false);

        Assert.AreEqual("last seen", kind);
        Assert.AreEqual(0, text.Attributes["IS_IN_SETTLEMENT"]);
    }

    [TestMethod]
    public void FallbackText_NoLink_IsTheNeverSeenText()
    {
        InitializeTexts();

        var (text, kind) = HeroHelper_GetLastSeenText_Patch.FallbackText(null, false);

        Assert.AreEqual("never seen", kind);
        StringAssert.Contains(text.Value, "NEVER_SEEN_RAW");
    }

    // --- binding against the installed engine ---

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch_TargetsGetLastSeenText_UnderOwnCategory_WithTheNamesHarmonyBinds()
    {
        var type = typeof(HeroHelper_GetLastSeenText_Patch);
        var target = type.GetCustomAttributes<HarmonyPatch>().Single().info;

        Assert.AreEqual(typeof(HeroHelper), target.declaringType);
        Assert.AreEqual(nameof(HeroHelper.GetLastSeenText), target.methodName);
        Assert.AreEqual("Patch123_LastSeenTextGuard", type.GetCustomAttribute<HarmonyPatchCategory>()!.info.category);
        CollectionAssert.AreEqual(new[] { "hero" }, TargetMethod().GetParameters().Select(p => p.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "hero", "__result" },
            type.GetMethod("Prefix")!.GetParameters().Select(p => p.Name).ToArray());
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void PatchCategory_AppliedToTheInstalledEngine_AttachesOnePrefix_AndTheJitAccepts()
    {
        const string id = "taom.tests.lastseenguard.jit";
        var target = TargetMethod();
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(HeroHelper_GetLastSeenText_Patch).Assembly)
                .Apply(harmony, HeroHelper_GetLastSeenText_Patch.PatchCategory);

            var info = Harmony.GetPatchInfo(target);
            Assert.IsNotNull(info, "the target was not patched: the class is not in the category");
            Assert.AreEqual(1, info!.Prefixes.Count(p => p.owner == id), "prefix count");
            Assert.AreEqual(0, info.Postfixes.Count(p => p.owner == id) + info.Finalizers.Count(p => p.owner == id)
                + info.Transpilers.Count(p => p.owner == id));
            RuntimeHelpers.PrepareMethod(target.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    // --- helpers ---

    private static MethodInfo TargetMethod() =>
        AccessTools.Method(typeof(HeroHelper), nameof(HeroHelper.GetLastSeenText), new[] { typeof(Hero) });

    private static LastSeenState State(
        string heroId = "lord_test",
        bool knownAndAlive = true,
        bool hasLastSeen = true,
        bool isInSettlement = false,
        bool isPrisoner = false,
        bool hasParty = true,
        bool partyIsMobile = true,
        bool partyLeaderMissing = false,
        bool hasArmy = false,
        bool armyLeaderMissing = false) =>
        new(heroId, knownAndAlive, hasLastSeen, isInSettlement, isPrisoner, hasParty, "party_test", partyIsMobile,
            partyLeaderMissing, hasArmy, armyLeaderMissing);

    private static void InitializeTexts()
    {
        var manager = new GameTextManager();
        manager.AddGameText("str_last_seen_encyclopedia_entry")
            .AddVariationWithId("", new TextObject("LAST_SEEN_RAW {SETTLEMENT}"), new List<GameTextManager.ChoiceTag>());
        manager.AddGameText("str_never_seen_encyclopedia_entry")
            .AddVariationWithId("", new TextObject("NEVER_SEEN_RAW"), new List<GameTextManager.ChoiceTag>());
        GameTexts.Initialize(manager);
    }
}
