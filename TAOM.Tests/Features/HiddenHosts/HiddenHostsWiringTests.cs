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
using TAOM.Features.CoopInterop;
using TAOM.Features.FarWatch;
using TAOM.Features.HiddenHosts;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #846 wiring. The service's per-party latch is reset in <c>HiddenHostsBehavior</c>'s constructor, so it holds only
/// while the module builds a fresh behavior inside the declaration's factory, which the runner calls once per campaign
/// start. A cached or shared instance would run the constructor once per process and carry one campaign's latch into the
/// next with every behavior test green, so the pin is the exact shape (inline <c>new</c> as the factory's body, exactly
/// once) and the cheapest regressions are rows it must reject (lessons/testing-qa.md, "Mutation-check a source-text pin").
/// </summary>
[TestClass]
public class HiddenHostsWiringTests
{
    private const string ModulePath = "Main/Features/HiddenHosts/HiddenHostsModule.cs";

    private static readonly Regex InlinePerCampaign =
        new Regex(@"CampaignBehaviorDecl\.Of\(\s*r\s*=>\s*new\s+HiddenHostsBehavior\(");

    private static readonly Regex AnyConstruction = new Regex(@"new\s+HiddenHostsBehavior\(");

    private static readonly Regex InlineMemoryPerCampaign =
        new Regex(@"CampaignBehaviorDecl\.Of\(\s*r\s*=>\s*new\s+HiddenHostsMemoryBehavior\(");

    private static readonly Regex AnyMemoryConstruction = new Regex(@"new\s+HiddenHostsMemoryBehavior\(");

    private static bool IsBuiltInlinePerCampaign(string source) =>
        InlinePerCampaign.Matches(source).Count == 1 && AnyConstruction.Matches(source).Count == 1;

    private static bool IsMemoryBuiltInlinePerCampaign(string source) =>
        InlineMemoryPerCampaign.Matches(source).Count == 1 && AnyMemoryConstruction.Matches(source).Count == 1;

    private static readonly Regex InlinePlainSightPerCampaign =
        new Regex(@"CampaignBehaviorDecl\.Of\(\s*r\s*=>\s*new\s+HiddenHostsPlainSightBehavior\(");

    private static readonly Regex AnyPlainSightConstruction = new Regex(@"new\s+HiddenHostsPlainSightBehavior\(");

    private static bool IsPlainSightBuiltInlinePerCampaign(string source) =>
        InlinePlainSightPerCampaign.Matches(source).Count == 1 && AnyPlainSightConstruction.Matches(source).Count == 1;

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<HiddenHostsModule>().Count(),
            "HiddenHostsModule must be listed exactly once in Main/Composition/FeatureModules.cs");
    }

    [TestMethod]
    public void Module_OwnsSaveData_SoAFaultFailsClosed() => Assert.IsTrue(new HiddenHostsModule().OwnsSaveData);

    [TestMethod]
    public void Module_IdIsHiddenHosts() => Assert.AreEqual("HiddenHosts", new HiddenHostsModule().Id);

    [TestMethod]
    public void Module_DeclaresThePatchCategoryAtGameInit()
    {
        var decl = new HiddenHostsModule().PatchCategories.Single();

        Assert.AreEqual("Patch105_HiddenHosts", decl.Category);
        Assert.AreEqual(HiddenHostsModule.PatchCategory, decl.Category);
        Assert.AreEqual(ApplyPhase.GameInit, decl.Phase);
    }

    [TestMethod]
    public void IoC_DoesNotRegisterTheFeatureByHand()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        Assert.IsFalse(src.Contains("HiddenHosts"),
            "Main/IoC.cs mentions the feature: the module registers every service, a hand registration would be a second one");
    }

    [TestMethod]
    public void Module_BuildsTheBehaviorInlineAsTheFactoryBody()
    {
        var src = RepoPaths.ReadSource(ModulePath, stripComments: true);

        Assert.IsTrue(IsBuiltInlinePerCampaign(src),
            "HiddenHostsModule must build HiddenHostsBehavior with new as the CampaignBehaviorDecl factory's body, exactly "
            + "once: its constructor is the per-campaign reset of the hidden-hosts service.");
    }

    [TestMethod]
    public void Module_BuildsTheMemoryBehaviorInlineAsAFactoryBody_ItsConstructorIsThePerCampaignReset()
    {
        var src = RepoPaths.ReadSource(ModulePath, stripComments: true);

        Assert.IsTrue(IsMemoryBuiltInlinePerCampaign(src),
            "HiddenHostsModule must build HiddenHostsMemoryBehavior with new as a CampaignBehaviorDecl factory's body, exactly "
            + "once: a cached instance would carry one campaign's remembered counts into the next.");
    }

    [DataTestMethod]
    [DataRow("CampaignBehaviorDecl.Of(r => _cached ??= new HiddenHostsMemoryBehavior(a, b, c, d)),")]
    [DataRow("private static readonly HiddenHostsMemoryBehavior Shared = new HiddenHostsMemoryBehavior(a, b, c, d); CampaignBehaviorDecl.Of(r => Shared),")]
    [DataRow("CampaignBehaviorDecl.Of(r => new HiddenHostsMemoryBehavior(a, b, c, d)), var x = new HiddenHostsMemoryBehavior(a, b, c, d);")]
    [DataRow("CampaignBehaviorDecl.Of(r => r.Resolve<HiddenHostsMemoryBehavior>()),")]
    public void MemoryPredicate_RejectsACachedOrSharedConstruction(string mutant)
    {
        Assert.IsFalse(IsMemoryBuiltInlinePerCampaign(mutant), mutant);
    }

    [TestMethod]
    public void Module_BuildsThePlainSightBehaviorInlineAsAFactoryBody()
    {
        var src = RepoPaths.ReadSource(ModulePath, stripComments: true);

        Assert.IsTrue(IsPlainSightBuiltInlinePerCampaign(src),
            "HiddenHostsModule must build HiddenHostsPlainSightBehavior with new as a CampaignBehaviorDecl factory's body, exactly once.");
    }

    [DataTestMethod]
    [DataRow("CampaignBehaviorDecl.Of(r => _cached ??= new HiddenHostsPlainSightBehavior(a, b, c)),")]
    [DataRow("private static readonly HiddenHostsPlainSightBehavior Shared = new HiddenHostsPlainSightBehavior(a, b, c); CampaignBehaviorDecl.Of(r => Shared),")]
    [DataRow("CampaignBehaviorDecl.Of(r => r.Resolve<HiddenHostsPlainSightBehavior>()),")]
    public void PlainSightPredicate_RejectsACachedOrSharedConstruction(string mutant)
    {
        Assert.IsFalse(IsPlainSightBuiltInlinePerCampaign(mutant), mutant);
    }

    private static string[] RegisteredEvents(string file)
    {
        var src = RepoPaths.ReadSource("Main/Features/HiddenHosts/" + file, stripComments: true);
        return Regex.Matches(src, @"CampaignEvents\.(\w+)\.AddNonSerializedListener\(this, (\w+)\)")
            .Cast<Match>().Select(m => m.Groups[1].Value + "->" + m.Groups[2].Value).OrderBy(x => x).ToArray();
    }

    [TestMethod]
    public void MemoryBehavior_RegistersTheFiveCampaignEvents_AndNoOther()
    {
        CollectionAssert.AreEqual(new[]
        {
            "HourlyTickEvent->OnHourlyTick", "MobilePartyDestroyed->OnPartyDestroyed", "OnBeforeSaveEvent->OnBeforeSave",
            "PartyVisibilityChangedEvent->OnVisibilityChanged", "TickEvent->OnTick",
        }, RegisteredEvents("HiddenHostsMemoryBehavior.cs"));
    }

    [TestMethod]
    public void PlainSightBehavior_StampsOnTheTickAndOnEveryGameMenu_AndNothingElse()
    {
        CollectionAssert.AreEqual(new[] { "GameMenuOpened->OnMenuOpened", "TickEvent->OnTick" },
            RegisteredEvents("HiddenHostsPlainSightBehavior.cs"),
            "the tick never fires while a plain game menu stops time, so the menu event is what stamps a town or encounter visit");
    }

    [TestMethod]
    public void MemoryBehavior_ResetsTheServiceInItsConstructor()
    {
        var src = RepoPaths.ReadSource("Main/Features/HiddenHosts/HiddenHostsMemoryBehavior.cs", stripComments: true);
        var ctor = src.Substring(src.IndexOf("public HiddenHostsMemoryBehavior(", StringComparison.Ordinal));
        ctor = ctor.Substring(0, ctor.IndexOf("public override void RegisterEvents", StringComparison.Ordinal));

        StringAssert.Contains(ctor, "_service.ResetForNewSession();");
    }

    [TestMethod]
    public void Module_DeclaresTheThreeBehaviors_InOrder()
    {
        CollectionAssert.AreEqual(
            new[] { typeof(HiddenHostsBehavior), typeof(HiddenHostsMemoryBehavior), typeof(HiddenHostsPlainSightBehavior) },
            new HiddenHostsModule().CampaignBehaviors.Select(d => d.BehaviorType).ToArray());
    }

    [TestMethod]
    public void ProductionSource_NoOtherFileConstructsOrRegistersTheBehavior()
    {
        var offenders = RepoPaths.CSharpFiles()
            .Where(path => !IsFile(path, "HiddenHostsModule.cs") && !IsFile(path, "HiddenHostsBehavior.cs")
                           && !IsFile(path, "HiddenHostsMemoryBehavior.cs") && !IsFile(path, "HiddenHostsPlainSightBehavior.cs"))
            .Where(path => RepoPaths.MentionsOutsideComments(path, "HiddenHostsBehavior", wholeWord: true)
                           || RepoPaths.MentionsOutsideComments(path, "HiddenHostsMemoryBehavior", wholeWord: true)
                           || RepoPaths.MentionsOutsideComments(path, "HiddenHostsPlainSightBehavior", wholeWord: true))
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Only HiddenHostsModule may construct HiddenHostsBehavior; elsewhere could hand the campaign a shared instance: "
            + string.Join(", ", offenders));
    }

    [DataTestMethod]
    [DataRow("CampaignBehaviorDecl.Of(r => _cached ??= new HiddenHostsBehavior(a, b)),")]
    [DataRow("private static readonly HiddenHostsBehavior Shared = new HiddenHostsBehavior(a, b); CampaignBehaviorDecl.Of(r => Shared),")]
    [DataRow("CampaignBehaviorDecl.Of(r => Make()), static HiddenHostsBehavior Make() => _c ??= new HiddenHostsBehavior(a, b);")]
    [DataRow("CampaignBehaviorDecl.Of(r => new HiddenHostsBehavior(a, b)), var x = new HiddenHostsBehavior(a, b);")]
    [DataRow("CampaignBehaviorDecl.Of(r => r.Resolve<HiddenHostsBehavior>()),")]
    public void Predicate_RejectsACachedOrSharedConstruction(string mutant)
    {
        Assert.IsFalse(IsBuiltInlinePerCampaign(mutant), mutant);
    }

    [TestMethod]
    public void Predicate_AcceptsTheInlineConstructionAcrossLines()
    {
        Assert.IsTrue(IsBuiltInlinePerCampaign("CampaignBehaviorDecl.Of(r =>\n    new HiddenHostsBehavior(\n a, b)),"));
    }

    /// <summary>
    /// The service takes the scout seam (#847), which Far Watch's module registers as a mapping onto its store. The tests
    /// that resolve the service register that module, so the graph here is the production one.
    /// </summary>
    private static void RegisterTheScoutSeam(IContainer container) => new FarWatchModule().RegisterServices(container);

    [TestMethod]
    public void Module_InitializeStatics_HandsTheHooksTheServiceAndAdapter()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        RegisterTheScoutSeam(container);
        var module = new HiddenHostsModule();
        module.RegisterServices(container);
        try
        {
            module.InitializeStatics(container);

            Assert.AreSame(container.Resolve<HiddenHostsService>(), HiddenHostsCalls.Service);
            Assert.AreSame(container.Resolve<IPartyIntelAdapter>(), HiddenHostsCalls.Adapter);
        }
        finally
        {
            HiddenHostsCalls.Initialize(null, null);
        }
    }

    private static readonly string[] PatchClasses =
    {
        "PartyNameplateVM_RefreshBinding_HiddenHosts_Patch",
        "MobilePartyVisual_OnOpenEncyclopedia_HiddenHosts_Patch",
        "TooltipRefresherCollection_Subject_HiddenHosts_Patch",
        "PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch",
        "TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch",
        "TooltipRefresherCollection_AddEncounterParties_HiddenHosts_Patch",
        "PartyBaseHelper_GetPartySizeText_HiddenHosts_Patch",
        "CampaignUIHelper_GetPartyNameplateText_HiddenHosts_Patch",
        "MobileParty_GetBehaviorText_HiddenHosts_Patch",
        "DefaultEncounterModel_CanMainHeroDoParleyWithParty_HiddenHosts_Patch",
    };

    [TestMethod]
    public void EveryHook_CarriesThePatchCategoryConstant()
    {
        foreach (var hook in PatchClasses)
        {
            var src = RepoPaths.ReadSource($"Main/Features/HiddenHosts/Hooks/{hook}.cs", stripComments: true);

            StringAssert.Contains(src, "[HarmonyPatchCategory(HiddenHostsModule.PatchCategory)]", hook);
        }
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void EveryHarmonyPatchClassInTheFeatureNamespace_CarriesThePatchCategory()
    {
        // GetTypes() resolves every patch class's engine base types, so the module assemblies must be loaded first (run alone,
        // nothing else in the process has loaded them).
        if (!GameAssemblies.EnsureLoaded()) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var offenders = typeof(HiddenHostsModule).Assembly.GetTypes()
            .Where(t => t.Namespace == "TAOM.Features.HiddenHosts.Hooks" || t.Namespace == "TAOM.Features.HiddenHosts")
            .Where(t => t.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), inherit: false).Any())
            .Where(t => !t.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatchCategory), inherit: false)
                .Cast<HarmonyLib.HarmonyPatchCategory>().Any(a => a.info.category == HiddenHostsModule.PatchCategory))
            .Select(t => t.FullName)
            .ToList();

        Assert.AreEqual(0, offenders.Count, "a patch class outside Patch105_HiddenHosts would never be applied or gated: " + string.Join(", ", offenders));
        Assert.IsTrue(typeof(HiddenHostsModule).Assembly.GetTypes().Count(t => t.Namespace == "TAOM.Features.HiddenHosts.Hooks"
            && t.GetCustomAttributes(typeof(HarmonyLib.HarmonyPatch), false).Any()) >= PatchClasses.Length, "the scan must find the patch classes");
    }

    [TestMethod]
    public void Hooks_AreTheOnlyPatchClassesInTheFeatureFolder()
    {
        var patchFiles = Directory.GetFiles(RepoPaths.RepoPath("Main/Features/HiddenHosts/Hooks"), "*_Patch.cs")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(x => x)
            .ToArray();

        CollectionAssert.AreEqual(PatchClasses.OrderBy(x => x).ToArray(), patchFiles,
            "a patch class not listed here would escape the category pin; a listed one that is gone is a stale row");
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_RegistersTheServiceGraph_AndTheBehaviorResolves()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        RegisterTheScoutSeam(container);
        var module = new HiddenHostsModule();

        module.RegisterServices(container);

        var decl = module.CampaignBehaviors.Single(d => d.BehaviorType == typeof(HiddenHostsBehavior));
        Assert.IsInstanceOfType(decl.Create(container), typeof(HiddenHostsBehavior));
        Assert.AreNotSame(decl.Create(container), decl.Create(container), "a fresh behavior per campaign start");
        var memory = module.CampaignBehaviors.Single(d => d.BehaviorType == typeof(HiddenHostsMemoryBehavior));
        Assert.IsInstanceOfType(memory.Create(container), typeof(HiddenHostsMemoryBehavior));
        Assert.AreNotSame(memory.Create(container), memory.Create(container), "and a fresh memory behavior too");
        var plain = module.CampaignBehaviors.Single(d => d.BehaviorType == typeof(HiddenHostsPlainSightBehavior));
        Assert.AreNotSame(plain.Create(container), plain.Create(container), "and a fresh plain-sight behavior");
        Assert.IsInstanceOfType(container.Resolve<IHiddenHostsCampaignAdapter>(), typeof(HiddenHostsCampaignAdapter));
        Assert.AreSame(container.Resolve<HiddenHostsService>(), container.Resolve<HiddenHostsService>());
        Assert.IsInstanceOfType(container.Resolve<IPartyIntelAdapter>(), typeof(PartyIntelAdapter));
        Assert.IsInstanceOfType(container.Resolve<IHiddenHostsHintAdapter>(), typeof(HiddenHostsHintAdapter));
        Assert.IsInstanceOfType(container.Resolve<IHiddenHostsSettings>(), typeof(HiddenHostsSettingsProvider));
    }

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
}
