using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Execution;
using TAOM.Features.FireAndTribute;
using TAOM.Features.FireAndTribute.Hooks;
using TAOM.Features.InitialChildGeneration;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>
/// #870 wiring. The service's hosts, cooldowns and counters reset in <c>FireAndTributeBehavior</c>'s constructor, so
/// they reset per campaign only while the module builds a fresh behavior inside the declaration's factory. A cached or
/// container-resolved behavior would carry one campaign's hosts into the next (csharp-architecture.md, "Singleton Services
/// Holding Per-Campaign State"). The feature registers only through its module, and Main/IoC.cs and Main/SubModule.cs
/// stay untouched.
/// </summary>
[TestClass]
public class FireAndTributeWiringTests
{
    private static readonly Regex InlinePerCampaign =
        new Regex(@"CampaignBehaviorDecl\.Of\(\s*r\s*=>\s*new\s+FireAndTributeBehavior\(");

    private static readonly Regex AnyConstruction = new Regex(@"new\s+FireAndTributeBehavior\(");

    private static bool IsBuiltInlinePerCampaign(string source) =>
        InlinePerCampaign.Matches(source).Count == 1 && AnyConstruction.Matches(source).Count == 1;

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<FireAndTributeModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.AreEqual(1, Regex.Matches(src, @"FireAndTributeModule\(\)").Count);
    }

    [TestMethod]
    public void Module_IsEnabled_OwnsSaveData_AndNamesItself()
    {
        var module = new FireAndTributeModule();

        Assert.IsNull(module.ParkedReason);
        Assert.IsTrue(module.OwnsSaveData, "the hosts, cooldowns and first-seen day ride SyncData, so a fault fails closed");
        Assert.AreEqual("FireAndTribute", module.Id);
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
    }

    [TestMethod]
    public void IoC_AndSubModule_DoNotWireTheFeatureByHand()
    {
        var ioc = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(ioc.Contains("FireAndTribute"), "IoC.cs registers the feature by hand AND through its module");
        Assert.IsFalse(subModule.Contains("FireAndTribute"), "SubModule.cs adds the behavior by hand AND through its module");
    }

    [TestMethod]
    public void Module_BuildsTheBehaviorInlineAsTheFactoryBody()
    {
        var src = RepoPaths.ReadSource("Main/Features/FireAndTribute/FireAndTributeModule.cs", stripComments: true);

        Assert.IsTrue(IsBuiltInlinePerCampaign(src),
            "FireAndTributeModule must build FireAndTributeBehavior with new as the CampaignBehaviorDecl factory's body, exactly once.");
    }

    [TestMethod]
    public void ProductionSource_NoOtherFileConstructsTheBehavior()
    {
        var offenders = Directory.EnumerateFiles(RepoPaths.RepoPath("Main"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsFile(path, "FireAndTributeModule.cs") && !IsFile(path, "FireAndTributeBehavior.cs"))
            .Where(path => RepoPaths.MentionsOutsideComments(path, "FireAndTributeBehavior", wholeWord: true))
            .ToList();

        Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
    }

    [DataTestMethod]
    [DataRow("CampaignBehaviorDecl.Of(r => _cached ??= new FireAndTributeBehavior(a, b)),")]
    [DataRow("CampaignBehaviorDecl.Of(r => r.Resolve<FireAndTributeBehavior>()),")]
    [DataRow("CampaignBehaviorDecl.Of(r => new FireAndTributeBehavior(a, b)), var x = new FireAndTributeBehavior(a, b);")]
    public void Predicate_RejectsACachedOrSharedConstruction(string mutant)
    {
        Assert.IsFalse(IsBuiltInlinePerCampaign(mutant), mutant);
    }

    [TestMethod]
    public void Behavior_IsThin_NoRulesNoCoopChecksNoConditionalCompilation()
    {
        var src = RepoPaths.ReadSource("Main/Features/FireAndTribute/Hooks/FireAndTributeBehavior.cs", stripComments: true);

        Assert.IsFalse(src.Contains("#if"), "no conditional compilation");
        Assert.IsFalse(src.Contains("IsAuthority"), "IsAuthority fails open under BannerlordTogether");
        Assert.IsFalse(src.Contains("ICoopSessionProvider"), "the single-player gate is the service's, in one place");
        Assert.IsFalse(src.Contains("MobileParty.MainParty"), "no engine reads");
    }

    [TestMethod]
    public void Behavior_StaysUnderTheEntryPointLimit()
    {
        var lines = RepoPaths.ReadSource("Main/Features/FireAndTribute/Hooks/FireAndTributeBehavior.cs").Split('\n').Length;

        Assert.IsTrue(lines < 150, lines + " lines");
    }

    [TestMethod]
    public void TheSaveRecordUsesOneKey()
    {
        var src = RepoPaths.ReadSource("Main/Features/FireAndTribute/Hooks/FireAndTributeBehavior.cs", stripComments: true);

        Assert.AreEqual(1, Regex.Matches(src, @"dataStore\.SyncData\(").Count);
        StringAssert.Contains(src, "\"_taom_fire_and_tribute_v1\"");
    }

    [TestMethod]
    public void TheFeature_BansNothingTheHouseRulesBan()
    {
        foreach (var path in Directory.EnumerateFiles(RepoPaths.RepoPath("Main", "Features", "FireAndTribute"), "*.cs", SearchOption.AllDirectories)
                     .Concat(new[] { RepoPaths.RepoPath("Main", "Adapters", "RaidHostAdapter.cs"), RepoPaths.RepoPath("Main", "Adapters", "FireAndTributeNoticeAdapter.cs") }))
        {
            var text = RepoPaths.StripComments(File.ReadAllText(path));
            Assert.IsFalse(text.Contains("#region"), path);
            Assert.IsFalse(text.Contains("[Obsolete"), path);
            Assert.IsFalse(text.Contains("#if DEBUG"), path);
        }
    }

    [TestMethod]
    public void Service_TakesNoSealedEngineType()
    {
        var ctor = typeof(FireAndTributeService).GetConstructors().Single();
        var forbidden = ctor.GetParameters().Select(p => p.ParameterType)
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("TaleWorlds", StringComparison.Ordinal))
            .ToList();

        Assert.AreEqual(0, forbidden.Count, string.Join(", ", forbidden.Select(t => t.Name)));
        foreach (var method in typeof(IRaidHostAdapter).GetMethods())
            Assert.IsFalse(method.GetParameters().Any(p => p.ParameterType.Namespace?.StartsWith("TaleWorlds") == true), method.Name);
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_RegistersTheServiceGraph_AndTheBehaviorResolves()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IRandomSource>());
        container.RegisterInstance(Substitute.For<IKingdomWarSnapshotAdapter>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<IWarOfTheRingService>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        var module = new FireAndTributeModule();

        module.RegisterServices(container);

        var decl = module.CampaignBehaviors.Single();
        Assert.AreEqual(typeof(FireAndTributeBehavior), decl.BehaviorType);
        Assert.IsInstanceOfType(decl.Create(container), typeof(FireAndTributeBehavior));
        Assert.AreNotSame(decl.Create(container), decl.Create(container), "a fresh behavior per campaign start");
        Assert.IsInstanceOfType(container.Resolve<IFireAndTributeService>(), typeof(FireAndTributeService));
        Assert.AreSame(container.Resolve<IFireAndTributeService>(), container.Resolve<IFireAndTributeService>());
        Assert.IsInstanceOfType(container.Resolve<IRaidHostAdapter>(), typeof(RaidHostAdapter));
        Assert.IsInstanceOfType(container.Resolve<IFireAndTributeNoticeAdapter>(), typeof(FireAndTributeNoticeAdapter));
        Assert.IsInstanceOfType(container.Resolve<IFireAndTributeSettings>(), typeof(FireAndTributeSettingsProvider));
        Assert.IsInstanceOfType(container.Resolve<IFireAndTributeConfigProvider>(), typeof(FireAndTributeConfigProvider));
    }

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
}
