using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HarmonyLib;
using Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.HiddenHosts;
using TAOM.Features.HiddenHosts.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// The tooltip brackets as pairs, through real Harmony on the real engine methods (harmony-patches.md, "Prefix and
/// finalizer pair"). What a hand call cannot reach is what these cases do: the frame visible inside the original and closed
/// after it, closed after the original throws, the prefix stripped while the finalizer stays (PatchShield removes
/// prefixes, never finalizers), a later finalizer that throws (Harmony reruns every finalizer), and nesting inside an outer
/// frame. The last group runs a vanilla caller of <c>GetPartySizeText</c> that the JIT compiled before the patch existed,
/// which is the inlining check for that small helper.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HiddenHostsTooltipHarmonyPairTests
{
    private const string HarmonyId = "taom.tests.hiddenhosts.tooltips";
    private const BindingFlags OwnStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool _gameLoaded;
    private Harmony _harmony = null!;
    private IHiddenHostsSettings _settings = null!;
    private IPartyIntelAdapter _adapter = null!;
    private IModLogger _logger = null!;

    private static TooltipFrame? _seen;
    private static int _seenCalls;
    private static bool _throwNow;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void ProbePrefix()
    {
        _seen = HiddenHostsTooltipScope.Current;
        _seenCalls++;
    }

    private static void ThrowingFinalizer() => throw new InvalidOperationException("a finalizer after ours");

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        _settings = Substitute.For<IHiddenHostsSettings>();
        _settings.Enabled.Returns(true);
        _adapter = Substitute.For<IPartyIntelAdapter>();
        _adapter.ReadSettlement(Arg.Any<Settlement>()).Returns(new SettlementIntelSnapshot
        {
            HasData = true, StringId = "town_1", DefenderCount = 245, DistanceToMain = 70f, MainSeeingRange = 100f,
        });
        _adapter.Read(Arg.Any<MobileParty>()).Returns(new PartyIntelSnapshot
        {
            HasData = true, StringId = "p1", HealthyCount = 123, DistanceToMain = 70f, MainSeeingRange = 100f, SpottingRatio = 1f,
            IsVisible = true,
        });
        _logger = Substitute.For<IModLogger>();
        HiddenHostsCalls.Initialize(new HiddenHostsService(_settings, new NoScoutIntel()), _adapter, _logger, () => "Defenders", () => "Troops",
            hours => "Counted " + hours + "h ago");
        _seen = null;
        _seenCalls = 0;
        _throwNow = false;
        _harmony = new Harmony(HarmonyId);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _harmony?.UnpatchAll(HarmonyId);
        while (HiddenHostsTooltipScope.Current != null)
            HiddenHostsTooltipScope.Close(new ScopeToken(null));
        HiddenHostsCalls.Initialize(null, null);
    }

    private void Apply(Type patchClass) => _harmony.CreateClassProcessor(patchClass).Patch();

    private static T Bare<T>() where T : class => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static PropertyBasedTooltipVM NewVm()
    {
        var vm = Bare<PropertyBasedTooltipVM>();
        vm.TooltipPropertyList = new MBBindingList<TooltipProperty>();
        return vm;
    }

    private static MethodBase Refresher(string name) => AccessTools.Method(typeof(TooltipRefresherCollection), name);

    private void ProbeLast(MethodBase target) =>
        _harmony.Patch(target, prefix: new HarmonyMethod(typeof(HiddenHostsTooltipHarmonyPairTests).GetMethod(nameof(ProbePrefix), OwnStatic)) { priority = Priority.Last });

    private void ThrowAfterOurs(MethodBase target) =>
        _harmony.Patch(target, finalizer: new HarmonyMethod(typeof(HiddenHostsTooltipHarmonyPairTests).GetMethod(nameof(ThrowingFinalizer), OwnStatic)) { priority = Priority.Last });

    /// <summary>A settlement with no party: the vanilla builder returns normally at its first guard, after our prefix ran.</summary>
    private static void RefreshSettlement() =>
        TooltipRefresherCollection.RefreshSettlementTooltip(NewVm(), new object[] { Bare<Settlement>() });

    /// <summary>A bare party: the vanilla builder throws a NullReferenceException after our prefix ran.</summary>
    private static void RefreshPartyThatThrows() =>
        TooltipRefresherCollection.RefreshMobilePartyTooltip(NewVm(), new object[] { Bare<MobileParty>(), false, true });

    private static ScopeToken OpenOuter() =>
        HiddenHostsTooltipScope.Open(new TooltipFrame(TooltipSubjectKind.Army, TooltipVerdict.Vanilla));

    // ---- T1 to T5 bracket

    [TestMethod]
    public void Bracket_TheFrameIsVisibleInsideTheOriginal_AndClosedAfterIt()
    {
        var target = Refresher(nameof(TooltipRefresherCollection.RefreshSettlementTooltip));
        Apply(typeof(TooltipRefresherCollection_Subject_HiddenHosts_Patch));
        ProbeLast(target);

        RefreshSettlement();

        Assert.AreEqual(1, _seenCalls);
        Assert.AreEqual(TooltipSubjectKind.Settlement, _seen!.Kind);
        Assert.AreEqual(TooltipMode.Masked, _seen.Verdict.Mode);
        Assert.AreEqual(245, _seen.Verdict.SubjectCount);
        Assert.IsNull(HiddenHostsTooltipScope.Current);
    }

    [TestMethod]
    public void Bracket_TheOriginalThrows_TheFrameIsStillClosed_AndTheExceptionPropagates()
    {
        Apply(typeof(TooltipRefresherCollection_Subject_HiddenHosts_Patch));
        ProbeLast(Refresher(nameof(TooltipRefresherCollection.RefreshMobilePartyTooltip)));

        try
        {
            RefreshPartyThatThrows();
            Assert.Fail("the bare party should make the vanilla builder throw");
        }
        catch (AssertFailedException)
        {
            throw;
        }
        catch (Exception)
        {
            // expected: the engine's own exception, not ours
        }

        Assert.AreEqual(TooltipSubjectKind.Party, _seen!.Kind, "the frame was open when the original ran");
        Assert.IsNull(HiddenHostsTooltipScope.Current, "and the finalizer closed it");
        _logger.DidNotReceiveWithAnyArgs().LogError(null!);
    }

    [TestMethod]
    public void Bracket_NestedInsideAnOuterFrame_RestoresIt()
    {
        var target = Refresher(nameof(TooltipRefresherCollection.RefreshSettlementTooltip));
        Apply(typeof(TooltipRefresherCollection_Subject_HiddenHosts_Patch));
        ProbeLast(target);
        var outer = OpenOuter();
        var outerFrame = HiddenHostsTooltipScope.Current;

        RefreshSettlement();

        Assert.AreEqual(TooltipSubjectKind.Settlement, _seen!.Kind, "the inner frame was current inside");
        Assert.AreSame(outerFrame, HiddenHostsTooltipScope.Current, "and the outer one is back");
        HiddenHostsTooltipScope.Close(in outer);
    }

    [TestMethod]
    public void Bracket_ThePrefixStripped_TheFinalizerStays_AndLeavesTheOuterFrame()
    {
        var target = Refresher(nameof(TooltipRefresherCollection.RefreshSettlementTooltip));
        Apply(typeof(TooltipRefresherCollection_Subject_HiddenHosts_Patch));
        _harmony.Unpatch(target, HarmonyPatchType.Prefix, HarmonyId);
        ProbeLast(target);
        var outer = OpenOuter();
        var outerFrame = HiddenHostsTooltipScope.Current;

        RefreshSettlement();

        Assert.AreSame(outerFrame, _seen, "no inner frame was opened");
        Assert.AreSame(outerFrame, HiddenHostsTooltipScope.Current, "and the finalizer with a default token closed nothing");
        Assert.IsTrue(Harmony.GetPatchInfo(target).Finalizers.Any(p => p.owner == HarmonyId), "PatchShield strips prefixes, never finalizers");
        HiddenHostsTooltipScope.Close(in outer);
    }

    [TestMethod]
    public void Bracket_ALaterFinalizerThrows_OurFinalizerRunsAgainAndTheOuterFrameIsRestored()
    {
        var target = Refresher(nameof(TooltipRefresherCollection.RefreshSettlementTooltip));
        Apply(typeof(TooltipRefresherCollection_Subject_HiddenHosts_Patch));
        ThrowAfterOurs(target);
        var outer = OpenOuter();
        var outerFrame = HiddenHostsTooltipScope.Current;

        var thrown = Assert.ThrowsException<InvalidOperationException>(RefreshSettlement);

        Assert.AreEqual("a finalizer after ours", thrown.Message);
        Assert.AreSame(outerFrame, HiddenHostsTooltipScope.Current, "closing twice writes the same previous frame");
        HiddenHostsTooltipScope.Close(in outer);
    }

    [TestMethod]
    public void Bracket_ToggleOff_OpensNothing_AndTheOuterFrameIsUntouched()
    {
        var target = Refresher(nameof(TooltipRefresherCollection.RefreshSettlementTooltip));
        Apply(typeof(TooltipRefresherCollection_Subject_HiddenHosts_Patch));
        ProbeLast(target);
        var outer = OpenOuter();
        var outerFrame = HiddenHostsTooltipScope.Current;
        _settings.Enabled.Returns(false);

        RefreshSettlement();

        Assert.AreSame(outerFrame, _seen);
        Assert.AreSame(outerFrame, HiddenHostsTooltipScope.Current);
        _adapter.DidNotReceiveWithAnyArgs().ReadSettlement(null);
        HiddenHostsTooltipScope.Close(in outer);
    }

    // ---- T6 bracket

    private static PropertyBasedTooltipVM NewPeriodicVm(Type? invokedType, object subject, List<TooltipFrame?> sink)
    {
        var vm = NewVm();
        var baseType = typeof(TooltipBaseVM);
        baseType.GetField("_invokedType", AnyInstance)!.SetValue(vm, invokedType);
        baseType.GetField("_invokedArgs", AnyInstance)!.SetValue(vm, new[] { subject, false, true });
        // A row whose value function records the frame it runs in, as vanilla's count rows do; it runs once here, outside.
        vm.TooltipPropertyList.Add(new TooltipProperty("count", () =>
        {
            if (_throwNow) throw new InvalidOperationException("a row function threw");
            sink.Add(HiddenHostsTooltipScope.Current);
            return "x";
        }, 0));
        sink.Clear();
        return vm;
    }

    private static void RunPeriodicRefresh(PropertyBasedTooltipVM vm) =>
        typeof(PropertyBasedTooltipVM).GetMethod("OnPeriodicRefresh", AnyInstance | BindingFlags.DeclaredOnly)!.Invoke(vm, null);

    [TestMethod]
    public void Periodic_TheRowFunctionsRunInsideAFreshFrame_ThatIsClosedAfter()
    {
        Apply(typeof(PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch));
        var seen = new List<TooltipFrame?>();
        var vm = NewPeriodicVm(typeof(MobileParty), Bare<MobileParty>(), seen);

        RunPeriodicRefresh(vm);

        Assert.AreEqual(1, seen.Count);
        Assert.AreEqual(TooltipSubjectKind.Party, seen[0]!.Kind);
        Assert.AreEqual(TooltipMode.Masked, seen[0]!.Verdict.Mode);
        Assert.IsNull(HiddenHostsTooltipScope.Current);
    }

    [TestMethod]
    public void Periodic_EachRefreshReadsTheDistanceAgain()
    {
        Apply(typeof(PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch));
        var seen = new List<TooltipFrame?>();
        var vm = NewPeriodicVm(typeof(MobileParty), Bare<MobileParty>(), seen);

        RunPeriodicRefresh(vm);
        _adapter.Read(Arg.Any<MobileParty>()).Returns(new PartyIntelSnapshot
        {
            HasData = true, StringId = "p1", HealthyCount = 123, DistanceToMain = 10f, MainSeeingRange = 100f, SpottingRatio = 1f,
            IsVisible = true,
        });
        RunPeriodicRefresh(vm);

        Assert.AreEqual(TooltipMode.Masked, seen[0]!.Verdict.Mode);
        Assert.AreEqual(TooltipMode.Vanilla, seen[1]!.Verdict.Mode, "the party closed in: its numbers are exact on the next refresh");
    }

    [TestMethod]
    public void Periodic_AnUnrelatedTooltipType_OpensNothing()
    {
        Apply(typeof(PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch));
        var seen = new List<TooltipFrame?>();
        var vm = NewPeriodicVm(typeof(Hero), Bare<MobileParty>(), seen);

        RunPeriodicRefresh(vm);

        Assert.AreEqual(1, seen.Count);
        Assert.IsNull(seen[0]);
    }

    [TestMethod]
    public void Periodic_TheRowFunctionThrows_TheFrameIsStillClosed()
    {
        Apply(typeof(PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch));
        var vm = NewPeriodicVm(typeof(MobileParty), Bare<MobileParty>(), new List<TooltipFrame?>());
        _throwNow = true;

        Assert.ThrowsException<TargetInvocationException>(() => RunPeriodicRefresh(vm));

        Assert.IsNull(HiddenHostsTooltipScope.Current);
    }

    [TestMethod]
    public void Periodic_ThePrefixStripped_TheFinalizerStays_AndLeavesTheOuterFrame()
    {
        Apply(typeof(PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch));
        var target = AccessTools.DeclaredMethod(typeof(PropertyBasedTooltipVM), "OnPeriodicRefresh");
        _harmony.Unpatch(target, HarmonyPatchType.Prefix, HarmonyId);
        var seen = new List<TooltipFrame?>();
        var vm = NewPeriodicVm(typeof(MobileParty), Bare<MobileParty>(), seen);
        var outer = OpenOuter();
        var outerFrame = HiddenHostsTooltipScope.Current;

        RunPeriodicRefresh(vm);

        Assert.AreSame(outerFrame, seen[0]);
        Assert.AreSame(outerFrame, HiddenHostsTooltipScope.Current);
        HiddenHostsTooltipScope.Close(in outer);
    }

    [TestMethod]
    public void Periodic_ALaterFinalizerThrows_TheOuterFrameIsRestored()
    {
        Apply(typeof(PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch));
        ThrowAfterOurs(AccessTools.DeclaredMethod(typeof(PropertyBasedTooltipVM), "OnPeriodicRefresh"));
        var vm = NewPeriodicVm(typeof(MobileParty), Bare<MobileParty>(), new List<TooltipFrame?>());
        var outer = OpenOuter();
        var outerFrame = HiddenHostsTooltipScope.Current;

        Assert.ThrowsException<TargetInvocationException>(() => RunPeriodicRefresh(vm));

        Assert.AreSame(outerFrame, HiddenHostsTooltipScope.Current);
        HiddenHostsTooltipScope.Close(in outer);
    }

    // ---- T7 pair, and T10 through a vanilla caller

    private static MethodInfo AddPartyTroopProperties() =>
        typeof(TooltipRefresherCollection).GetMethod("AddPartyTroopProperties", BindingFlags.Static | BindingFlags.NonPublic)!;

    /// <summary>The vanilla private builder, called by reflection with an empty roster and an inspected subject.</summary>
    private static PropertyBasedTooltipVM RunTroopBuilder(bool isInspected = true)
    {
        var vm = NewVm();
        AddPartyTroopProperties().Invoke(null, new object?[] { vm, TroopRoster.CreateDummyTroopRoster(), new TextObject("{=!}Troops"), isInspected, null });
        return vm;
    }

    private static void OpenMaskedFrame() =>
        HiddenHostsTooltipScope.Open(new TooltipFrame(TooltipSubjectKind.Party, new TooltipVerdict(TooltipMode.Masked, 0.7f, 1f, 0, 0)));

    [TestMethod]
    public void Troops_AVanillaCallerCompiledBeforeThePatch_StillPrintsThroughTheGetPartySizeTextPostfix()
    {
        // Baseline first: this JIT-compiles the builder and the closure that calls GetPartySizeText with no patch in place,
        // which is the only way an inlined copy could bypass the detour installed afterwards.
        var baseline = RunTroopBuilder();
        Assert.AreEqual(3, baseline.TooltipPropertyList.Count, "spacer, title and the separator vanilla adds for an inspected subject");
        Assert.AreEqual(" (0)", baseline.TooltipPropertyList[1].ValueLabel);

        Apply(typeof(PartyBaseHelper_GetPartySizeText_HiddenHosts_Patch));
        OpenMaskedFrame();
        var patched = RunTroopBuilder();

        Assert.AreEqual(" (?)", patched.TooltipPropertyList[1].ValueLabel, "the masked count reached the title row");
        Assert.AreEqual(3, patched.TooltipPropertyList.Count, "this patch alone trims nothing");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void Troops_AnUninspectedSubject_GetsOnlyTheSpacerAndTheTitle_SoTheFormationBlockIsGatedByIsInspected()
    {
        var vm = RunTroopBuilder(isInspected: false);

        Assert.AreEqual(2, vm.TooltipPropertyList.Count);
    }

    [TestMethod]
    public void Troops_OutsideAFrame_TheBuilderPrintsVanillasText_EvenWithThePatchApplied()
    {
        Apply(typeof(PartyBaseHelper_GetPartySizeText_HiddenHosts_Patch));

        var vm = RunTroopBuilder();

        Assert.AreEqual(" (0)", vm.TooltipPropertyList[1].ValueLabel);
    }

    [TestMethod]
    public void Troops_TheFullPair_TrimsFromTheSeparatorOn_AndKeepsTheMaskedTitle()
    {
        Apply(typeof(PartyBaseHelper_GetPartySizeText_HiddenHosts_Patch));
        Apply(typeof(TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch));
        OpenMaskedFrame();

        var vm = RunTroopBuilder();

        Assert.AreEqual(2, vm.TooltipPropertyList.Count, "the spacer and the title: the separator is gone");
        Assert.AreEqual(" (?)", vm.TooltipPropertyList[1].ValueLabel);
    }

    [TestMethod]
    public void Troops_ThePrefixStripped_ThePostfixTrimsNothing()
    {
        Apply(typeof(TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch));
        _harmony.Unpatch(AddPartyTroopProperties(), HarmonyPatchType.Prefix, HarmonyId);
        OpenMaskedFrame();

        var vm = RunTroopBuilder();

        Assert.AreEqual(3, vm.TooltipPropertyList.Count, "a default TrimToken must delete nothing");
    }

    [TestMethod]
    public void Troops_VanillaFrame_TrimsNothing()
    {
        Apply(typeof(TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch));
        HiddenHostsTooltipScope.Open(new TooltipFrame(TooltipSubjectKind.Party, TooltipVerdict.Vanilla));

        Assert.AreEqual(3, RunTroopBuilder().TooltipPropertyList.Count);
    }

    // ---- Orders: the postfix on MobileParty.GetBehaviorText (#848). The branches are hand calls in HiddenHostsTooltipHookTests.

    private static readonly TextObject ProbeText = new TextObject("{=!}Besieging X.");

    /// <summary>Stands in for the engine's long branch chain on a bare party: skips the original and returns a known line.</summary>
    private static bool ProbeOrdersPrefix(ref TextObject __result)
    {
        __result = ProbeText;
        return false;
    }

    private static MethodBase BehaviorText() => AccessTools.Method(typeof(MobileParty), nameof(MobileParty.GetBehaviorText));

    private void ProbeOrdersFirst() =>
        _harmony.Patch(BehaviorText(), prefix: new HarmonyMethod(typeof(HiddenHostsTooltipHarmonyPairTests).GetMethod(nameof(ProbeOrdersPrefix), OwnStatic)) { priority = Priority.First });

    [TestMethod]
    public void Orders_ThroughRealHarmony_AMaskedFrameReplacesTheShippedText_AndNoFrameOrAVanillaFrameKeepsTheOriginal()
    {
        ProbeOrdersFirst();
        Apply(typeof(MobileParty_GetBehaviorText_HiddenHosts_Patch));
        var party = Bare<MobileParty>();

        Assert.AreSame(ProbeText, party.GetBehaviorText(), "no frame");
        var token = HiddenHostsTooltipScope.Open(new TooltipFrame(TooltipSubjectKind.Party, new TooltipVerdict(TooltipMode.Masked, 0.7f, 1f, 0, 123)));
        Assert.AreEqual("Orders unknown.", party.GetBehaviorText().ToString(), "the shipped {=taom_hhost_orders_unknown} default renders");
        HiddenHostsTooltipScope.Close(in token);
        HiddenHostsTooltipScope.Open(new TooltipFrame(TooltipSubjectKind.Party, TooltipVerdict.Vanilla));
        Assert.AreSame(ProbeText, party.GetBehaviorText(), "a vanilla frame");
    }

    [TestMethod]
    public void Orders_TheWrapper_ReachesThePatch_FromACallerCompiledBeforeAndAfterIt()
    {
        ProbeOrdersFirst();
        var party = Bare<MobileParty>();
        HiddenHostsTooltipScope.Open(new TooltipFrame(TooltipSubjectKind.Party, new TooltipVerdict(TooltipMode.Masked, 0.7f, 1f, 0, 123)));

        // Baseline first: this JIT-compiles the wrapper with only the probe in place, so an inlined copy could not see our detour.
        Assert.AreEqual("Besieging X.", CampaignUIHelper.GetMobilePartyBehaviorText(party));

        Apply(typeof(MobileParty_GetBehaviorText_HiddenHosts_Patch));

        Assert.AreEqual("Orders unknown.", CampaignUIHelper.GetMobilePartyBehaviorText(party));
        Assert.AreEqual("Orders unknown.", CallWrapperFromAnotherMethod(party), "and a caller compiled after the patch");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string CallWrapperFromAnotherMethod(MobileParty party) => CampaignUIHelper.GetMobilePartyBehaviorText(party);

    // ---- Every patch class applies and compiles against the installed engine

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void EveryTooltipPatchClass_AppliedToTheInstalledEngine_BuildsReplacementsTheJitAccepts()
    {
        var expectations = new (Type PatchClass, MethodBase[] Targets, bool Prefix, bool Postfix, bool Finalizer)[]
        {
            (typeof(TooltipRefresherCollection_Subject_HiddenHosts_Patch),
                new[]
                {
                    Refresher(nameof(TooltipRefresherCollection.RefreshMobilePartyTooltip)),
                    Refresher(nameof(TooltipRefresherCollection.RefreshArmyTooltip)),
                    Refresher(nameof(TooltipRefresherCollection.RefreshSettlementTooltip)),
                    Refresher(nameof(TooltipRefresherCollection.RefreshMapEventTooltip)),
                    Refresher(nameof(TooltipRefresherCollection.RefreshSiegeEventTooltip)),
                }, true, true, true),
            (typeof(PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch),
                new MethodBase[] { AccessTools.DeclaredMethod(typeof(PropertyBasedTooltipVM), "OnPeriodicRefresh") }, true, false, true),
            (typeof(TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch),
                new MethodBase[] { AddPartyTroopProperties() }, true, true, false),
            (typeof(TooltipRefresherCollection_AddEncounterParties_HiddenHosts_Patch),
                typeof(TooltipRefresherCollection).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(m => m.Name == "AddEncounterParties").Cast<MethodBase>().ToArray(), true, false, false),
            (typeof(PartyBaseHelper_GetPartySizeText_HiddenHosts_Patch),
                new MethodBase[] { AccessTools.Method(typeof(PartyBaseHelper), nameof(PartyBaseHelper.GetPartySizeText), new[] { typeof(int), typeof(int), typeof(bool) }) },
                false, true, false),
            (typeof(CampaignUIHelper_GetPartyNameplateText_HiddenHosts_Patch),
                new MethodBase[] { AccessTools.Method(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetPartyNameplateText), new[] { typeof(MobileParty), typeof(bool) }) },
                false, true, false),
            (typeof(MobileParty_GetBehaviorText_HiddenHosts_Patch), new[] { BehaviorText() }, false, true, false),
        };

        foreach (var (patchClass, targets, prefix, postfix, finalizer) in expectations)
        {
            Assert.IsTrue(targets.Length > 0 && targets.All(t => t != null), patchClass.Name + ": every target resolves");
            Apply(patchClass);
            foreach (var target in targets)
            {
                var info = Harmony.GetPatchInfo(target);
                Assert.AreEqual(prefix, info.Prefixes.Any(p => p.owner == HarmonyId), patchClass.Name + " prefix on " + target.Name);
                Assert.AreEqual(postfix, info.Postfixes.Any(p => p.owner == HarmonyId), patchClass.Name + " postfix on " + target.Name);
                Assert.AreEqual(finalizer, info.Finalizers.Any(p => p.owner == HarmonyId), patchClass.Name + " finalizer on " + target.Name);
                RuntimeHelpers.PrepareMethod(target.MethodHandle);
            }
        }

        Assert.AreEqual(2, expectations[3].Targets.Length, "both AddEncounterParties overloads");
        Assert.AreEqual(5, expectations[0].Targets.Length);
    }

    // ---- Phase 3: the remembered total through the vanilla builder and the two-second refresh

    private static PartyIntelSnapshot Snapshot(float distance) => new()
    {
        HasData = true, StringId = "p1", HealthyCount = 123, OwnHealthyCount = 123, DistanceToMain = distance, MainSeeingRange = 100f,
        SpottingRatio = 1f, IsVisible = true,
    };

    private static void OpenRememberedFrame() =>
        HiddenHostsTooltipScope.Open(new TooltipFrame(TooltipSubjectKind.Party,
            new TooltipVerdict(TooltipMode.Masked, 0.7f, 1f, 0, 123, "~123", 5)));

    [TestMethod]
    public void Remembered_TheFullPair_ShowsTheTildeCountInTheTotalRow_OnlyInsideAFrameThatRemembers()
    {
        Apply(typeof(PartyBaseHelper_GetPartySizeText_HiddenHosts_Patch));
        Apply(typeof(TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch));
        var token = HiddenHostsTooltipScope.Open(new TooltipFrame(TooltipSubjectKind.Party,
            new TooltipVerdict(TooltipMode.Masked, 0.7f, 1f, 0, 123, "~123", 5)));

        var vm = RunTroopBuilder();

        Assert.AreEqual(2, vm.TooltipPropertyList.Count, "the separator is still trimmed");
        Assert.AreEqual(" (~123)", vm.TooltipPropertyList[1].ValueLabel, "the total shows what was counted");

        vm.TooltipPropertyList[1].RefreshValue();
        Assert.AreEqual(" (~123)", vm.TooltipPropertyList[1].ValueLabel, "and keeps showing it at the next refresh");

        HiddenHostsTooltipScope.Close(in token);
        vm.TooltipPropertyList[1].RefreshValue();
        Assert.AreEqual(" (0)", vm.TooltipPropertyList[1].ValueLabel, "outside every frame the engine's own number comes back");
    }

    [TestMethod]
    public void Remembered_ThePrisonersSection_StaysALiveMask()
    {
        Apply(typeof(PartyBaseHelper_GetPartySizeText_HiddenHosts_Patch));
        Apply(typeof(TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch));
        OpenRememberedFrame();
        var vm = NewVm();

        AddPartyTroopProperties().Invoke(null, new object?[] { vm, TroopRoster.CreateDummyTroopRoster(), new TextObject("{=!}Prisoners"), true, null });

        Assert.AreEqual(" (?)", vm.TooltipPropertyList[1].ValueLabel, "only the troops total is remembered");
    }

    private static PropertyBasedTooltipVM AsPeriodic(PropertyBasedTooltipVM vm, Type invokedType, object subject)
    {
        var baseType = typeof(TooltipBaseVM);
        baseType.GetField("_invokedType", AnyInstance)!.SetValue(vm, invokedType);
        baseType.GetField("_invokedArgs", AnyInstance)!.SetValue(vm, new[] { subject, false, true });
        return vm;
    }

    [TestMethod]
    public void Remembered_TheTotalSurvivesTheTwoSecondRefresh_AndSharpensWhenThePlayerClosesIn()
    {
        Apply(typeof(PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch));
        Apply(typeof(PartyBaseHelper_GetPartySizeText_HiddenHosts_Patch));
        Apply(typeof(TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch));
        var party = Bare<MobileParty>();

        // The player counted the host up close, then it was left behind: the next tooltip is built far away.
        _adapter.Read(Arg.Any<MobileParty>()).Returns(Snapshot(distance: 10f));
        HiddenHostsTooltipFrames.Open(party, "test");
        HiddenHostsTooltipScope.Close(new ScopeToken(null));
        _adapter.Read(Arg.Any<MobileParty>()).Returns(Snapshot(distance: 70f));
        var token = HiddenHostsTooltipFrames.Open(party, "test");
        var vm = AsPeriodic(RunTroopBuilder(), typeof(MobileParty), party);
        HiddenHostsTooltipScope.Close(in token);
        Assert.AreEqual(" (~123)", vm.TooltipPropertyList[1].ValueLabel, "built far away: what was counted");

        RunPeriodicRefresh(vm);
        Assert.AreEqual(" (~123)", vm.TooltipPropertyList[1].ValueLabel, "two seconds later it is still the memory, not vanilla's number");

        _adapter.Read(Arg.Any<MobileParty>()).Returns(Snapshot(distance: 10f));
        RunPeriodicRefresh(vm);
        Assert.AreEqual(" (0)", vm.TooltipPropertyList[1].ValueLabel, "the player closed in: the engine's own number");
    }

    [TestMethod]
    public void Remembered_AMemoryThatAppearsWhileTheTooltipIsOpen_ShowsAtTheNextRefresh()
    {
        Apply(typeof(PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch));
        Apply(typeof(PartyBaseHelper_GetPartySizeText_HiddenHosts_Patch));
        Apply(typeof(TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch));
        var party = Bare<MobileParty>();

        // Built masked at 0.3 (two digits read, nothing remembered beyond them). The builder's dummy roster holds nobody, so the
        // live text is vanilla's " (0)"; what matters is that the total row is already the wrapped one.
        _adapter.Read(Arg.Any<MobileParty>()).Returns(Snapshot(distance: 30f));
        var token = HiddenHostsTooltipFrames.Open(party, "test");
        var vm = AsPeriodic(RunTroopBuilder(), typeof(MobileParty), party);
        HiddenHostsTooltipScope.Close(in token);
        Assert.AreEqual(" (0)", vm.TooltipPropertyList[1].ValueLabel);

        // The player rides away while it stays open: the plate now says "~12?", and the open tooltip must agree.
        _adapter.Read(Arg.Any<MobileParty>()).Returns(Snapshot(distance: 50f));
        RunPeriodicRefresh(vm);

        Assert.AreEqual(" (~12?)", vm.TooltipPropertyList[1].ValueLabel);
    }
}
