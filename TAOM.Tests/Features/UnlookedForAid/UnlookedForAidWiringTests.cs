using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.FireAndTribute;
using TAOM.Features.UnlookedForAid;
using TAOM.Features.UnlookedForAid.Models;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.UnlookedForAid;

/// <summary>Wiring guard for Help Unlooked-For (#872): the module line, its one model slot, and a model that is safe on the AI worker.</summary>
[TestClass]
public class UnlookedForAidWiringTests
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [TestMethod]
    public void FeatureModules_ListTheUnlookedForAidModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<UnlookedForAidModule>().Count());
    }

    [TestMethod]
    public void TheModule_IsNamedUnlookedForAid_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new UnlookedForAidModule();

        Assert.AreEqual("UnlookedForAid", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(0, module.PatchCategories.Count, "a model, not a Harmony patch");
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_DeclaresOneCampaignModel_ForTheMobilePartyAISlot()
    {
        var decl = new UnlookedForAidModule().GameModels.Single();

        Assert.AreEqual(ModelTarget.Campaign, decl.Target);
        Assert.AreEqual(typeof(MobilePartyAIModel), decl.SlotType);
        Assert.AreEqual(typeof(TaomMobilePartyAIModel), decl.ModelType);
        Assert.AreEqual(typeof(DefaultMobilePartyAIModel), decl.ModelType.BaseType,
            "extend the model SandBox installs: every other member must stay vanilla's");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void OnlyTheUnlookedForAidModule_FillsTheMobilePartyAISlot()
    {
        var owners = FeatureModules.All.Where(m => m.GameModels.Any(d => d.SlotType == typeof(MobilePartyAIModel))).ToList();

        Assert.AreEqual(1, owners.Count);
        Assert.IsInstanceOfType(owners[0], typeof(UnlookedForAidModule));
    }

    [TestMethod]
    public void TheModule_RegistersAServiceThatResolvesFromARealContainer()
    {
        using var container = new Container();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(RepoPaths.RepoPath("Main", "_Module", "ModuleData"));
        container.RegisterInstance(paths);
        container.RegisterInstance(Substitute.For<IModLogger>());
        new UnlookedForAidModule().RegisterServices(container);

        var service = container.Resolve<UnlookedForAidService>();

        Assert.AreSame(service, container.Resolve<UnlookedForAidService>(), "one shared instance");
        Assert.IsFalse(service.Decide(new SiegeAvoidanceFacts(true, true, false, false, 60f, 100f)));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheOverride_CallsBase_AndReadsNoSettingsAndNoIoC()
    {
        var method = typeof(TaomMobilePartyAIModel).GetMethod(nameof(TaomMobilePartyAIModel.ShouldConsiderAvoiding), All)!;
        var calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToArray();

        Assert.IsTrue(calls.Any(m => m.DeclaringType == typeof(DefaultMobilePartyAIModel) && m.Name == nameof(MobilePartyAIModel.ShouldConsiderAvoiding)),
            "vanilla's rule runs first");
        Assert.IsTrue(calls.Any(m => m.DeclaringType == typeof(UnlookedForAidService) && m.Name == nameof(UnlookedForAidService.Decide)));
        AssertNoThreadUnsafeReads(calls);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheAttackOverride_AsksTheHostsThenCallsBase_AndReadsNoSettingsAndNoIoC()
    {
        var method = typeof(TaomMobilePartyAIModel).GetMethod(nameof(TaomMobilePartyAIModel.ShouldConsiderAttacking), All)!;
        var calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToArray();

        Assert.IsTrue(calls.Any(m => m.DeclaringType == typeof(DefaultMobilePartyAIModel) && m.Name == nameof(MobilePartyAIModel.ShouldConsiderAttacking)));
        Assert.IsTrue(calls.Any(m => m.Name == "OwnSideMeetsHost"), "the own-side check runs on the attack path");
        AssertNoThreadUnsafeReads(calls);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheOwnSideHelpers_ReadNoSettingsAndNoIoC()
    {
        foreach (var name in new[] { "OwnSideMeetsHost", "KingdomIdOf" })
        {
            var method = typeof(TaomMobilePartyAIModel).GetMethod(name, All)!;
            AssertNoThreadUnsafeReads(IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToArray());
        }
    }

    [TestMethod]
    public void TheHostsOwnSideQuery_ReadsNoSettingsAndNoIoC()
    {
        var type = typeof(FireAndTributeService);
        foreach (var method in new MethodBase[] { type.GetProperty(nameof(IRaidHostOwnSideQuery.AnyHost))!.GetMethod!, type.GetMethod(nameof(IRaidHostOwnSideQuery.IsOwnSide))! })
            AssertNoThreadUnsafeReads(IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToArray());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheFactsReader_ReadsNoSettingsAndNoIoC()
    {
        foreach (var method in typeof(SiegeAvoidanceFactsReader).GetMethods(All))
            AssertNoThreadUnsafeReads(IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToArray());
    }

    [TestMethod]
    public void TheService_ReadsNoSettingsInstanceAndNoIoC()
    {
        foreach (var method in typeof(UnlookedForAidService).GetMethods(All).Cast<MethodBase>().Concat(typeof(UnlookedForAidService).GetConstructors(All)))
            AssertNoThreadUnsafeReads(IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToArray());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModelDeclaration_BuildsTheModelOverTheSharedService()
    {
        using var container = new Container();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(RepoPaths.RepoPath("Main", "_Module", "ModuleData"));
        container.RegisterInstance(paths);
        container.RegisterInstance(Substitute.For<IModLogger>());
        // Fire and Tribute registers the hosts' own-side view in its own module.
        container.RegisterInstance(Substitute.For<IRaidHostOwnSideQuery>());
        var module = new UnlookedForAidModule();
        module.RegisterServices(container);

        var model = module.GameModels[0].Create(container);

        Assert.IsInstanceOfType(model, typeof(TaomMobilePartyAIModel));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TaomMobilePartyAIModel_OverridesOnlyTheAttackAndAvoidChecks()
    {
        var overridden = typeof(TaomMobilePartyAIModel)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        CollectionAssert.AreEqual(
            new[] { nameof(MobilePartyAIModel.ShouldConsiderAttacking), nameof(MobilePartyAIModel.ShouldConsiderAvoiding) }, overridden);
        Assert.AreEqual(typeof(DefaultMobilePartyAIModel), typeof(TaomMobilePartyAIModel).BaseType);
    }

    // A bare uninitialized MobileParty: IsLordParty has a private setter (v1.5.5 MobileParty.cs:1270). With no besieger
    // camp, no settlement and no campaign, the reader must read neither strengths nor MobileParty.MainParty. A party with an
    // Army, an AttachedTo or a BesiegerCamp cannot be built without a running campaign (their setters call game code, and
    // the player-led check reads Campaign.Current), so those paths are left to the in-game check.
    private static MobileParty Party(bool lord)
    {
        var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        typeof(MobileParty).GetProperty(nameof(MobileParty.IsLordParty))!.SetValue(party, lord);
        return party;
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [TestCategory("RequiresGame")]
    public void TheFactsReader_ATargetOutsideAnySiege_ReportsNoSiegeAndUnreadStrengths(bool lord)
    {
        var facts = SiegeAvoidanceFactsReader.Read(Party(lord), Party(false));

        Assert.AreEqual(lord, facts.IsLordParty);
        Assert.IsFalse(facts.TargetIsBesieger);
        Assert.IsFalse(facts.TargetInBesiegedSettlement);
        Assert.IsFalse(facts.PlayerLedSiege);
        Assert.IsTrue(float.IsNaN(facts.OwnStrength), "unread strengths are NaN, never 0");
        Assert.IsTrue(float.IsNaN(facts.CampStrength));
    }

    private static void AssertNoThreadUnsafeReads(MethodBase[] calls)
    {
        Assert.IsFalse(calls.Any(m => m.DeclaringType != null && m.DeclaringType.IsAssignableFrom(typeof(TaomSettings)) && m.Name == "get_Instance"),
            "the AI worker thread must not walk MCM's settings containers");
        Assert.IsFalse(calls.Any(m => m.DeclaringType?.Name == "IoC"), "no IoC resolve on the AI worker thread");
        Assert.IsFalse(calls.Any(m => m.DeclaringType?.Name == "IModLogger"), "no logging on the AI worker thread");
    }

    [DataTestMethod]
    [DataRow("Main", "IoC.cs")]
    [DataRow("Main", "SubModule.cs")]
    public void TheSingleOwnerFiles_NameNoUnlookedForAidType(string folder, string file)
    {
        var text = File.ReadAllText(RepoPaths.RepoPath(folder, file));

        Assert.IsFalse(text.Contains("UnlookedForAid") || text.Contains("TaomMobilePartyAIModel"),
            file + " is single-owner: the feature registers through its module and FeatureModules.cs only");
    }
}
