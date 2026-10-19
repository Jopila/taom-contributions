using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.RealmBorders;
using TAOM.Features.WatchfulHolds;
using TAOM.Features.WatchfulHolds.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WatchfulHolds;

/// <summary>
/// #822 wiring. The service's horn clock and counters reset in <c>WatchfulHoldsBehavior</c>'s constructor, so they
/// reset per campaign only while the module builds a fresh behavior inside the declaration's factory. A cached
/// or container-resolved behavior would carry one campaign's horn cooldown into the next (csharp-architecture.md,
/// "Singleton Services Holding Per-Campaign State"). The feature registers only through its module.
/// </summary>
[TestClass]
public class WatchfulHoldsWiringTests
{
    private static readonly Regex InlinePerCampaign =
        new Regex(@"CampaignBehaviorDecl\.Of\(\s*r\s*=>\s*new\s+WatchfulHoldsBehavior\(");

    private static readonly Regex AnyConstruction = new Regex(@"new\s+WatchfulHoldsBehavior\(");

    private static bool IsBuiltInlinePerCampaign(string source) =>
        InlinePerCampaign.Matches(source).Count == 1 && AnyConstruction.Matches(source).Count == 1;

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<WatchfulHoldsModule>().Count());
    }

    [TestMethod]
    public void Module_IsEnabledAndSavesNothing()
    {
        var module = new WatchfulHoldsModule();

        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual("WatchfulHolds", module.Id);
    }

    [TestMethod]
    public void IoC_AndSubModule_DoNotWireTheFeatureByHand()
    {
        var ioc = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(ioc.Contains("WatchfulHolds"), "IoC.cs registers the feature by hand AND through its module");
        Assert.IsFalse(subModule.Contains("WatchfulHolds"), "SubModule.cs adds the behavior by hand AND through its module");
    }

    [TestMethod]
    public void Module_BuildsTheBehaviorInlineAsTheFactoryBody()
    {
        var src = RepoPaths.ReadSource("Main/Features/WatchfulHolds/WatchfulHoldsModule.cs", stripComments: true);

        Assert.IsTrue(IsBuiltInlinePerCampaign(src),
            "WatchfulHoldsModule must build WatchfulHoldsBehavior with new as the CampaignBehaviorDecl factory's body, exactly once.");
    }

    [TestMethod]
    public void ProductionSource_NoOtherFileConstructsTheBehavior()
    {
        var offenders = Directory.EnumerateFiles(RepoPaths.RepoPath("Main"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsFile(path, "WatchfulHoldsModule.cs") && !IsFile(path, "WatchfulHoldsBehavior.cs"))
            .Where(path => RepoPaths.MentionsOutsideComments(path, "WatchfulHoldsBehavior", wholeWord: true))
            .ToList();

        Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
    }

    [DataTestMethod]
    [DataRow("CampaignBehaviorDecl.Of(r => _cached ??= new WatchfulHoldsBehavior(a, b, c)),")]
    [DataRow("CampaignBehaviorDecl.Of(r => r.Resolve<WatchfulHoldsBehavior>()),")]
    [DataRow("CampaignBehaviorDecl.Of(r => new WatchfulHoldsBehavior(a, b, c)), var x = new WatchfulHoldsBehavior(a, b, c);")]
    public void Predicate_RejectsACachedOrSharedConstruction(string mutant)
    {
        Assert.IsFalse(IsBuiltInlinePerCampaign(mutant), mutant);
    }

    [TestMethod]
    public void Behavior_BothHandlersStandDownInAnyCoopSession()
    {
        var src = RepoPaths.ReadSource("Main/Features/WatchfulHolds/Hooks/WatchfulHoldsBehavior.cs", stripComments: true);

        Assert.AreEqual(2, Regex.Matches(src, @"if \(_coopSession\.IsSessionActive \|\| _coopSession\.ShouldDeferToHost\)\s*return;").Count,
            "the hourly and the daily handler each return in a live co-op session and under an unprobeable co-op mod");
        Assert.IsFalse(src.Contains("IsAuthority"), "IsAuthority fails open under BannerlordTogether");
        Assert.IsFalse(src.Contains("#if"), "no conditional compilation");
    }

    [TestMethod]
    public void Behavior_StaysUnderTheEntryPointLimit()
    {
        var lines = RepoPaths.ReadSource("Main/Features/WatchfulHolds/Hooks/WatchfulHoldsBehavior.cs").Split('\n').Length;

        Assert.IsTrue(lines < 150, lines + " lines");
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_RegistersTheServiceGraph_AndTheBehaviorResolves()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IRandomSource>());
        container.RegisterInstance(Substitute.For<IEnlistmentStateQuery>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IRealmBordersSettings>());
        container.RegisterInstance(new RealmTerritoryService(
            Substitute.For<IMapTerrainAdapter>(), Substitute.For<IRealmMapAdapter>(),
            Substitute.For<ITerritoryWorker>(), Substitute.For<IModLogger>()));
        var module = new WatchfulHoldsModule();

        module.RegisterServices(container);

        var decl = module.CampaignBehaviors.Single();
        Assert.AreEqual(typeof(WatchfulHoldsBehavior), decl.BehaviorType);
        Assert.IsInstanceOfType(decl.Create(container), typeof(WatchfulHoldsBehavior));
        Assert.AreNotSame(decl.Create(container), decl.Create(container), "a fresh behavior per campaign start");
        Assert.IsInstanceOfType(container.Resolve<IWatchfulHoldsService>(), typeof(WatchfulHoldsService));
        Assert.AreSame(container.Resolve<IWatchfulHoldsService>(), container.Resolve<IWatchfulHoldsService>());
        Assert.IsInstanceOfType(container.Resolve<IWatchfulHoldsAdapter>(), typeof(WatchfulHoldsAdapter));
        Assert.IsInstanceOfType(container.Resolve<IWatchfulHoldsNoticeAdapter>(), typeof(WatchfulHoldsNoticeAdapter));
        Assert.IsInstanceOfType(container.Resolve<IWatchfulProvinceLookup>(), typeof(RealmProvinceLookup));
        Assert.IsInstanceOfType(container.Resolve<IWatchfulHoldsSettings>(), typeof(WatchfulHoldsSettingsProvider));
        Assert.IsInstanceOfType(container.Resolve<IWatchfulHoldsConfigProvider>(), typeof(WatchfulHoldsConfigProvider));
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Behavior_Constructor_ResetsTheService()
    {
        var service = Substitute.For<IWatchfulHoldsService>();

        _ = new WatchfulHoldsBehavior(service, Substitute.For<ICoopSessionProvider>(), Substitute.For<IModLogger>());

        service.Received(1).ResetForNewSession();
    }

    [TestCategory("RequiresGame")]
    [DataTestMethod]
    [DataRow(false, false, 1)]
    [DataRow(true, false, 0)]
    [DataRow(false, true, 0)]
    public void Behavior_DailyTick_LogsTheSummaryOnlyWhenSolo(bool sessionActive, bool defer, int expectedCalls)
    {
        var service = Substitute.For<IWatchfulHoldsService>();
        var sut = new WatchfulHoldsBehavior(service, Coop(sessionActive, defer), Substitute.For<IModLogger>());

        Invoke(sut, "OnDailyTick");

        service.Received(expectedCalls).LogDailySummary();
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Behavior_DailyTick_AThrowingServiceIsLoggedNotRaised()
    {
        var service = Substitute.For<IWatchfulHoldsService>();
        service.When(s => s.LogDailySummary()).Do(_ => throw new InvalidOperationException("boom"));
        var logger = Substitute.For<IModLogger>();
        var sut = new WatchfulHoldsBehavior(service, Coop(false, false), logger);

        Invoke(sut, "OnDailyTick");

        logger.Received(1).LogError(NSubstitute.Arg.Is<string>(s => s.Contains("[WatchfulHolds]") && s.Contains("boom")));
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Behavior_HourlyTick_ANullPartyIsIgnored()
    {
        var service = Substitute.For<IWatchfulHoldsService>();
        var sut = new WatchfulHoldsBehavior(service, Coop(false, false), Substitute.For<IModLogger>());

        Invoke(sut, "OnHourlyTickParty", new object?[] { null });

        service.DidNotReceiveWithAnyArgs().OnHourlyTickParty(null!);
    }

    [TestCategory("RequiresGame")]
    [DataTestMethod]
    [DataRow(false, false, 1)]
    [DataRow(true, false, 0)]
    [DataRow(false, true, 0)]
    public void Behavior_HourlyTick_ALordPartyReachesTheServiceOnlyWhenSolo(bool sessionActive, bool defer, int expectedCalls)
    {
        var service = Substitute.For<IWatchfulHoldsService>();
        var sut = new WatchfulHoldsBehavior(service, Coop(sessionActive, defer), Substitute.For<IModLogger>());
        var party = LordParty();

        Invoke(sut, "OnHourlyTickParty", new object?[] { party });

        service.Received(expectedCalls).OnHourlyTickParty(party);
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Behavior_HourlyTick_AThrowingServiceIsLoggedNotRaised()
    {
        var service = Substitute.For<IWatchfulHoldsService>();
        service.When(s => s.OnHourlyTickParty(NSubstitute.Arg.Any<object>())).Do(_ => throw new InvalidOperationException("boom"));
        var logger = Substitute.For<IModLogger>();
        var sut = new WatchfulHoldsBehavior(service, Coop(false, false), logger);

        Invoke(sut, "OnHourlyTickParty", new object?[] { LordParty() });

        logger.Received(1).LogError(NSubstitute.Arg.Is<string>(s => s.Contains("[WatchfulHolds]") && s.Contains("boom")));
    }

    // A bare lord party: IsLordParty has a private setter (v1.5.5 MobileParty.cs:1270), and its being true keeps the
    // handler from reading IsMainParty, which needs a running campaign. A non-lord party cannot be built without one.
    private static MobileParty LordParty()
    {
        var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        typeof(MobileParty).GetProperty(nameof(MobileParty.IsLordParty))!.SetValue(party, true);
        return party;
    }

    private static ICoopSessionProvider Coop(bool sessionActive, bool defer)
    {
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsSessionActive.Returns(sessionActive);
        coop.ShouldDeferToHost.Returns(defer);
        return coop;
    }

    private static void Invoke(object target, string method, object?[]? args = null) =>
        target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(target, args ?? new object?[0]);

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
}
