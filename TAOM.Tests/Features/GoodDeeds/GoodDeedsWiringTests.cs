using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.GoodDeeds;
using TAOM.Features.GoodDeeds.Hooks;
using TAOM.Features.GoodDeeds.Models;
using TAOM.Features.Relations;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.GoodDeeds;

/// <summary>
/// Wiring guards for Good Deeds. Each piece fails silently when unwired: drop the module line and nothing runs; drop the
/// model declaration and a donation keeps vanilla's value; leave a dependency unregistered and the first release, or the first
/// tournament win, throws inside a handler's catch. The service holds no per-campaign state, so the behavior built for each
/// campaign is the only thing a campaign needs fresh.
/// </summary>
[TestClass]
public class GoodDeedsWiringTests
{
    private static Container BuildContainer()
    {
        var container = new Container();
        new GoodDeedsModule().RegisterServices(container);
        new RelationsModule().RegisterServices(container); // owns the relation seam
        // What the module takes from other features, which IoC.Configure registers for every game.
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        return container;
    }

    [TestMethod]
    public void FeatureModules_ListTheGoodDeedsModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<GoodDeedsModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.GoodDeeds.GoodDeedsModule()"));
    }

    [TestMethod]
    public void TheSingleOwnerFiles_AreNotEditedForThisFeature()
    {
        Assert.IsFalse(RepoPaths.ReadSource("Main/IoC.cs", stripComments: true).Contains("GoodDeeds"));
        Assert.IsFalse(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true).Contains("GoodDeeds"));
    }

    [TestMethod]
    public void TheModule_IsNotParked_OwnsNoSaveData_AndPatchesNothing()
    {
        var module = new GoodDeedsModule();

        Assert.AreEqual("GoodDeeds", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "nothing is persisted");
        Assert.AreEqual(0, module.PatchCategories.Count, "no Harmony patch: two events and a model");
        Assert.AreEqual(0, module.MissionBehaviors.Count);
    }

    [TestMethod]
    public void TheModule_DeclaresThePrisonerDonationModelForTheCampaign()
    {
        var models = new GoodDeedsModule().GameModels.ToList();

        Assert.AreEqual(1, models.Count);
        Assert.AreEqual(typeof(PrisonerDonationModel), models[0].SlotType);
        Assert.AreEqual(typeof(TaomPrisonerDonationModel), models[0].ModelType);
        Assert.AreEqual(ModelTarget.Campaign, models[0].Target);
    }

    [TestMethod]
    public void TheModule_AddsOneCampaignBehavior_BuiltWithNew()
    {
        var module = new GoodDeedsModule();
        var src = RepoPaths.ReadSource("Main/Features/GoodDeeds/GoodDeedsModule.cs", stripComments: true);

        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(GoodDeedsBehavior), module.CampaignBehaviors[0].BehaviorType);
        Assert.IsTrue(src.Contains("new GoodDeedsBehavior("), "the behavior must be built per campaign");
        Assert.IsFalse(src.Contains("Resolve<GoodDeedsBehavior>"));
    }

    [TestMethod]
    public void EveryServiceTheModuleDeclares_ResolvesFromItsOwnRegistrations()
    {
        using var container = BuildContainer();

        Assert.IsNotNull(container.Resolve<IGoodDeedsSettingsProvider>());
        Assert.IsNotNull(container.Resolve<IGoodDeedsHeroAdapter>());
        Assert.IsNotNull(container.Resolve<IGoodDeedsNoticeAdapter>());
        Assert.IsNotNull(container.Resolve<GoodDeedsService>());
    }

    [TestMethod]
    public void TheService_IsOneSharedInstance_ForTheBehaviorAndTheModel()
    {
        using var container = BuildContainer();

        Assert.AreSame(container.Resolve<GoodDeedsService>(), container.Resolve<GoodDeedsService>());
    }

    [TestMethod]
    public void TheModule_DoesNotRegisterTheRelationSeamAgain()
    {
        var src = RepoPaths.ReadSource("Main/Features/GoodDeeds/GoodDeedsModule.cs", stripComments: true);

        Assert.IsFalse(src.Contains("IRelationGrantService,"), "RelationsModule owns the seam; a second default makes every resolve throw");
        Assert.IsFalse(src.Contains("IHeroRelationAdapter,"));
        Assert.IsFalse(src.Contains("IAlignmentService,"));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheBehaviorDeclaration_BuildsAFreshBehaviorForEachCampaign()
    {
        using var container = BuildContainer();
        var decl = new GoodDeedsModule().CampaignBehaviors[0];

        var first = decl.Create(container);
        var second = decl.Create(container);

        Assert.IsInstanceOfType(first, typeof(GoodDeedsBehavior));
        Assert.AreNotSame(first, second, "a campaign registers its behaviors' events on the instance, so each campaign needs its own");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModelDeclaration_BuildsTheModelOverTheSharedService()
    {
        using var container = BuildContainer();

        var model = new GoodDeedsModule().GameModels[0].Create(container);

        Assert.IsInstanceOfType(model, typeof(TaomPrisonerDonationModel));
    }

    // --- the model ---------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void TaomPrisonerDonationModel_OverridesOnlyTheHeroRelationMethod()
    {
        var overridden = typeof(TaomPrisonerDonationModel)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToList();

        CollectionAssert.AreEqual(new[] { "CalculateRelationGainAfterHeroPrisonerDonate" }, overridden);
        Assert.AreEqual(typeof(DefaultPrisonerDonationModel), typeof(TaomPrisonerDonationModel).BaseType);
    }

    [TestMethod]
    public void TaomPrisonerDonationModel_BodyIsOneBoundaryConversionAndOneServiceCall()
    {
        // gamemodels.md rule 4: no if, loop or switch in an override body.
        var src = RepoPaths.ReadSource("Main/Features/GoodDeeds/Models/TaomPrisonerDonationModel.cs", stripComments: true);

        Assert.IsFalse(Regex.IsMatch(src, @"\b(if|foreach|for|while|switch)\b\s*\("), "the model carries no decision");
        StringAssert.Contains(src, "_service.DonationRelation(");
        StringAssert.Contains(src, "base.CalculateRelationGainAfterHeroPrisonerDonate(");
    }

    // A type fills the slot when its base list names PrisonerDonationModel or DefaultPrisonerDonationModel. A file that only reads
    // Campaign.Current.Models.PrisonerDonationModel (a courtesy, a report) fills nothing and must not fail the scan.
    private static bool FillsThePrisonerDonationSlot(string strippedSource) =>
        Regex.IsMatch(strippedSource, @"\bclass\s+\w+\s*(?:<[^>]*>)?\s*:\s*[^{;]*\b(?:Default)?PrisonerDonationModel\b");

    [TestMethod]
    public void NoOtherTaomModelFillsThePrisonerDonationSlot()
    {
        // A source scan, not reflection: GetTypes() on TAOM's assembly needs the game. Two models on one slot would make the
        // later one win and drop the other's rule (gamemodels.md rule 7). Each file is read once.
        var fillers = RepoPaths.CSharpFiles()
            .Where(f => FillsThePrisonerDonationSlot(RepoPaths.StripComments(File.ReadAllText(f))))
            .Select(Path.GetFileName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        CollectionAssert.AreEqual(new[] { "TaomPrisonerDonationModel.cs" }, fillers, "the slot is filled by exactly one file");
    }

    [DataTestMethod]
    [DataRow("public class TaomX : DefaultPrisonerDonationModel { }", true)]
    [DataRow("public class TaomX : TaleWorlds.CampaignSystem.ComponentInterfaces.PrisonerDonationModel { }", true)]
    [DataRow("public sealed class TaomX<T> : IFoo, PrisonerDonationModel\n{\n}", true)]
    [DataRow("public class Reader { void F() { var m = Campaign.Current.Models.PrisonerDonationModel; } }", false)]
    [DataRow("public class Reader : CampaignBehaviorBase\n{\n void F() { var m = Campaign.Current.Models.PrisonerDonationModel; }\n}", false)]
    [DataRow("public class Other : DefaultPartyWageModel { }", false)]
    public void FillsThePrisonerDonationSlot_OnlyAClassDerivingFromTheModel_Matches(string source, bool expected)
    {
        Assert.AreEqual(expected, FillsThePrisonerDonationSlot(source));
    }

    // --- the tournament listener ----------------------------------------------------------------------------------------

    [TestMethod]
    public void TheTournamentListener_IsGoodDeedsOwn_AndItsBodyIsInsideATry()
    {
        // An MbEvent invokes its listeners with no try: a fault would reach the engine's tournament code. The listener sits at the
        // head of the list (newest first), so it runs before TournamentRewards' and vanilla's; it writes only relation, which they never read.
        var src = RepoPaths.ReadSource("Main/Features/GoodDeeds/Hooks/GoodDeedsBehavior.cs", stripComments: true);

        StringAssert.Contains(src, "CampaignEvents.TournamentFinished.AddNonSerializedListener(this, OnTournamentFinished)");
        var handler = src.IndexOf("internal void HandleTournament(", StringComparison.Ordinal);
        Assert.IsTrue(handler >= 0, "HandleTournament not found");
        var tryAt = src.IndexOf("try", handler, StringComparison.Ordinal);
        var call = src.IndexOf("_service.OnTournamentFinished(", handler, StringComparison.Ordinal);
        var catchAt = src.IndexOf("catch (Exception", handler, StringComparison.Ordinal);
        Assert.IsTrue(tryAt > handler && call > tryAt && catchAt > call, "the service call must sit inside the try");
    }

    [TestMethod]
    public void TournamentRewards_DoesNotReferenceGoodDeeds()
    {
        // The step is Good Deeds' own listener; TournamentRewards keeps its single listener, its order argument and its service
        // shape untouched.
        foreach (var file in new[] { "TournamentRewardsBehavior", "TournamentRewardsModule" })
        {
            var src = RepoPaths.ReadSource($"Main/Features/TournamentRewards/{file}.cs", stripComments: true);
            Assert.IsFalse(src.Contains("GoodDeeds"), file + " must not reference Good Deeds");
        }
    }

    [TestMethod]
    public void GoodDeeds_ContainsNoHarmonyPatch()
    {
        // The release is a campaign event and the donation a model; a Harmony patch here would bypass the seam.
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "GoodDeeds"), "*.cs", SearchOption.AllDirectories);

        Assert.IsTrue(files.Length > 0);
        foreach (var file in files)
            Assert.IsFalse(RepoPaths.MentionsOutsideComments(file, "HarmonyPatch"), file + " declares a Harmony patch");
    }
}
