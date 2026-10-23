using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.ArmyTargeting.HoldTheWalls;
using TAOM.Features.ArmyTargeting.HoldTheWalls.Hooks;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.ArmyTargeting.HoldTheWalls;

/// <summary>
/// #871 wiring. The service's captures and holds reset in <c>HoldTheWallsBehavior</c>'s constructor, so they reset per
/// campaign only while the module builds a fresh behavior inside the declaration's factory. A cached or container-resolved
/// behavior would carry one campaign's holds into the next (csharp-architecture.md, "Singleton Services Holding Per-Campaign
/// State"). The feature registers only through its module, and Main/IoC.cs and Main/SubModule.cs stay untouched.
/// </summary>
[TestClass]
public class HoldTheWallsWiringTests
{
    private const string BehaviorPath = "Main/Features/ArmyTargeting/HoldTheWalls/Hooks/HoldTheWallsBehavior.cs";

    private static readonly Regex InlinePerCampaign =
        new Regex(@"CampaignBehaviorDecl\.Of\(\s*r\s*=>\s*new\s+HoldTheWallsBehavior\(");

    private static readonly Regex AnyConstruction = new Regex(@"new\s+HoldTheWallsBehavior\(");

    private static bool IsBuiltInlinePerCampaign(string source) =>
        InlinePerCampaign.Matches(source).Count == 1 && AnyConstruction.Matches(source).Count == 1;

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce_AppendedAsOneLine()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<HoldTheWallsModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.AreEqual(1, Regex.Matches(src, @"HoldTheWallsModule\(\)").Count);
    }

    [TestMethod]
    public void Module_IsEnabled_SavesNothing_AndNamesItself()
    {
        var module = new HoldTheWallsModule();

        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "the engine saves and expires the AI pause itself");
        Assert.AreEqual("HoldTheWalls", module.Id);
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
    }

    [TestMethod]
    public void IoC_AndSubModule_DoNotWireTheFeatureByHand()
    {
        var ioc = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var armyIoc = RepoPaths.ReadSource("Main/Features/ArmyTargeting/ArmyTargetingIoC.cs", stripComments: true);

        Assert.IsFalse(ioc.Contains("HoldTheWalls"), "IoC.cs registers the feature by hand AND through its module");
        Assert.IsFalse(subModule.Contains("HoldTheWalls"), "SubModule.cs adds the behavior by hand AND through its module");
        Assert.IsFalse(armyIoc.Contains("HoldTheWalls"), "ArmyTargetingIoC is not where this feature registers");
    }

    [TestMethod]
    public void Module_BuildsTheBehaviorInlineAsTheFactoryBody()
    {
        var src = RepoPaths.ReadSource("Main/Features/ArmyTargeting/HoldTheWalls/HoldTheWallsModule.cs", stripComments: true);

        Assert.IsTrue(IsBuiltInlinePerCampaign(src),
            "HoldTheWallsModule must build HoldTheWallsBehavior with new as the CampaignBehaviorDecl factory's body, exactly once.");
    }

    [TestMethod]
    public void ProductionSource_NoOtherFileConstructsTheBehavior()
    {
        var offenders = RepoPaths.CSharpFiles()
            .Where(path => !IsFile(path, "HoldTheWallsModule.cs") && !IsFile(path, "HoldTheWallsBehavior.cs"))
            .Where(path => RepoPaths.MentionsOutsideComments(path, "HoldTheWallsBehavior", wholeWord: true))
            .ToList();

        Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
    }

    [DataTestMethod]
    [DataRow("CampaignBehaviorDecl.Of(r => _cached ??= new HoldTheWallsBehavior(a, b)),")]
    [DataRow("CampaignBehaviorDecl.Of(r => r.Resolve<HoldTheWallsBehavior>()),")]
    [DataRow("CampaignBehaviorDecl.Of(r => new HoldTheWallsBehavior(a, b)), var x = new HoldTheWallsBehavior(a, b);")]
    public void Predicate_RejectsACachedOrSharedConstruction(string mutant)
    {
        Assert.IsFalse(IsBuiltInlinePerCampaign(mutant), mutant);
    }

    [TestMethod]
    public void Behavior_IsThin_NoRulesNoCoopChecksNoPartyReads()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        Assert.IsFalse(src.Contains("#if"), "no conditional compilation");
        Assert.IsFalse(src.Contains("IsAuthority"), "IsAuthority fails open under BannerlordTogether");
        Assert.IsFalse(src.Contains("ICoopSessionProvider"), "the single-player gate is the service's, in one place");
        Assert.IsFalse(src.Contains("MobileParty"), "no party reads");
        Assert.IsFalse(src.Contains("DisableForHours"), "the one engine write is the adapter's");
    }

    [TestMethod]
    public void Behavior_ListensToTheOwnerChangeTheDisbandAndBothTicks_AndSavesNothing()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        StringAssert.Contains(src, "CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener");
        StringAssert.Contains(src, "CampaignEvents.HourlyTickEvent.AddNonSerializedListener");
        StringAssert.Contains(src, "CampaignEvents.DailyTickEvent.AddNonSerializedListener");
        StringAssert.Contains(src, "CampaignEvents.ArmyDispersed.AddNonSerializedListener");
        Assert.AreEqual(0, Regex.Matches(src, @"dataStore\.SyncData\(").Count, "no save data");
    }

    [TestMethod]
    public void Behavior_EveryHandlerIsGuarded()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        Assert.AreEqual(4, Regex.Matches(src, @"catch \(Exception ex\)").Count, "one catch per campaign event handler");
    }

    [TestMethod]
    public void Behavior_StaysUnderTheEntryPointLimit()
    {
        var lines = RepoPaths.ReadSource(BehaviorPath).Split('\n').Length;

        Assert.IsTrue(lines < 150, lines + " lines");
    }

    [TestMethod]
    public void TheFeature_BansNothingTheHouseRulesBan()
    {
        var files = Directory.EnumerateFiles(RepoPaths.RepoPath("Main", "Features", "ArmyTargeting", "HoldTheWalls"), "*.cs", SearchOption.AllDirectories)
            .Concat(new[]
            {
                RepoPaths.RepoPath("Main", "Adapters", "FortHoldAdapter.cs"), RepoPaths.RepoPath("Main", "Adapters", "IFortHoldAdapter.cs"),
                RepoPaths.RepoPath("Main", "Adapters", "FortArmyReading.cs"),
            });
        foreach (var path in files)
        {
            var text = RepoPaths.StripComments(File.ReadAllText(path));
            Assert.IsFalse(text.Contains("#region"), path);
            Assert.IsFalse(text.Contains("[Obsolete"), path);
            Assert.IsFalse(text.Contains("#if DEBUG"), path);
        }
    }

    [TestMethod]
    public void Service_TakesNoSealedEngineType_AndTheAdapterInterfaceSpeaksInPlainValues()
    {
        var ctor = typeof(HoldTheWallsService).GetConstructors().Single();
        var forbidden = ctor.GetParameters().Select(p => p.ParameterType)
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("TaleWorlds", StringComparison.Ordinal))
            .ToList();

        Assert.AreEqual(0, forbidden.Count, string.Join(", ", forbidden.Select(t => t.Name)));
        foreach (var method in typeof(IFortHoldAdapter).GetMethods())
        {
            Assert.IsFalse(method.GetParameters().Any(p => p.ParameterType.Namespace?.StartsWith("TaleWorlds") == true), method.Name);
            Assert.IsFalse(method.ReturnType.Namespace?.StartsWith("TaleWorlds") == true, method.Name);
        }
        foreach (var property in typeof(FortArmyReading).GetProperties())
            Assert.IsFalse(property.PropertyType.Namespace?.StartsWith("TaleWorlds") == true, property.Name);
    }

    [TestMethod]
    public void TheService_ReadsTheSwitchAsBothMcmSettings_AndNeverChecksTheSiegeItself()
    {
        var src = RepoPaths.ReadSource("Main/Features/ArmyTargeting/HoldTheWalls/HoldTheWallsService.cs", stripComments: true);

        StringAssert.Contains(src, "_settings.EnableArmyStrategicIntelligence && _settings.HoldCapturedForts");
        Assert.IsFalse(Regex.IsMatch(src, @"InMapEvent\s*\|\|\s*army\.FortUnderSiege"), "MayStart owns the siege rule: the service passes the reading and tests nothing itself");
    }

    [TestMethod]
    public void TheService_ResolvesTheSideOnlyThroughTheSharedPair()
    {
        var src = RepoPaths.ReadSource("Main/Features/ArmyTargeting/HoldTheWalls/HoldTheWallsService.cs", stripComments: true);

        StringAssert.Contains(src, "_alignment.ResolveSide(army.Side)");
        Assert.IsFalse(Regex.IsMatch(src, @"new\s+SideIds"), "a pair is built only by FactionSides (#957)");
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_RegistersTheServiceGraph_AndTheBehaviorResolves()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IArmyTargetingConfigProvider>());
        container.RegisterInstance(Substitute.For<IArmyTargetingSettingsProvider>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        var module = new HoldTheWallsModule();

        module.RegisterServices(container);

        var decl = module.CampaignBehaviors.Single();
        Assert.AreEqual(typeof(HoldTheWallsBehavior), decl.BehaviorType);
        Assert.IsInstanceOfType(decl.Create(container), typeof(HoldTheWallsBehavior));
        Assert.AreNotSame(decl.Create(container), decl.Create(container), "a fresh behavior per campaign start");
        Assert.IsInstanceOfType(container.Resolve<IHoldTheWallsService>(), typeof(HoldTheWallsService));
        Assert.AreSame(container.Resolve<IHoldTheWallsService>(), container.Resolve<IHoldTheWallsService>());
        Assert.IsInstanceOfType(container.Resolve<IFortHoldAdapter>(), typeof(FortHoldAdapter));
    }

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
}
