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
/// <c>Army.LeaderParty.LeaderHero</c> unguarded. Patch123 contains that NRE only when it was thrown in
/// the target's own body and the hero's state reaches one of those reads; every other fault propagates.
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
        Probe.Observed = null;
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
    public void Capture_NullHero_ContainsNothing() =>
        Assert.IsFalse(LastSeenState.Capture(null).ReachesUnguardedLeaderRead);

    // --- the fallback text ---

    [TestMethod]
    public void FallbackText_WithSettlement_IsTheLastSeenTextWithBothVariables()
    {
        InitializeTexts();
        var link = new TextObject("Bree");

        var (text, kind) = HeroHelper_GetLastSeenText_Patch.FallbackText(State(settlementLink: link, isInSettlement: true));

        Assert.AreEqual("last seen", kind);
        StringAssert.Contains(text.Value, "LAST_SEEN_RAW");
        Assert.AreSame(link, text.Attributes["SETTLEMENT"]);
        Assert.AreEqual(1, text.Attributes["IS_IN_SETTLEMENT"]);
    }

    [TestMethod]
    public void FallbackText_NearSettlement_SetsIsInSettlementZero()
    {
        InitializeTexts();

        var (text, _) = HeroHelper_GetLastSeenText_Patch.FallbackText(State(settlementLink: new TextObject("Bree")));

        Assert.AreEqual(0, text.Attributes["IS_IN_SETTLEMENT"]);
    }

    [TestMethod]
    public void FallbackText_NoSettlementLink_IsTheNeverSeenText()
    {
        InitializeTexts();

        var (text, kind) = HeroHelper_GetLastSeenText_Patch.FallbackText(State(settlementLink: null));

        Assert.AreEqual("never seen", kind);
        StringAssert.Contains(text.Value, "NEVER_SEEN_RAW");
    }

    [TestMethod]
    public void FallbackText_GameTextsNotInitialized_IsEmptyNotNull()
    {
        var (text, kind) = HeroHelper_GetLastSeenText_Patch.FallbackText(State(settlementLink: new TextObject("Bree")));

        Assert.IsNotNull(text, "The page calls ToString() on the result, so it must never be null.");
        Assert.AreEqual("empty", kind);
    }

    // --- reporting ---

    [TestMethod]
    public void Contain_SameHeroTwice_LogsOnce()
    {
        HeroHelper_GetLastSeenText_Patch.Contain(State(heroId: "lord_a"), new NullReferenceException());
        HeroHelper_GetLastSeenText_Patch.Contain(State(heroId: "lord_a"), new NullReferenceException());

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("[LastSeenGuard]") && m.Contains("lord_a")));
    }

    [TestMethod]
    public void Contain_TwoHeroes_LogsEach()
    {
        HeroHelper_GetLastSeenText_Patch.Contain(State(heroId: "lord_a"), new NullReferenceException());
        HeroHelper_GetLastSeenText_Patch.Contain(State(heroId: "lord_b"), new NullReferenceException());

        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("[LastSeenGuard]")));
    }

    [TestMethod]
    public void Contain_NoLogger_StillReturnsText()
    {
        HeroHelper_GetLastSeenText_Patch.Initialize(null);

        Assert.IsNotNull(HeroHelper_GetLastSeenText_Patch.Contain(State(), new NullReferenceException()));
    }

    // --- the finalizer's gates, called directly ---

    [TestMethod]
    public void Finalizer_NoException_LeavesResultUntouched()
    {
        var vanilla = new TextObject("vanilla");
        var result = vanilla;

        var returned = HeroHelper_GetLastSeenText_Patch.Finalizer(null, null, ref result, null);

        Assert.IsNull(returned);
        Assert.AreSame(vanilla, result, "A normal call must keep vanilla's text.");
    }

    [TestMethod]
    public void Finalizer_OtherException_PropagatesSameInstance()
    {
        TextObject result = null!;
        var other = new InvalidOperationException("unrelated");

        var returned = HeroHelper_GetLastSeenText_Patch.Finalizer(other, null, ref result, null);

        Assert.AreSame<Exception?>(other, returned);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Finalizer_NreNotFromTheTarget_PropagatesSameInstance()
    {
        TextObject result = null!;
        var nre = new NullReferenceException("never thrown, so no frame names the target");

        var returned = HeroHelper_GetLastSeenText_Patch.Finalizer(nre, null, ref result, TargetMethod());

        Assert.AreSame<Exception?>(nre, returned, "An NRE the target did not throw is not this defect.");
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // --- the origin check, through real Harmony on a synthetic target ---

    [TestMethod]
    public void ThrewInOriginal_NreFromTheTargetsOwnBody_IsTrue()
    {
        Assert.IsTrue(RunPatchedSynthetic(foreignPrefix: false));
    }

    [TestMethod]
    public void ThrewInOriginal_NreFromAnotherOwnersPrefix_IsFalse()
    {
        Assert.IsFalse(RunPatchedSynthetic(foreignPrefix: true), "Codex 2026-10-26 HIGH: a foreign prefix's NRE was swallowed.");
    }

    [TestMethod]
    public void ThrewInOriginal_NoOriginal_IsFalse() =>
        Assert.IsFalse(HeroHelper_GetLastSeenText_Patch.ThrewInOriginal(new NullReferenceException(), null));

    // --- binding against the installed engine ---

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch_TargetsGetLastSeenText_UnderOwnCategory()
    {
        var type = typeof(HeroHelper_GetLastSeenText_Patch);
        var target = type.GetCustomAttributes<HarmonyPatch>().Single().info;

        Assert.AreEqual(typeof(HeroHelper), target.declaringType);
        Assert.AreEqual(nameof(HeroHelper.GetLastSeenText), target.methodName);
        Assert.AreEqual("Patch123_LastSeenTextGuard", type.GetCustomAttribute<HarmonyPatchCategory>()!.info.category);
        CollectionAssert.AreEqual(new[] { "hero" }, TargetMethod().GetParameters().Select(p => p.Name).ToArray(),
            "Harmony binds the finalizer's 'hero' parameter by this name.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void PatchCategory_AppliedToTheInstalledEngine_AttachesOneFinalizer_AndTheJitAccepts()
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
            Assert.AreEqual(1, info!.Finalizers.Count(p => p.owner == id), "finalizer count");
            Assert.AreEqual(0, info.Prefixes.Count(p => p.owner == id) + info.Postfixes.Count(p => p.owner == id));
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
        bool isPrisoner = false,
        bool hasParty = true,
        bool partyIsMobile = true,
        bool partyLeaderMissing = false,
        bool hasArmy = false,
        bool armyLeaderMissing = false,
        TextObject? settlementLink = null,
        bool isInSettlement = false) =>
        new(heroId, knownAndAlive, hasLastSeen, isPrisoner, hasParty, "party_test", partyIsMobile,
            partyLeaderMissing, hasArmy, armyLeaderMissing, settlementLink, isInSettlement);

    private static void InitializeTexts()
    {
        var manager = new GameTextManager();
        manager.AddGameText("str_last_seen_encyclopedia_entry")
            .AddVariationWithId("", new TextObject("LAST_SEEN_RAW {SETTLEMENT}"), new List<GameTextManager.ChoiceTag>());
        manager.AddGameText("str_never_seen_encyclopedia_entry")
            .AddVariationWithId("", new TextObject("NEVER_SEEN_RAW"), new List<GameTextManager.ChoiceTag>());
        GameTexts.Initialize(manager);
    }

    private static bool RunPatchedSynthetic(bool foreignPrefix)
    {
        const string id = "taom.tests.lastseenguard.origin";
        var target = AccessTools.Method(typeof(SyntheticTarget), nameof(SyntheticTarget.Run));
        var harmony = new Harmony(id);
        try
        {
            harmony.Patch(target,
                prefix: foreignPrefix ? new HarmonyMethod(typeof(ForeignPatch), nameof(ForeignPatch.Prefix)) : null,
                finalizer: new HarmonyMethod(typeof(Probe), nameof(Probe.Finalizer)));
            SyntheticTarget.Run(null);
            Assert.IsNotNull(Probe.Observed, "the probe finalizer saw no exception");
            return Probe.Observed!.Value;
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    public static class SyntheticTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Run(object? hero)
        {
            object? missing = hero;
            return missing!.ToString();
        }
    }

    public static class ForeignPatch
    {
        public static void Prefix() => throw new NullReferenceException("foreign prefix");
    }

    public static class Probe
    {
        public static bool? Observed;

        public static Exception? Finalizer(Exception? __exception, MethodBase? __originalMethod)
        {
            if (__exception != null)
                Observed = HeroHelper_GetLastSeenText_Patch.ThrewInOriginal(__exception, __originalMethod);
            return null;
        }
    }
}
