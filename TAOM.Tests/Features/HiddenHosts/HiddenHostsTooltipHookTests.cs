using System;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
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
/// #846 phase 2: every branch of the tooltip hooks, run against bare engine objects. A tooltip view model and the campaign
/// subjects cannot be constructed in a test host, but <c>FormatterServices.GetUninitializedObject</c> gives instances the
/// hooks only pass through to the faked adapter; the view model gets a real, empty row list. The bracket through real
/// Harmony is in <see cref="HiddenHostsTooltipHarmonyPairTests"/>.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HiddenHostsTooltipHookTests
{
    private static bool _gameLoaded;
    private IHiddenHostsSettings _settings = null!;
    private IPartyIntelAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private HiddenHostsService _service = null!;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        _settings = Substitute.For<IHiddenHostsSettings>();
        _settings.Enabled.Returns(true);
        _adapter = Substitute.For<IPartyIntelAdapter>();
        _logger = Substitute.For<IModLogger>();
        _service = new HiddenHostsService(_settings);
        HiddenHostsCalls.Initialize(_service, _adapter, _logger, () => "Defenders", () => "Troops", hours => "Counted " + hours + "h ago");
    }

    [TestCleanup]
    public void Cleanup()
    {
        while (HiddenHostsTooltipScope.Current != null)
            HiddenHostsTooltipScope.Close(new ScopeToken(null));
        HiddenHostsCalls.Initialize(null, null);
    }

    private static T Bare<T>() where T : class => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static PropertyBasedTooltipVM NewVm()
    {
        var vm = Bare<PropertyBasedTooltipVM>();
        vm.TooltipPropertyList = new MBBindingList<TooltipProperty>();
        return vm;
    }

    private static PartyIntelSnapshot Party(int count, float ratio, bool visible = true) => new()
    {
        HasData = true, StringId = "p1", HealthyCount = count, OwnHealthyCount = count, DistanceToMain = ratio * 100f,
        MainSeeingRange = 100f, SpottingRatio = 1f, IsVisible = visible,
    };

    private void SeesParty(PartyIntelSnapshot snapshot) => _adapter.Read(Arg.Any<MobileParty>()).Returns(snapshot);

    private static ScopeToken OpenFrame(TooltipSubjectKind kind, TooltipMode mode, int count = 123, float ratio = 0.3f, int latched = 0) =>
        HiddenHostsTooltipScope.Open(new TooltipFrame(kind, new TooltipVerdict(mode, ratio, 1f, latched, count)));

    // ---- HiddenHostsTooltipFrames.Open

    [TestMethod]
    public void Open_Party_FarSnapshot_OpensAMaskedPartyFrame()
    {
        SeesParty(Party(123, 0.7f));

        var token = HiddenHostsTooltipFrames.Open(Bare<MobileParty>(), "hook");

        Assert.IsTrue(token.IsSet);
        Assert.AreEqual(TooltipSubjectKind.Party, HiddenHostsTooltipScope.Current!.Kind);
        Assert.AreEqual(TooltipMode.Masked, HiddenHostsTooltipScope.Current.Verdict.Mode);
        Assert.AreEqual(123, HiddenHostsTooltipScope.Current.Verdict.SubjectCount);
    }

    [TestMethod]
    public void Open_Army_IsJudgedOnItsLeader()
    {
        SeesParty(Party(300, 0.95f));

        HiddenHostsTooltipFrames.Open(Bare<Army>(), "hook");

        Assert.AreEqual(TooltipSubjectKind.Army, HiddenHostsTooltipScope.Current!.Kind);
        Assert.AreEqual(TooltipMode.Horizon, HiddenHostsTooltipScope.Current.Verdict.Mode);
        _adapter.Received(1).Read(Arg.Any<MobileParty>());
    }

    [TestMethod]
    public void Open_Settlement_ReadsTheSettlementSnapshot()
    {
        _adapter.ReadSettlement(Arg.Any<Settlement>()).Returns(new SettlementIntelSnapshot
        {
            HasData = true, StringId = "town_1", DefenderCount = 245, DistanceToMain = 70f, MainSeeingRange = 100f,
        });

        HiddenHostsTooltipFrames.Open(Bare<Settlement>(), "hook");

        Assert.AreEqual(TooltipSubjectKind.Settlement, HiddenHostsTooltipScope.Current!.Kind);
        Assert.AreEqual(245, HiddenHostsTooltipScope.Current.Verdict.SubjectCount);
    }

    [TestMethod]
    public void Open_MapEvent_ReadsTheEventSnapshot()
    {
        _adapter.ReadMapEvent(Arg.Any<MapEvent>()).Returns(new EventIntelSnapshot
        {
            HasData = true, TotalHealthy = 500, DistanceToMain = 70f, MainSeeingRange = 100f,
        });

        HiddenHostsTooltipFrames.Open(Bare<MapEvent>(), "hook");

        Assert.AreEqual(TooltipSubjectKind.MapEvent, HiddenHostsTooltipScope.Current!.Kind);
        Assert.AreEqual(500, HiddenHostsTooltipScope.Current.Verdict.SubjectCount);
    }

    [TestMethod]
    public void Open_SiegeEvent_ReadsTheSiegeSnapshot()
    {
        _adapter.ReadSiegeEvent(Arg.Any<SiegeEvent>()).Returns(new EventIntelSnapshot
        {
            HasData = true, TotalHealthy = 800, DistanceToMain = 70f, MainSeeingRange = 100f,
        });

        HiddenHostsTooltipFrames.Open(Bare<SiegeEvent>(), "hook");

        Assert.AreEqual(TooltipSubjectKind.SiegeEvent, HiddenHostsTooltipScope.Current!.Kind);
        Assert.AreEqual(800, HiddenHostsTooltipScope.Current.Verdict.SubjectCount);
    }

    [TestMethod]
    public void Open_ExemptSubject_StillPushesAVanillaFrame_SoAnOuterFrameIsNeverSeenThroughIt()
    {
        var outer = OpenFrame(TooltipSubjectKind.Settlement, TooltipMode.Masked);
        SeesParty(Party(123, 0.7f) with { IsMainParty = true });

        var inner = HiddenHostsTooltipFrames.Open(Bare<MobileParty>(), "hook");

        Assert.IsTrue(inner.IsSet);
        Assert.AreEqual(TooltipMode.Vanilla, HiddenHostsTooltipScope.Current!.Verdict.Mode);
        HiddenHostsTooltipScope.Close(in inner);
        Assert.AreEqual(TooltipSubjectKind.Settlement, HiddenHostsTooltipScope.Current!.Kind);
        HiddenHostsTooltipScope.Close(in outer);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("a string")]
    public void Open_AnUnknownSubject_OpensNothing(object? subject)
    {
        var token = HiddenHostsTooltipFrames.Open(subject, "hook");

        Assert.IsFalse(token.IsSet);
        Assert.IsNull(HiddenHostsTooltipScope.Current);
    }

    [TestMethod]
    public void Open_SettingOff_OpensNothing_AndNeverReadsTheAdapter()
    {
        _settings.Enabled.Returns(false);
        SeesParty(Party(123, 0.7f));

        var token = HiddenHostsTooltipFrames.Open(Bare<MobileParty>(), "hook");

        Assert.IsFalse(token.IsSet);
        Assert.IsNull(HiddenHostsTooltipScope.Current);
        _adapter.DidNotReceiveWithAnyArgs().Read(null);
    }

    [TestMethod]
    public void Open_TheSettingThrows_OpensNothing_AndTheFaultIsReported()
    {
        _settings.Enabled.Returns(_ => throw new InvalidOperationException("mcm gone"));

        var token = HiddenHostsTooltipFrames.Open(Bare<MobileParty>(), "hook");

        Assert.IsFalse(token.IsSet);
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Open_NoServiceOrAdapter_OpensNothing()
    {
        HiddenHostsCalls.Initialize(null, null);

        Assert.IsFalse(HiddenHostsTooltipFrames.Open(Bare<MobileParty>(), "hook").IsSet);
    }

    [TestMethod]
    public void Open_TheAdapterThrows_OpensNothing_AndLogsOnce()
    {
        _adapter.Read(Arg.Any<MobileParty>()).Throws(new InvalidOperationException("no campaign"));

        Assert.IsFalse(HiddenHostsTooltipFrames.Open(Bare<MobileParty>(), "hook").IsSet);
        Assert.IsFalse(HiddenHostsTooltipFrames.Open(Bare<MobileParty>(), "hook").IsSet);

        Assert.IsNull(HiddenHostsTooltipScope.Current);
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Open_AfterTheFaultBudgetIsSpent_OpensNothing()
    {
        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());
        SeesParty(Party(123, 0.7f));

        Assert.IsFalse(HiddenHostsTooltipFrames.Open(Bare<MobileParty>(), "hook").IsSet);
        _adapter.DidNotReceiveWithAnyArgs().Read(null);
    }

    [TestMethod]
    public void IsFrameType_IsTrueForTheFiveSubjectTypesOnly()
    {
        foreach (var type in new[] { typeof(MobileParty), typeof(Army), typeof(Settlement), typeof(MapEvent), typeof(SiegeEvent) })
            Assert.IsTrue(HiddenHostsTooltipFrames.IsFrameType(type), type.Name);

        Assert.IsFalse(HiddenHostsTooltipFrames.IsFrameType(typeof(Hero)));
        Assert.IsFalse(HiddenHostsTooltipFrames.IsFrameType(null));
    }

    // ---- T1 to T5: prefix, postfix, finalizer

    [TestMethod]
    public void Subject_Prefix_OpensTheSubjectsFrame_AndTheFinalizerClosesIt()
    {
        SeesParty(Party(123, 0.7f));

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Prefix(new object[] { Bare<MobileParty>(), false, true }, out var token);

        Assert.IsTrue(token.IsSet);
        Assert.IsNotNull(HiddenHostsTooltipScope.Current);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Finalizer(token);

        Assert.IsNull(HiddenHostsTooltipScope.Current);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void Subject_Prefix_NullOrEmptyArgs_OpensNothing(int length)
    {
        var args = length == 0 ? Array.Empty<object>() : new object[] { null! };

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Prefix(args, out var token);
        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Prefix(null!, out var token2);

        Assert.IsFalse(token.IsSet);
        Assert.IsFalse(token2.IsSet);
    }

    [TestMethod]
    public void Subject_Finalizer_UnsetToken_LeavesTheOuterFrame()
    {
        var outer = OpenFrame(TooltipSubjectKind.Party, TooltipMode.Masked);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Finalizer(default);

        Assert.IsNotNull(HiddenHostsTooltipScope.Current);
        HiddenHostsTooltipScope.Close(in outer);
    }

    [TestMethod]
    public void Subject_Finalizer_ClosesEvenWhenTheToggleIsOffAndTheBudgetIsSpent()
    {
        var token = OpenFrame(TooltipSubjectKind.Party, TooltipMode.Masked);
        _settings.Enabled.Returns(false);
        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Finalizer(token);

        Assert.IsNull(HiddenHostsTooltipScope.Current, "toggles gate I/O, never a state transition");
    }

    [TestMethod]
    public void Subject_Postfix_NoFrame_ChangesNothing()
    {
        var vm = NewVm();
        vm.AddProperty("", "Lord Aldric", 0, TooltipProperty.TooltipPropertyFlags.Title);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual("Lord Aldric", vm.TooltipPropertyList[0].ValueLabel);
    }

    [DataTestMethod]
    [DataRow((int)TooltipSubjectKind.Party)]
    [DataRow((int)TooltipSubjectKind.Army)]
    public void Subject_Postfix_AtTheHorizon_RenamesTheTitleRow(int subjectKind)
    {
        var kind = (TooltipSubjectKind)subjectKind;
        var vm = NewVm();
        vm.AddProperty("", "Lord Aldric", 0, TooltipProperty.TooltipPropertyFlags.Title);
        vm.AddProperty("Owner", " ");
        OpenFrame(kind, TooltipMode.Horizon);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual(HiddenHostsTexts.UnknownParty, vm.TooltipPropertyList[0].ValueLabel);
        Assert.AreEqual(" ", vm.TooltipPropertyList[1].ValueLabel, "only the title changes: the owner block stays");
    }

    [TestMethod]
    public void Subject_Postfix_RowZeroWithoutTheTitleFlag_IsLeftAlone()
    {
        var vm = NewVm();
        vm.AddProperty("", "Lord Aldric", 0);
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Horizon);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual("Lord Aldric", vm.TooltipPropertyList[0].ValueLabel);
    }

    [TestMethod]
    public void Subject_Postfix_EmptyList_DoesNothing()
    {
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Horizon);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(NewVm());
    }

    [DataTestMethod]
    [DataRow((int)TooltipMode.Vanilla)]
    [DataRow((int)TooltipMode.Masked)]
    public void Subject_Postfix_NotAtTheHorizon_KeepsTheName(int tooltipMode)
    {
        var mode = (TooltipMode)tooltipMode;
        var vm = NewVm();
        vm.AddProperty("", "Lord Aldric", 0, TooltipProperty.TooltipPropertyFlags.Title);
        OpenFrame(TooltipSubjectKind.Party, mode);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual("Lord Aldric", vm.TooltipPropertyList[0].ValueLabel);
    }

    [TestMethod]
    public void Subject_Postfix_BattleAtTheHorizon_KeepsItsTitle()
    {
        var vm = NewVm();
        vm.AddProperty("", "Field Battle", 0, TooltipProperty.TooltipPropertyFlags.Title);
        OpenFrame(TooltipSubjectKind.MapEvent, TooltipMode.Horizon);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual("Field Battle", vm.TooltipPropertyList[0].ValueLabel);
    }

    [TestMethod]
    public void Subject_Postfix_Settlement_Masked_RewritesTheDefendersRow()
    {
        var vm = NewVm();
        vm.AddProperty("Owner", " ");
        vm.AddProperty("Defenders", "245+12w");
        vm.AddProperty("Lord Aldric", "120");
        OpenFrame(TooltipSubjectKind.Settlement, TooltipMode.Masked, count: 245, ratio: 0.3f, latched: 2);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual("24?", vm.TooltipPropertyList[1].ValueLabel);
        Assert.AreEqual("120", vm.TooltipPropertyList[2].ValueLabel, "party rows are the number helper's, not the postfix's");
    }

    [TestMethod]
    public void Subject_Postfix_Settlement_Vanilla_KeepsTheDefendersRow()
    {
        var vm = NewVm();
        vm.AddProperty("Defenders", "245+12w");
        OpenFrame(TooltipSubjectKind.Settlement, TooltipMode.Vanilla);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual("245+12w", vm.TooltipPropertyList[0].ValueLabel);
    }

    [TestMethod]
    public void Subject_Postfix_Settlement_NoDefendersRow_ChangesNothing()
    {
        var vm = NewVm();
        vm.AddProperty("Parties", "?");
        OpenFrame(TooltipSubjectKind.Settlement, TooltipMode.Masked, count: 245);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual("?", vm.TooltipPropertyList[0].ValueLabel);
    }

    [TestMethod]
    public void Subject_Postfix_Settlement_NoDefendersRow_WithAMemory_AddsNoReasonRow_AndDrawsNothing()
    {
        var vm = NewVm();
        vm.AddProperty("Parties", "?");
        OpenMemoryFrame(TooltipSubjectKind.Settlement, memory: "~245", hours: 12, shown: false);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual(1, vm.TooltipPropertyList.Count, "an Alt view or a hideout has no defenders row: no 'Counted' row either");
        Assert.AreEqual("?", vm.TooltipPropertyList[0].ValueLabel);
        Assert.IsFalse(_service.MemoryDrawn, "nothing with a tilde reached the screen");
        Assert.IsFalse(HiddenHostsTooltipScope.Current!.ShowedMemory);
    }

    [TestMethod]
    public void Subject_Postfix_AfterTheFaultBudgetIsSpent_ChangesNothing()
    {
        var vm = NewVm();
        vm.AddProperty("", "Lord Aldric", 0, TooltipProperty.TooltipPropertyFlags.Title);
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Horizon);
        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual("Lord Aldric", vm.TooltipPropertyList[0].ValueLabel);
    }

    [TestMethod]
    public void Subject_Postfix_TheLabelSeamThrows_ChangesNothing_AndLogsOnce()
    {
        HiddenHostsCalls.Initialize(_service, _adapter, _logger, () => throw new InvalidOperationException("no texts"));
        var vm = NewVm();
        vm.AddProperty("Defenders", "245");
        OpenFrame(TooltipSubjectKind.Settlement, TooltipMode.Masked, count: 245);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);
        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual("245", vm.TooltipPropertyList[0].ValueLabel);
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    // ---- T6: OnPeriodicRefresh

    private static PropertyBasedTooltipVM NewPeriodicVm(Type? invokedType, object[]? invokedArgs)
    {
        var vm = NewVm();
        var baseType = typeof(TooltipBaseVM);
        baseType.GetField("_invokedType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(vm, invokedType);
        baseType.GetField("_invokedArgs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(vm, invokedArgs);
        return vm;
    }

    [TestMethod]
    public void Periodic_Prefix_OpensTheFrameOfTheInvokedSubject_AndTheFinalizerCloses()
    {
        SeesParty(Party(123, 0.7f));
        var vm = NewPeriodicVm(typeof(MobileParty), new object[] { Bare<MobileParty>(), false, true });

        PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch.Prefix(vm, out var token);

        Assert.IsTrue(token.IsSet);
        Assert.AreEqual(TooltipSubjectKind.Party, HiddenHostsTooltipScope.Current!.Kind);

        PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch.Finalizer(token);

        Assert.IsNull(HiddenHostsTooltipScope.Current);
    }

    [TestMethod]
    public void Periodic_Prefix_AnUnrelatedTooltipType_OpensNothing()
    {
        SeesParty(Party(123, 0.7f));
        var vm = NewPeriodicVm(typeof(Hero), new object[] { Bare<MobileParty>() });

        PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch.Prefix(vm, out var token);

        Assert.IsFalse(token.IsSet);
        Assert.IsNull(HiddenHostsTooltipScope.Current);
    }

    [TestMethod]
    public void Periodic_Prefix_NoInvokedArgs_OpensNothing()
    {
        Assert.IsFalse(Open(NewPeriodicVm(typeof(MobileParty), null)).IsSet);
        Assert.IsFalse(Open(NewPeriodicVm(typeof(MobileParty), Array.Empty<object>())).IsSet);
        Assert.IsFalse(Open(NewPeriodicVm(null, new object[] { Bare<MobileParty>() })).IsSet);
        Assert.IsFalse(Open(null!).IsSet);

        static ScopeToken Open(PropertyBasedTooltipVM vm)
        {
            PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch.Prefix(vm, out var token);
            return token;
        }
    }

    [TestMethod]
    public void Periodic_Finalizer_UnsetToken_LeavesTheOuterFrame()
    {
        var outer = OpenFrame(TooltipSubjectKind.Party, TooltipMode.Masked);

        PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch.Finalizer(default);

        Assert.IsNotNull(HiddenHostsTooltipScope.Current);
        HiddenHostsTooltipScope.Close(in outer);
    }

    // ---- T7: AddPartyTroopProperties

    private static void AddRows(PropertyBasedTooltipVM vm, int count)
    {
        for (int i = 0; i < count; i++) vm.AddProperty("row" + i, "v");
    }

    [TestMethod]
    public void Troops_Masked_PrefixRecordsTheStart_PostfixKeepsTheSpacerAndTitle_AndTrimsTheRest()
    {
        var vm = NewVm();
        AddRows(vm, 3);
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Masked);

        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Prefix(vm, out var state);
        AddRows(vm, 6);
        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Postfix(vm, TroopsTitle, state);

        Assert.IsTrue(state.IsSet);
        Assert.AreEqual(3, state.Start);
        Assert.AreEqual(5, vm.TooltipPropertyList.Count, "the three earlier rows, this call's spacer and its title");
        Assert.AreEqual("row0", vm.TooltipPropertyList[0].DefinitionLabel);
        Assert.AreEqual("row1", vm.TooltipPropertyList[4].DefinitionLabel);
    }

    [TestMethod]
    public void Troops_Horizon_TrimsToo()
    {
        var vm = NewVm();
        OpenFrame(TooltipSubjectKind.Army, TooltipMode.Horizon);

        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Prefix(vm, out var state);
        AddRows(vm, 6);
        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Postfix(vm, TroopsTitle, state);

        Assert.AreEqual(2, vm.TooltipPropertyList.Count);
    }

    [TestMethod]
    public void Troops_ACallThatAddedOnlyASpacerAndATitle_LosesNothing()
    {
        var vm = NewVm();
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Masked);

        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Prefix(vm, out var state);
        AddRows(vm, 2);
        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Postfix(vm, TroopsTitle, state);

        Assert.AreEqual(2, vm.TooltipPropertyList.Count);
    }

    [TestMethod]
    public void Troops_VanillaFrame_RecordsNothing_AndTrimsNothing()
    {
        var vm = NewVm();
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Vanilla);

        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Prefix(vm, out var state);
        AddRows(vm, 6);
        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Postfix(vm, TroopsTitle, state);

        Assert.IsFalse(state.IsSet);
        Assert.AreEqual(6, vm.TooltipPropertyList.Count);
    }

    [TestMethod]
    public void Troops_NoFrame_RecordsNothing()
    {
        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Prefix(NewVm(), out var state);

        Assert.IsFalse(state.IsSet);
    }

    [TestMethod]
    public void Troops_PrefixStripped_PostfixTrimsNothing()
    {
        var vm = NewVm();
        AddRows(vm, 6);
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Masked);

        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Postfix(vm, TroopsTitle, default);

        Assert.AreEqual(6, vm.TooltipPropertyList.Count, "a default token must not delete the tooltip from row 2 on");
    }

    [TestMethod]
    public void Troops_AfterTheFaultBudgetIsSpent_RecordsNothing()
    {
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Masked);
        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Prefix(NewVm(), out var state);

        Assert.IsFalse(state.IsSet);
    }

    [TestMethod]
    public void Troops_NullViewModel_RecordsNothing()
    {
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Masked);

        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Prefix(null!, out var state);
        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Postfix(null!, TroopsTitle, new TrimToken(0));

        Assert.IsFalse(state.IsSet);
    }

    // ---- T8, T9: AddEncounterParties

    [DataTestMethod]
    [DataRow((int)TooltipMode.Masked)]
    [DataRow((int)TooltipMode.Horizon)]
    public void Encounter_Masked_TurnsThePerTroopRowsOff(int tooltipMode)
    {
        var mode = (TooltipMode)tooltipMode;
        OpenFrame(TooltipSubjectKind.MapEvent, mode);
        bool isExtended = true;

        TooltipRefresherCollection_AddEncounterParties_HiddenHosts_Patch.Prefix(ref isExtended);

        Assert.IsFalse(isExtended);
    }

    [TestMethod]
    public void Encounter_VanillaFrame_LeavesIsExtended()
    {
        OpenFrame(TooltipSubjectKind.MapEvent, TooltipMode.Vanilla);
        bool isExtended = true;

        TooltipRefresherCollection_AddEncounterParties_HiddenHosts_Patch.Prefix(ref isExtended);

        Assert.IsTrue(isExtended);
    }

    [TestMethod]
    public void Encounter_NoFrame_LeavesIsExtended()
    {
        bool isExtended = true;

        TooltipRefresherCollection_AddEncounterParties_HiddenHosts_Patch.Prefix(ref isExtended);

        Assert.IsTrue(isExtended);
    }

    [TestMethod]
    public void Encounter_NotExtended_StaysNotExtended()
    {
        OpenFrame(TooltipSubjectKind.MapEvent, TooltipMode.Masked);
        bool isExtended = false;

        TooltipRefresherCollection_AddEncounterParties_HiddenHosts_Patch.Prefix(ref isExtended);

        Assert.IsFalse(isExtended);
    }

    [TestMethod]
    public void Encounter_AfterTheFaultBudgetIsSpent_LeavesIsExtended()
    {
        OpenFrame(TooltipSubjectKind.MapEvent, TooltipMode.Masked);
        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());
        bool isExtended = true;

        TooltipRefresherCollection_AddEncounterParties_HiddenHosts_Patch.Prefix(ref isExtended);

        Assert.IsTrue(isExtended);
    }

    // ---- T10: GetPartySizeText

    private static string Run(int healthy, int wounded, bool isInspected, out TextObject original)
    {
        original = new TextObject("{=!}vanilla");
        var result = original;
        PartyBaseHelper_GetPartySizeText_HiddenHosts_Patch.Postfix(healthy, wounded, isInspected, ref result);
        return result.ToString();
    }

    [TestMethod]
    public void Size_NoFrame_KeepsVanillasText() => Assert.AreEqual("vanilla", Run(123, 0, true, out _));

    [TestMethod]
    public void Size_VanillaFrame_KeepsVanillasText()
    {
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Vanilla);

        Assert.AreEqual("vanilla", Run(123, 0, true, out _));
    }

    [TestMethod]
    public void Size_VanillasOwnFog_IsLeftAlone()
    {
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Masked, ratio: 0.7f);

        Assert.AreEqual("vanilla", Run(123, 0, isInspected: false, out _), "vanilla already printed ??? for this call");
    }

    [TestMethod]
    public void Size_Masked_PrintsTheKnownDigits_WithoutTheWoundedPart()
    {
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Masked, count: 123, ratio: 0.3f, latched: 2);

        Assert.AreEqual("12?", Run(123, 17, true, out _));
    }

    [TestMethod]
    public void Size_MaskedButThisNumberIsReadInFull_KeepsVanillasText()
    {
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Masked, count: 123, ratio: 0.2f, latched: 2);

        Assert.AreEqual("vanilla", Run(5, 1, true, out _));
    }

    [TestMethod]
    public void Size_Horizon_PrintsThreeDots()
    {
        OpenFrame(TooltipSubjectKind.Army, TooltipMode.Horizon);

        Assert.AreEqual("...", Run(123, 0, true, out _));
    }

    [TestMethod]
    public void Size_AfterTheFaultBudgetIsSpent_KeepsVanillasText()
    {
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Horizon);
        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        Assert.AreEqual("vanilla", Run(123, 0, true, out _));
    }

    [TestMethod]
    public void Size_NoService_KeepsVanillasText()
    {
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Horizon);
        HiddenHostsCalls.Initialize(null, null);

        Assert.AreEqual("vanilla", Run(123, 0, true, out _));
    }

    // ---- T11: GetPartyNameplateText

    private static string Row(MobileParty party, string vanilla, bool includeAttachedParties = false)
    {
        var result = vanilla;
        CampaignUIHelper_GetPartyNameplateText_HiddenHosts_Patch.Postfix(party, includeAttachedParties, ref result);
        return result;
    }

    [TestMethod]
    public void Row_NoFrame_KeepsVanillasText() => Assert.AreEqual("150", Row(Bare<MobileParty>(), "150"));

    [TestMethod]
    public void Row_NotASettlementFrame_KeepsVanillasText()
    {
        SeesParty(Party(150, 0f));
        OpenFrame(TooltipSubjectKind.Party, TooltipMode.Masked, ratio: 0.3f);

        Assert.AreEqual("150", Row(Bare<MobileParty>(), "150"), "the army menu overlay and the party tooltip stay exact");
    }

    [TestMethod]
    public void Row_SettlementFrame_MasksThePartysCountByTheSettlementsDistance()
    {
        SeesParty(Party(150, 9f));
        OpenFrame(TooltipSubjectKind.Settlement, TooltipMode.Masked, ratio: 0.3f);

        Assert.AreEqual("15?", Row(Bare<MobileParty>(), "150"));
    }

    [TestMethod]
    public void Row_SettlementFrame_ArmyLeader_MasksTheOwnRosterVanillaPrinted_NotTheArmyTotal()
    {
        SeesParty(Party(345, 9f) with { OwnHealthyCount = 45 });
        OpenFrame(TooltipSubjectKind.Settlement, TooltipMode.Masked, ratio: 0.4f);

        Assert.AreEqual("4?", Row(Bare<MobileParty>(), "45"), "the settlement tooltip asks for the party without its attached parties");
        Assert.AreEqual("3??", Row(Bare<MobileParty>(), "345", includeAttachedParties: true));
    }

    [TestMethod]
    public void Row_SettlementFrame_VanillaVerdict_KeepsVanillasText()
    {
        SeesParty(Party(150, 9f));
        OpenFrame(TooltipSubjectKind.Settlement, TooltipMode.Vanilla);

        Assert.AreEqual("150", Row(Bare<MobileParty>(), "150"));
    }

    [TestMethod]
    public void Row_SettlementFrame_AnExemptParty_KeepsVanillasText()
    {
        SeesParty(Party(150, 9f) with { InPlayerClan = true });
        OpenFrame(TooltipSubjectKind.Settlement, TooltipMode.Masked, ratio: 0.3f);

        Assert.AreEqual("150", Row(Bare<MobileParty>(), "150"));
    }

    [TestMethod]
    public void Row_SettlementFrame_TheAdapterThrows_KeepsVanillasText_AndLogsOnce()
    {
        _adapter.Read(Arg.Any<MobileParty>()).Throws(new InvalidOperationException("no campaign"));
        OpenFrame(TooltipSubjectKind.Settlement, TooltipMode.Masked, ratio: 0.3f);

        Assert.AreEqual("150", Row(Bare<MobileParty>(), "150"));
        Assert.AreEqual("150", Row(Bare<MobileParty>(), "150"));
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Row_AfterTheFaultBudgetIsSpent_KeepsVanillasText()
    {
        SeesParty(Party(150, 9f));
        OpenFrame(TooltipSubjectKind.Settlement, TooltipMode.Masked, ratio: 0.3f);
        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        Assert.AreEqual("150", Row(Bare<MobileParty>(), "150"));
    }

    [TestMethod]
    public void Row_NullPartyOrNoServices_KeepsVanillasText()
    {
        OpenFrame(TooltipSubjectKind.Settlement, TooltipMode.Masked, ratio: 0.3f);
        Assert.AreEqual("150", Row(null!, "150"));

        HiddenHostsCalls.Initialize(null, null);
        Assert.AreEqual("150", Row(Bare<MobileParty>(), "150"));
    }

    // ---- Phase 3: the remembered count in the troop total, the reason row, the army frame

    private static TextObject TroopsTitle => new("{=!}Troops");

    private static ScopeToken OpenMemoryFrame(TooltipSubjectKind kind, TooltipMode mode = TooltipMode.Masked, string? memory = "~123", int hours = 5,
        bool shown = true) =>
        HiddenHostsTooltipScope.Open(new TooltipFrame(kind, new TooltipVerdict(mode, 0.7f, 1f, 0, 123, memory, hours)) { ShowedMemory = shown });

    private static string? _originalSaw;

    /// <summary>Runs the T7 pair around a builder that adds a spacer, a title row whose function records <c>InTotal</c>, and a separator.</summary>
    private static PropertyBasedTooltipVM BuildTroopSection(TextObject title, int earlierRows = 0)
    {
        var vm = NewVm();
        AddRows(vm, earlierRows);
        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Prefix(vm, out var state);
        vm.AddProperty(string.Empty, string.Empty, -1);
        vm.TooltipPropertyList.Add(new TooltipProperty("Troops", () =>
        {
            _originalSaw = HiddenHostsTooltipScope.Current?.InTotal.ToString() ?? "no frame";
            return " (original)";
        }, 7, true, TooltipProperty.TooltipPropertyFlags.Cost) { OnlyShowWhenNotExtended = true });
        vm.AddProperty("", "", 0, TooltipProperty.TooltipPropertyFlags.RundownSeperator);
        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Postfix(vm, title, state);
        return vm;
    }

    [DataTestMethod]
    [DataRow((int)TooltipSubjectKind.Party)]
    [DataRow((int)TooltipSubjectKind.Army)]
    public void Wrap_AMemoryFrame_TheTroopsTitle_ReplacesTheTotalRow_WhoseFunctionRunsInTotal(int subjectKind)
    {
        _originalSaw = null;
        OpenMemoryFrame((TooltipSubjectKind)subjectKind);

        var vm = BuildTroopSection(TroopsTitle);

        Assert.AreEqual(2, vm.TooltipPropertyList.Count, "the spacer and the title: the separator is still trimmed");
        var row = vm.TooltipPropertyList[1];
        Assert.AreEqual("True", _originalSaw, "the wrapper evaluated the original while the frame was in its total");
        Assert.AreEqual(" (original)", row.ValueLabel);
        Assert.IsFalse(HiddenHostsTooltipScope.Current!.InTotal, "and left the flag off");
    }

    [TestMethod]
    public void Wrap_KeepsTheRowsLabelHeightFlagsAndExtendedSettings()
    {
        OpenMemoryFrame(TooltipSubjectKind.Party);

        var row = BuildTroopSection(TroopsTitle).TooltipPropertyList[1];

        Assert.AreEqual("Troops", row.DefinitionLabel);
        Assert.AreEqual(7, row.TextHeight);
        Assert.AreEqual((int)TooltipProperty.TooltipPropertyFlags.Cost, row.PropertyModifier);
        Assert.IsTrue(row.OnlyShowWhenExtended);
        Assert.IsTrue(row.OnlyShowWhenNotExtended);
    }

    [TestMethod]
    public void Wrap_EachRefresh_RunsTheOriginalAgain_InTheFrameThatIsCurrentThen()
    {
        OpenMemoryFrame(TooltipSubjectKind.Party);
        var row = BuildTroopSection(TroopsTitle).TooltipPropertyList[1];
        _originalSaw = null;

        row.RefreshValue();

        Assert.AreEqual("True", _originalSaw);
        Assert.IsFalse(HiddenHostsTooltipScope.Current!.InTotal);
    }

    [TestMethod]
    public void Wrap_NoFrameAtRefresh_TheOriginalRunsAsVanilla_WithoutThrowing()
    {
        var token = OpenMemoryFrame(TooltipSubjectKind.Party);
        var row = BuildTroopSection(TroopsTitle).TooltipPropertyList[1];
        HiddenHostsTooltipScope.Close(in token);
        _originalSaw = null;

        row.RefreshValue();

        Assert.AreEqual("no frame", _originalSaw);
        Assert.AreEqual(" (original)", row.ValueLabel);
    }

    [TestMethod]
    public void Wrap_TheOriginalThrows_TheFlagIsStillCleared()
    {
        OpenMemoryFrame(TooltipSubjectKind.Party);
        var vm = NewVm();
        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Prefix(vm, out var state);
        vm.AddProperty(string.Empty, string.Empty, -1);
        bool throwNow = false;
        vm.AddProperty("Troops", () => throwNow ? throw new InvalidOperationException("roster gone") : "ok");
        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Postfix(vm, TroopsTitle, state);
        var frame = HiddenHostsTooltipScope.Current!;
        throwNow = true;

        Assert.ThrowsException<InvalidOperationException>(() => vm.TooltipPropertyList[1].RefreshValue());

        Assert.IsFalse(frame.InTotal);
    }

    [TestMethod]
    public void Wrap_ThePrisonersTitle_IsNotWrapped()
    {
        OpenMemoryFrame(TooltipSubjectKind.Party);
        _originalSaw = null;

        var vm = BuildTroopSection(new TextObject("{=!}Prisoners"));

        Assert.AreEqual("False", _originalSaw, "the original ran in the frame but never in a total");
        Assert.AreEqual(" (original)", vm.TooltipPropertyList[1].ValueLabel);
        vm.TooltipPropertyList[1].RefreshValue();
        Assert.AreEqual("False", _originalSaw);
    }

    [DataTestMethod]
    [DataRow((int)TooltipSubjectKind.Settlement, (int)TooltipMode.Masked, true)]
    [DataRow((int)TooltipSubjectKind.MapEvent, (int)TooltipMode.Masked, true)]
    [DataRow((int)TooltipSubjectKind.Party, (int)TooltipMode.Horizon, true)]
    [DataRow((int)TooltipSubjectKind.Party, (int)TooltipMode.Vanilla, true)]
    public void Wrap_NotForThisFrame_LeavesTheOriginalRow(int subjectKind, int tooltipMode, bool hasMemory)
    {
        OpenMemoryFrame((TooltipSubjectKind)subjectKind, (TooltipMode)tooltipMode, hasMemory ? "~123" : null);
        _originalSaw = null;

        var vm = BuildTroopSection(TroopsTitle);
        vm.TooltipPropertyList[1].RefreshValue();

        Assert.AreEqual("False", _originalSaw);
    }

    [DataTestMethod]
    [DataRow((int)TooltipSubjectKind.Party)]
    [DataRow((int)TooltipSubjectKind.Army)]
    public void Wrap_AMaskedFrameWithNoMemoryYet_StillWraps_SoALaterMemoryShowsAtTheNextRefresh(int subjectKind)
    {
        _originalSaw = null;
        OpenMemoryFrame((TooltipSubjectKind)subjectKind, memory: null);

        var vm = BuildTroopSection(TroopsTitle);

        Assert.AreEqual("True", _originalSaw, "the original ran inside the wrapper although nothing is remembered yet");
        Assert.AreEqual(2, vm.TooltipPropertyList.Count);
    }

    [TestMethod]
    public void Wrap_ThePrefixStripped_OrTheBudgetSpent_LeavesTheOriginalRow()
    {
        OpenMemoryFrame(TooltipSubjectKind.Party);
        var vm = NewVm();
        vm.AddProperty(string.Empty, string.Empty, -1);
        bool wrapped = false;
        vm.AddProperty("Troops", () =>
        {
            wrapped |= HiddenHostsTooltipScope.Current!.InTotal;
            return "x";
        });

        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Postfix(vm, TroopsTitle, default);
        vm.TooltipPropertyList[1].RefreshValue();

        Assert.IsFalse(wrapped);
    }

    [TestMethod]
    public void Wrap_TheTitleLabelSeamThrows_KeepsTheTooltip_AndLogsOnce()
    {
        HiddenHostsCalls.Initialize(_service, _adapter, _logger, () => "Defenders", () => throw new InvalidOperationException("no text"));
        OpenMemoryFrame(TooltipSubjectKind.Party);

        BuildTroopSection(TroopsTitle);
        BuildTroopSection(TroopsTitle);

        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Wrap_ARowListShorterThanTheSectionAdds_IsLeftAlone()
    {
        OpenMemoryFrame(TooltipSubjectKind.Party);
        var vm = NewVm();
        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Prefix(vm, out var state);
        vm.AddProperty(string.Empty, string.Empty, -1);

        TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.Postfix(vm, TroopsTitle, state);

        Assert.AreEqual(1, vm.TooltipPropertyList.Count);
    }

    // ---- T10 with the total flag

    [TestMethod]
    public void Size_InTheTotal_WithAMemory_PrintsTheRememberedCount()
    {
        OpenMemoryFrame(TooltipSubjectKind.Party);
        HiddenHostsTooltipScope.Current!.InTotal = true;

        Assert.AreEqual("~123", Run(123, 17, true, out _));
    }

    [TestMethod]
    public void Size_InTheTotal_PrintingTheMemory_MarksTheFrame_AndPrintingTheMaskDoesNot()
    {
        OpenMemoryFrame(TooltipSubjectKind.Party, shown: false);
        HiddenHostsTooltipScope.Current!.InTotal = true;
        Run(123, 0, true, out _);
        Assert.IsTrue(HiddenHostsTooltipScope.Current.ShowedMemory);

        OpenMemoryFrame(TooltipSubjectKind.Party, memory: null, shown: false);
        HiddenHostsTooltipScope.Current!.InTotal = true;
        Run(123, 0, true, out _);
        Assert.IsFalse(HiddenHostsTooltipScope.Current.ShowedMemory);
    }

    [TestMethod]
    public void Size_NotInTheTotal_WithAMemory_StaysTheLiveMask()
    {
        OpenMemoryFrame(TooltipSubjectKind.Party);

        Assert.AreEqual("???", Run(123, 0, true, out _));
    }

    [TestMethod]
    public void Size_InTheTotal_WithoutAMemory_IsTheLiveMask()
    {
        OpenMemoryFrame(TooltipSubjectKind.Party, memory: null);
        HiddenHostsTooltipScope.Current!.InTotal = true;

        Assert.AreEqual("???", Run(123, 0, true, out _));
    }

    [TestMethod]
    public void Size_InTheTotal_VanillasOwnFog_WinsOverTheMemory()
    {
        OpenMemoryFrame(TooltipSubjectKind.Party);
        HiddenHostsTooltipScope.Current!.InTotal = true;

        Assert.AreEqual("vanilla", Run(123, 0, isInspected: false, out _), "never more than vanilla shows");
    }

    // ---- The reason row

    private static PropertyBasedTooltipVM TitledVm()
    {
        var vm = NewVm();
        vm.AddProperty("", "Lord Aldric", 0, TooltipProperty.TooltipPropertyFlags.Title);
        return vm;
    }

    [DataTestMethod]
    [DataRow((int)TooltipSubjectKind.Party)]
    [DataRow((int)TooltipSubjectKind.Army)]
    [DataRow((int)TooltipSubjectKind.Settlement)]
    public void Reason_AMemoryVerdict_AppendsOneRowThatNamesTheAge(int subjectKind)
    {
        var vm = TitledVm();
        OpenMemoryFrame((TooltipSubjectKind)subjectKind, hours: 5);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual(2, vm.TooltipPropertyList.Count);
        Assert.AreEqual(string.Empty, vm.TooltipPropertyList[1].DefinitionLabel);
        Assert.AreEqual("Counted 5h ago", vm.TooltipPropertyList[1].ValueLabel);
    }

    [TestMethod]
    public void Reason_EachRefreshReadsTheFrameThatIsCurrentThen_AndGoesBlankWithoutAMemory()
    {
        var vm = TitledVm();
        var first = OpenMemoryFrame(TooltipSubjectKind.Party, hours: 5);
        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);
        var row = vm.TooltipPropertyList[1];
        HiddenHostsTooltipScope.Close(in first);

        var older = OpenMemoryFrame(TooltipSubjectKind.Party, hours: 30);
        row.RefreshValue();
        Assert.AreEqual("Counted 30h ago", row.ValueLabel);
        HiddenHostsTooltipScope.Close(in older);

        var live = OpenMemoryFrame(TooltipSubjectKind.Party, memory: null);
        row.RefreshValue();
        Assert.AreEqual(string.Empty, row.ValueLabel, "the player closed in: nothing is remembered any more");
        HiddenHostsTooltipScope.Close(in live);

        row.RefreshValue();
        Assert.AreEqual(string.Empty, row.ValueLabel, "no frame at all");
    }

    [TestMethod]
    public void Reason_AMemoryThatNeverReachedTheScreen_AddsNoRow()
    {
        var vm = TitledVm();
        OpenMemoryFrame(TooltipSubjectKind.Party, shown: false);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual(1, vm.TooltipPropertyList.Count);
    }

    [DataTestMethod]
    [DataRow((int)TooltipSubjectKind.Party, (int)TooltipMode.Horizon, true)]
    [DataRow((int)TooltipSubjectKind.Party, (int)TooltipMode.Vanilla, true)]
    [DataRow((int)TooltipSubjectKind.Party, (int)TooltipMode.Masked, false)]
    [DataRow((int)TooltipSubjectKind.MapEvent, (int)TooltipMode.Masked, true)]
    [DataRow((int)TooltipSubjectKind.SiegeEvent, (int)TooltipMode.Masked, true)]
    public void Reason_NoRow_WhenNothingIsRemembered_OrForABattle(int subjectKind, int tooltipMode, bool hasMemory)
    {
        var vm = TitledVm();
        OpenMemoryFrame((TooltipSubjectKind)subjectKind, (TooltipMode)tooltipMode, hasMemory ? "~123" : null);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual(1, vm.TooltipPropertyList.Count);
    }

    [TestMethod]
    public void Reason_TheTextSeamThrows_TheRowIsBlank_AndLogsOnce()
    {
        HiddenHostsCalls.Initialize(_service, _adapter, _logger, () => "Defenders", null, hours => throw new InvalidOperationException("no text"));
        var vm = TitledVm();
        OpenMemoryFrame(TooltipSubjectKind.Party);
        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        vm.TooltipPropertyList[1].RefreshValue();
        vm.TooltipPropertyList[1].RefreshValue();

        Assert.AreEqual(string.Empty, vm.TooltipPropertyList[1].ValueLabel);
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Reason_AfterTheFaultBudgetIsSpent_NoRowIsAdded()
    {
        var vm = TitledVm();
        OpenMemoryFrame(TooltipSubjectKind.Party);
        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            HiddenHostsCalls.ReportFault("hook", new InvalidOperationException());

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual(1, vm.TooltipPropertyList.Count);
    }

    [TestMethod]
    public void Settlement_AMemoryVerdict_ShowsTheRememberedDefenders_AndTheReasonRow()
    {
        var vm = NewVm();
        vm.AddProperty("Defenders", "245");
        OpenMemoryFrame(TooltipSubjectKind.Settlement, memory: "~245", hours: 12);

        TooltipRefresherCollection_Subject_HiddenHosts_Patch.Postfix(vm);

        Assert.AreEqual("~245", vm.TooltipPropertyList[0].ValueLabel);
        Assert.AreEqual("Counted 12h ago", vm.TooltipPropertyList[1].ValueLabel);
    }

    // ---- The army frame carries the army's memory; a party that leads one does not

    [TestMethod]
    public void Open_Army_CarriesTheArmysMemory_AButTheLeadersPartyTooltipDoesNot()
    {
        var leader = Party(345, 0.1f) with { LeadsArmy = true, OwnHealthyCount = 45 };
        SeesParty(leader);
        HiddenHostsTooltipFrames.Open(Bare<Army>(), "hook");
        SeesParty(leader with { DistanceToMain = 70f });

        HiddenHostsTooltipFrames.Open(Bare<Army>(), "hook");
        var army = HiddenHostsTooltipScope.Current!;
        HiddenHostsTooltipFrames.Open(Bare<MobileParty>(), "hook");
        var party = HiddenHostsTooltipScope.Current!;

        Assert.AreEqual(TooltipSubjectKind.Army, army.Kind);
        Assert.AreEqual("~345", army.Verdict.MemoryText);
        Assert.AreEqual(TooltipSubjectKind.Party, party.Kind);
        Assert.IsNull(party.Verdict.MemoryText);
    }
}
