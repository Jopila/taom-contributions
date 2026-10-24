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
using TAOM.Features.SettlementServices;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SettlementServices;

/// <summary>
/// Wiring guard for Watch-Silver and Tidings of the Lists (#859): the module line, its empty save footprint, the container graph,
/// and the shape rules the two behaviors must keep (ADR-002 size, the events they subscribe to, the shared fault log behind every
/// engine-called member). The engine registers and calls those members, so the rules are pinned on the comment-stripped sources.
/// </summary>
[TestClass]
public class SettlementServicesWiringTests
{
    private static string Dialog() => RepoPaths.ReadSource("Main/Features/SettlementServices/Hooks/WatchSilverDialogBehavior.cs", stripComments: true);

    private static string Menu() => RepoPaths.ReadSource("Main/Features/SettlementServices/Hooks/TidingsMenuBehavior.cs", stripComments: true);

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
        => Assert.AreEqual(1, FeatureModules.All.OfType<SettlementServicesModule>().Count());

    [TestMethod]
    public void TheModule_IsNamedSettlementServices_NotParked_OwnsNoSaveData_DeclaresTwoBehaviorsOnly()
    {
        var module = new SettlementServicesModule();

        Assert.AreEqual("SettlementServices", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "a fief's loyalty and the player's gold are saved by the engine; the feature saves nothing of its own");
        Assert.AreEqual(2, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.PatchCategories.Count, "no Harmony");
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
        new SettlementServicesModule().RegisterServices(container);

        Assert.IsNotNull(container.Resolve<WatchSilverService>());
        Assert.AreSame(container.Resolve<WatchSilverService>(), container.Resolve<WatchSilverService>());
        Assert.IsNotNull(container.Resolve<TidingsService>());
        Assert.IsInstanceOfType(container.Resolve<ISettlementServicesAdapter>(), typeof(SettlementServicesAdapter));
        Assert.IsNotNull(container.Resolve<SettlementServicesSettingsProvider>());
    }

    [TestMethod]
    public void BothBehaviors_AreBuiltWithNewInsideTheDeclarations_SoTheyAreFreshPerCampaign()
    {
        var src = RepoPaths.ReadSource("Main/Features/SettlementServices/SettlementServicesModule.cs", stripComments: true);

        Assert.IsTrue(Regex.IsMatch(src, @"CampaignBehaviorDecl\.Of\(r => new WatchSilverDialogBehavior\(r\.Resolve<WatchSilverService>\(\), r\.Resolve<IModLogger>\(\)\)\)"));
        Assert.IsTrue(Regex.IsMatch(src, @"CampaignBehaviorDecl\.Of\(r => new TidingsMenuBehavior\(r\.Resolve<TidingsService>\(\), r\.Resolve<IModLogger>\(\)\)\)"));
    }

    [TestMethod]
    public void IoC_DoesNotRegisterTheFeatureByHand()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true) + RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("SettlementServices"), "the module registers everything");
    }

    [TestMethod]
    public void EveryEntryPointFile_StaysUnder150Lines()
    {
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "SettlementServices", "Hooks"), "*.cs");

        Assert.AreEqual(4, files.Length, "the texts, the lines and the two behaviors");
        foreach (var file in files)
            Assert.IsTrue(File.ReadAllLines(file).Length < 150, "ADR-002: " + Path.GetFileName(file) + " is 150 lines or more");
    }

    [TestMethod]
    public void TheDialogBehavior_SubscribesTheSessionLaunchAndTheConversationEnd_AndSavesNothing()
    {
        var src = Dialog();

        StringAssert.Contains(src, "CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched)");
        StringAssert.Contains(src, "CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded)");
        Assert.IsFalse(src.Contains("OnAfterSessionLaunchedEvent"));
        Assert.IsFalse(src.Contains("dataStore.SyncData"), "nothing is saved");
    }

    [TestMethod]
    public void TheDialogBehavior_ClearsTheOutcome_OnEveryConversationEnd_AndWhenAnOfferStarts()
    {
        var src = Dialog();

        Assert.IsTrue(Regex.IsMatch(src, @"void OnConversationEnded\(IEnumerable<CharacterObject> \w+\) => _outcome = null;"));
        Assert.IsTrue(Regex.IsMatch(src, @"void StartOffer\(\) => _outcome = null;"));
    }

    [TestMethod]
    public void TheMenuBehavior_SubscribesTheAfterSessionLaunch_NotTheSessionLaunch()
    {
        var src = Menu();

        StringAssert.Contains(src, "CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, OnAfterSessionLaunched)");
        Assert.IsFalse(src.Contains("OnSessionLaunchedEvent"), "an option inserted into another feature's menu registers after every session listener");
        Assert.IsFalse(src.Contains("dataStore.SyncData"), "nothing is saved");
    }

    [TestMethod]
    public void TheMenuBehavior_InsertsAboveTheBackOption_InTheArenaMenu()
    {
        var src = Menu();

        StringAssert.Contains(src, "internal const string MenuId = \"town_arena\"");
        StringAssert.Contains(src, "internal const string BackOptionId = \"town_arena_back\"");
        Assert.IsTrue(Regex.IsMatch(src, @"GetPresumedGameMenu\(MenuId\)\.MenuOptions\.Select\(o => o\.IdString\)\.ToList\(\)\.IndexOf\(BackOptionId\)"));
        Assert.IsTrue(Regex.IsMatch(src, @"isLeave: false, index: index"));
        StringAssert.Contains(src, "GameMenuOption.LeaveType.Conversation");
    }

    [TestMethod]
    public void BothBehaviors_UseTheSharedFaultLog_UnderOneFeatureTag()
    {
        foreach (var src in new[] { Dialog(), Menu() })
            StringAssert.Contains(src, "_faults = new ScreenFaultLog(logger, \"SettlementServices\");");
        Assert.IsFalse(File.Exists(RepoPaths.RepoPath("Main", "Features", "SettlementServices", "Hooks", "CallbackFaultLatch.cs")), "one fault log, not a copy");
    }

    [TestMethod]
    public void TheDialogBehavior_RunsEveryEngineCalledMemberBehindTheLatch()
    {
        var src = Dialog();

        foreach (var site in new[] { "\"watch-silver session launched\"", "\"offer condition\"", "\"offer clickable condition\"", "\"ask condition\"", "\"done condition\"" })
            StringAssert.Contains(src, site);
        Assert.IsTrue(Regex.IsMatch(src, @"_faults\.Condition\(""tier "" \+ index \+ "" condition"""));
        Assert.IsTrue(Regex.IsMatch(src, @"_faults\.Run\(""tier "" \+ index \+ "" consequence"""));
        Assert.IsTrue(Regex.IsMatch(src, @"_faults\.LogOnce\(""tier "" \+ index \+ "" clickable condition"""));
    }

    [TestMethod]
    public void TheMenuBehavior_RunsEveryEngineCalledMemberBehindTheLatch()
    {
        var src = Menu();

        StringAssert.Contains(src, "_faults.Run(\"tidings session launched\"");
        StringAssert.Contains(src, "_faults.Condition(\"menu condition\"");
        StringAssert.Contains(src, "_faults.Run(\"menu consequence\"");
    }

    [TestMethod]
    public void NoCatchInTheFeatureSwallowsWithoutLogging()
    {
        foreach (var file in Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "SettlementServices"), "*.cs", SearchOption.AllDirectories))
        {
            var src = RepoPaths.StripComments(File.ReadAllText(file));
            Assert.IsFalse(Regex.IsMatch(src, @"catch\s*(\{|\(Exception\s*\))"), Path.GetFileName(file) + ": a catch without a variable cannot log the cause");
            Assert.IsFalse(Regex.IsMatch(src, @"catch[^{]*\{\s*\}"), Path.GetFileName(file) + ": an empty catch");
        }
    }

    [TestMethod]
    public void BothSwitches_AreListedAsPlayerLocal_InTheCoopRelevanceTable()
    {
        var src = RepoPaths.ReadSource("Main/Features/CoopInterop/CoopSettingsRelevance.cs", stripComments: true);
        var start = src.IndexOf("PlayerLocal =", System.StringComparison.Ordinal);
        var block = src.Substring(start, src.IndexOf("};", start, System.StringComparison.Ordinal) - start);

        StringAssert.Contains(block, "\"EnableWatchSilver\"");
        StringAssert.Contains(block, "\"EnableTidingsOfTheLists\"");
    }
}
