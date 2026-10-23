using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.Namegiving;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Namegiving;

/// <summary>
/// Wiring guard for Namegiving (#858): the module line, its empty save footprint, the container graph, and the shape rules the
/// mixin and the behavior must keep (ADR-002 size, a latch behind every engine-called member, a guarded load handler). The
/// mixin and the behavior need a running campaign, so those rules are pinned on the comment-stripped sources.
/// </summary>
[TestClass]
public class NamegivingWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<NamegivingModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedNamegiving_NotParked_OwnsNoSaveData_DeclaresOneBehaviorOnly()
    {
        var module = new NamegivingModule();

        Assert.AreEqual("Namegiving", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "a custom name is saved by the engine with the settlement, the feature saves nothing of its own");
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.PatchCategories.Count, "no Harmony: the feature is a mixin, a prefab insertion and a behavior");
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void TheModulesServices_ResolveFromARealContainer()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        new NamegivingModule().RegisterServices(container);

        Assert.IsNotNull(container.Resolve<NamegivingService>());
        Assert.AreSame(container.Resolve<NamegivingService>(), container.Resolve<NamegivingService>());
        Assert.IsInstanceOfType(container.Resolve<IFiefNamingAdapter>(), typeof(FiefNamingAdapter));
        Assert.IsNotNull(container.Resolve<NamegivingSettingsProvider>());
    }

    [TestMethod]
    public void TheBehavior_IsBuiltWithNewInsideTheDeclaration_SoItIsFreshPerCampaign()
    {
        var src = RepoPaths.ReadSource("Main/Features/Namegiving/NamegivingModule.cs", stripComments: true);

        Assert.IsTrue(Regex.IsMatch(src, @"CampaignBehaviorDecl\.Of\(r => new NamegivingBehavior\(r\.Resolve<NamegivingService>\(\)\)\)"));
    }

    [TestMethod]
    public void IoC_DoesNotRegisterTheFeatureByHand()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true) + RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("Namegiving"), "the module registers everything; UIExtenderEx finds the mixin and the prefab by assembly scan");
    }

    [TestMethod]
    public void EveryEntryPointFile_StaysUnder150Lines()
    {
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "Namegiving", "UI"), "*.cs")
            .Concat(Directory.GetFiles(RepoPaths.RepoPath("Client", "Features", "Namegiving", "UI"), "*.cs"))
            .Concat(Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "Namegiving", "Hooks"), "*.cs")).ToList();

        Assert.IsTrue(files.Count >= 5, "the mixin, the prompts, the texts, the prefab and the behavior");
        foreach (var file in files)
            Assert.IsTrue(File.ReadAllLines(file).Length < 150, "ADR-002: " + Path.GetFileName(file) + " is 150 lines or more");
    }

    [TestMethod]
    public void TheBehavior_ListensForTheCaptureAndTheSessionStart_AndSavesNothing()
    {
        var src = Behavior();

        StringAssert.Contains(src, "OnSettlementOwnerChangedEvent.AddNonSerializedListener");
        StringAssert.Contains(src, "OnClanChangedKingdomEvent.AddNonSerializedListener");
        StringAssert.Contains(src, "OnSessionLaunchedEvent.AddNonSerializedListener");
        StringAssert.Contains(src, "ReconcileAllLore()");
        StringAssert.Contains(src, "ReconcileLore(");
        Assert.IsFalse(src.Contains("dataStore.SyncData"), "nothing is saved: the engine saves the custom name with the settlement");
    }

    [TestMethod]
    public void TheBehavior_RunsTheFullPassOnSessionLaunched_NotOnGameLoaded()
    {
        // OnGameLoadedEvent fires for saved games only; OnSessionLaunchedEvent fires for new and loaded campaigns (lessons/state-lifecycle-save.md).
        Assert.IsFalse(Behavior().Contains("OnGameLoadedEvent"), "a new campaign would never get the pass");
    }

    [TestMethod]
    public void TheBehavior_ReconcilesOnlyTheChangingClansSettlements_AfterAKingdomChange()
    {
        // A clan that joins, defects from, founds or rebels against a kingdom keeps its fiefs with no owner event (lessons/campaign-mechanics.md).
        var src = Behavior();

        Assert.IsTrue(Regex.IsMatch(src, @"OnClanChangedKingdomEvent\.AddNonSerializedListener\(this, OnClanChangedKingdom\)"));
        Assert.IsTrue(Regex.IsMatch(src, @"void OnClanChangedKingdom\(Clan clan, Kingdom \w+, Kingdom \w+,\s*ChangeKingdomAction\.ChangeKingdomActionDetail \w+, bool \w+\)"));
        Assert.IsTrue(Regex.IsMatch(src, @"clan\?\.Settlements"), "a null clan or settlement list is tolerated");
        Assert.IsTrue(Regex.IsMatch(src, @"foreach \(var \w+ in settlements\)[\s\S]*?_service\.ReconcileLore\(\w+\.StringId\)"), "one reconcile per settlement the clan owns");
        Assert.AreEqual(1, Regex.Matches(src, @"ReconcileAllLore\(\)").Count, "the kingdom change never runs the full pass");
    }

    [TestMethod]
    public void TheLoadPath_IsGuardedInsideTheService_NotInTheHandler()
    {
        // A throw out of an OnGameLoaded handler loops the loading screen (lessons/campaign-mechanics.md).
        var service = RepoPaths.ReadSource("Main/Features/Namegiving/NamegivingService.cs", stripComments: true);

        Assert.IsTrue(Regex.IsMatch(service, @"public void ReconcileAllLore\(\)[\s\S]*?catch \(Exception ex\)"), "the load pass catches per fief");
        Assert.IsTrue(Regex.IsMatch(service, @"public void ReconcileAllLore\(\)\s*\{\s*try\s*\{[\s\S]*?LoreNames\.All"), "the table is read inside the outer try: a malformed row must not throw out of the handler");
        Assert.IsTrue(Regex.IsMatch(service, @"public void ReconcileLore\(string settlementId\)[\s\S]*?catch \(Exception ex\)"), "the capture pass catches");
    }

    [TestMethod]
    public void TheMixin_GuardsEveryEngineCalledMember_AndLogsThroughALatch()
    {
        var src = Mixin();

        Assert.IsTrue(Regex.IsMatch(src, @"catch \(Exception ex\)"));
        Assert.IsFalse(Regex.IsMatch(src, @"catch\s*(\{|\(Exception\s*\))"), "a catch without a variable cannot log the cause");
        Assert.IsFalse(Regex.IsMatch(src, @"catch[^{]*\{\s*\}"), "an empty catch");
        StringAssert.Contains(src, "LogOnce(");
        Assert.IsTrue(Regex.IsMatch(src, @"Guard\(""refresh"""), "the refresh runs inside the guard");
        Assert.IsTrue(Regex.IsMatch(src, @"Guard\(""selection"""), "the selection handler runs inside the guard");
        Assert.IsTrue(Regex.IsMatch(src, @"Guard\(""click"""), "the click runs inside the guard");
    }

    [TestMethod]
    public void TheMixin_BuildsItsLatchBeforeAnyOtherResolve_SoAConstructorFaultIsLogged()
    {
        var src = Mixin();
        var firstResolve = src.IndexOf("IoC.Resolve<", System.StringComparison.Ordinal);
        var logger = src.IndexOf("_logger = IoC.Resolve<IModLogger>();", System.StringComparison.Ordinal);

        Assert.IsTrue(logger >= 0);
        Assert.AreEqual(logger + "_logger = ".Length, firstResolve, "the logger is the first resolve");
    }

    [TestMethod]
    public void TheMixin_SubscribesInTheConstructor_AndUnsubscribesInOnFinalize()
    {
        var src = Mixin();

        Assert.AreEqual(1, Regex.Matches(src, @"PropertyChangedWithValue \+=").Count);
        Assert.AreEqual(1, Regex.Matches(src, @"PropertyChangedWithValue -=").Count);
        Assert.IsTrue(Regex.IsMatch(src, @"public override void OnFinalize\(\)"));
    }

    [TestMethod]
    public void TheMixinConstructor_DoesNotRefresh_BecauseUIExtenderExReplaysRefreshValues()
    {
        // The target's constructor runs RefreshValues before the mixin exists; UIExtenderEx replays it on the mixin as OnRefresh.
        var src = Mixin();
        var ctor = src.Substring(src.IndexOf("public FiefNamingMixin(", System.StringComparison.Ordinal));
        ctor = ctor.Substring(0, ctor.IndexOf("[DataSourceProperty]", System.StringComparison.Ordinal));

        Assert.IsFalse(Regex.IsMatch(ctor, @"\bRefresh\(\)"), "the constructor must not call Refresh()");
    }

    [TestMethod]
    public void TheMixin_ReadsNoSettingsItself_TheServiceOwnsTheSwitch()
    {
        var src = Mixin();

        Assert.IsFalse(src.Contains("TaomSettings"));
        Assert.IsFalse(src.Contains("NamegivingSettingsProvider"));
    }

    [TestMethod]
    public void TheMixin_NeverWritesTheNameItself_TheServiceDoes()
    {
        var src = Mixin() + RepoPaths.ReadSource("Main/Features/Namegiving/UI/NamegivingPrompts.cs", stripComments: true);

        Assert.IsFalse(src.Contains("SetCustomName") || src.Contains("ChangeClanInfluenceAction"), "writes go through NamegivingService.Apply and the adapter");
        StringAssert.Contains(src, ".Apply(");
    }

    [TestMethod]
    public void ThePrompts_RunEveryCallbackInsideAGuard()
    {
        var src = RepoPaths.ReadSource("Main/Features/Namegiving/UI/NamegivingPrompts.cs", stripComments: true);

        Assert.IsTrue(Regex.IsMatch(src, @"catch \(Exception ex\)"));
        StringAssert.Contains(src, "_onFault(");
        Assert.IsFalse(Regex.IsMatch(src, @"catch\s*(\{|\(Exception\s*\))"), "a catch without a variable cannot log the cause");
    }

    [TestMethod]
    public void ThePrompts_TakeTheNameSnapshotOncePerPrompt_NotPerKeystroke()
    {
        var src = RepoPaths.ReadSource("Main/Features/Namegiving/UI/NamegivingPrompts.cs", stripComments: true);

        Assert.AreEqual(1, Regex.Matches(src, @"\.OtherNames\(").Count);
        Assert.IsTrue(Regex.IsMatch(src, @"var others = _service\.OtherNames\([^)]*\);[\s\S]*?textCondition"), "the snapshot is taken before the text box opens");
    }

    private static string Behavior() => RepoPaths.ReadSource("Main/Features/Namegiving/Hooks/NamegivingBehavior.cs", stripComments: true);

    private static string Mixin() => RepoPaths.ReadSource("Client/Features/Namegiving/UI/FiefNamingMixin.cs", stripComments: true);
}
