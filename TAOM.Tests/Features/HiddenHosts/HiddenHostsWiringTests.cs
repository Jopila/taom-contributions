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
using TAOM.Features.HiddenHosts;
using TAOM.Tests.Infrastructure;

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

    private static bool IsBuiltInlinePerCampaign(string source) =>
        InlinePerCampaign.Matches(source).Count == 1 && AnyConstruction.Matches(source).Count == 1;

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
    public void ProductionSource_NoOtherFileConstructsOrRegistersTheBehavior()
    {
        var offenders = Directory.EnumerateFiles(RepoPaths.RepoPath("Main"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsFile(path, "HiddenHostsModule.cs") && !IsFile(path, "HiddenHostsBehavior.cs"))
            .Where(path => RepoPaths.MentionsOutsideComments(path, "HiddenHostsBehavior", wholeWord: true))
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

    [TestMethod]
    public void Module_InitializeStatics_HandsTheHooksTheServiceAndAdapter()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
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

    [DataTestMethod]
    [DataRow("PartyNameplateVM_RefreshBinding_HiddenHosts_Patch")]
    [DataRow("MobilePartyVisual_OnOpenEncyclopedia_HiddenHosts_Patch")]
    public void Hook_CarriesThePatchCategoryConstant(string hook)
    {
        var src = RepoPaths.ReadSource($"Main/Features/HiddenHosts/Hooks/{hook}.cs", stripComments: true);

        StringAssert.Contains(src, "[HarmonyPatchCategory(HiddenHostsModule.PatchCategory)]");
    }

    [TestMethod]
    public void Hooks_AreTheOnlyPatchClassesInTheFeatureFolder()
    {
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main/Features/HiddenHosts/Hooks"), "*.cs");

        Assert.AreEqual(2, files.Length, "the nameplate prefix and the encyclopedia prefix");
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_RegistersTheServiceGraph_AndTheBehaviorResolves()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        var module = new HiddenHostsModule();

        module.RegisterServices(container);

        var decl = module.CampaignBehaviors.Single();
        Assert.AreEqual(typeof(HiddenHostsBehavior), decl.BehaviorType);
        Assert.IsInstanceOfType(decl.Create(container), typeof(HiddenHostsBehavior));
        Assert.AreNotSame(decl.Create(container), decl.Create(container), "a fresh behavior per campaign start");
        Assert.AreSame(container.Resolve<HiddenHostsService>(), container.Resolve<HiddenHostsService>());
        Assert.IsInstanceOfType(container.Resolve<IPartyIntelAdapter>(), typeof(PartyIntelAdapter));
        Assert.IsInstanceOfType(container.Resolve<IHiddenHostsHintAdapter>(), typeof(HiddenHostsHintAdapter));
        Assert.IsInstanceOfType(container.Resolve<IHiddenHostsSettings>(), typeof(HiddenHostsSettingsProvider));
    }

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);
}
