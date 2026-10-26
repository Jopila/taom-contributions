using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.KeepWardens;
using TAOM.Features.KeepWardens.Models;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.KeepWardens;

/// <summary>Wiring guard for Keep Wardens (#888): the module line, its model slots, its one behavior, and its patch and save footprint.</summary>
[TestClass]
public class KeepWardensWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheKeepWardensModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<KeepWardensModule>().Count());
    }

    [TestMethod]
    public void TheModule_IsNamedKeepWardens_AndIsNotParked_AndOwnsNoSaveData()
    {
        var module = new KeepWardensModule();

        Assert.AreEqual("KeepWardens", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData, "the patrol parties are vanilla's; this module persists nothing");
    }

    [TestMethod]
    public void TheModule_DeclaresNoPatchCategory_AndExactlyTheCastleGuardHouseUpgradeBehavior()
    {
        var module = new KeepWardensModule();

        Assert.AreEqual(0, module.PatchCategories.Count, "vanilla's PatrolPartiesCampaignBehavior already queues, spawns and refills");
        Assert.AreEqual(1, module.CampaignBehaviors.Count, "one listener: the castle twin of vanilla's town Guard House upgrade refresh");
        Assert.AreEqual(typeof(CastleGuardHouseUpgradeBehavior), module.CampaignBehaviors[0].BehaviorType);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_DeclaresThreeCampaignModels_EachExtendingTheDefaultSandBoxModel()
    {
        var decls = new KeepWardensModule().GameModels;

        Assert.AreEqual(3, decls.Count, "patrol, security and building score");
        Assert.IsTrue(decls.All(d => d.Target == ModelTarget.Campaign), "patrol parties and fief building exist only in a campaign");
        AssertDecl(decls, typeof(SettlementPatrolModel), typeof(TaomSettlementPatrolModel), typeof(DefaultSettlementPatrolModel));
        AssertDecl(decls, typeof(SettlementSecurityModel), typeof(TaomSettlementSecurityModel), typeof(DefaultSettlementSecurityModel));
        AssertDecl(decls, typeof(BuildingScoreCalculationModel), typeof(TaomBuildingScoreModel), typeof(DefaultBuildingScoreCalculationModel));
    }

    private static void AssertDecl(System.Collections.Generic.IReadOnlyList<GameModelDecl> decls, System.Type slot, System.Type model, System.Type defaultModel)
    {
        var decl = decls.Single(d => d.SlotType == slot);
        Assert.AreEqual(model, decl.ModelType);
        Assert.AreEqual(defaultModel, decl.ModelType.BaseType, "extend the model SandBox installs: every answer not changed here stays vanilla's");
    }

    [TestMethod]
    public void NoOverrideBody_InTheSecurityAndBuildModels_HoldsAnIf_AndEachOverridesExactlyItsMethods()
    {
        var security = ReadSource("Main/Features/KeepWardens/Models/TaomSettlementSecurityModel.cs", stripComments: true);
        var build = ReadSource("Main/Features/KeepWardens/Models/TaomBuildingScoreModel.cs", stripComments: true);

        foreach (var src in new[] { security, build })
            Assert.IsFalse(Regex.IsMatch(src, @"\b(if|switch|foreach|for|while)\s*\("), "an override body must hold no branch or loop");
        Assert.AreEqual(1, Regex.Matches(security, @"\bpublic override\b").Count, "only CalculateSecurityChange");
        Assert.AreEqual(1, Regex.Matches(build, @"\bpublic override\b").Count, "only GetNextBuilding");
    }

    [TestMethod]
    public void TheSecurityModel_CallsBaseFirst_AndLabelsTheLineWithVanillasPatrolPartiesText()
    {
        // Base builds the whole number (and vanilla's own town patrol line); the castle line is added after it, never in place of it.
        var src = ReadSource("Main/Features/KeepWardens/Models/TaomSettlementSecurityModel.cs", stripComments: true);
        var start = src.IndexOf("public override ExplainedNumber CalculateSecurityChange(", System.StringComparison.Ordinal);
        Assert.AreNotEqual(-1, start, "the model must override CalculateSecurityChange");
        var body = src.Substring(start, src.IndexOf(';', start) - start);

        var wrap = body.IndexOf("AddCastlePatrolSecurity(", System.StringComparison.Ordinal);
        var baseCall = body.IndexOf("base.CalculateSecurityChange(town, includeDescriptions)", System.StringComparison.Ordinal);
        Assert.IsTrue(wrap >= 0 && wrap < baseCall, "the base call must be the wrapper's argument, so it runs first");
        StringAssert.Contains(src, "GameTexts.FindText(\"str_patrol_parties\")");
    }

    [TestMethod]
    public void TheBuildModel_PicksTheGuardHouseOnlyWhenTheServiceSaysSo_AndFallsBackToTheBaseGetNextBuilding()
    {
        var src = ReadSource("Main/Features/KeepWardens/Models/TaomBuildingScoreModel.cs", stripComments: true);
        var start = src.IndexOf("public override Building? GetNextBuilding(", System.StringComparison.Ordinal);
        Assert.AreNotEqual(-1, start, "the model must override GetNextBuilding");
        var body = src.Substring(start, src.IndexOf(';', start) - start);

        var decide = body.IndexOf("_service.PicksGuardHouse(_adapter.Read(town))", System.StringComparison.Ordinal);
        var pick = body.IndexOf("? _adapter.GuardHouse(town)", System.StringComparison.Ordinal);
        var fallback = body.IndexOf(": base.GetNextBuilding(town)", System.StringComparison.Ordinal);
        Assert.IsTrue(decide >= 0 && decide < pick && pick < fallback,
            "the service decides first, the Guard House is the picked branch, vanilla's random pick is the fallback");
        Assert.IsFalse(src.Contains("GuardHouseIf"), "the adapter has one lookup, GuardHouse; no conditional twin");
        Assert.IsFalse(src.Contains("GetNextDailyBuilding"), "the daily project pick stays vanilla's");
    }

    [TestMethod]
    public void NoOverrideBody_InTheModel_HoldsAnIf()
    {
        // gamemodels.md rule 4: the service decides, an override is one expression (castle branch first, base otherwise).
        var src = ReadSource("Main/Features/KeepWardens/Models/TaomSettlementPatrolModel.cs", stripComments: true);

        Assert.IsFalse(Regex.IsMatch(src, @"\b(if|switch|foreach|for|while)\s*\("), "an override body must hold no branch or loop");
        Assert.AreEqual(3, Regex.Matches(src, @"\bpublic override\b").Count, "exactly the three SettlementPatrolModel methods");
    }

    [TestMethod]
    public void TheModel_AnswersTheCastleBranchFirst_AndFallsBackToBase()
    {
        // Vanilla's GetPatrolPartySpawnDuration dereferences a null Guard House for a castle, so base may never run first.
        var src = ReadSource("Main/Features/KeepWardens/Models/TaomSettlementPatrolModel.cs", stripComments: true);

        foreach (var method in new[] { "CanSettlementHavePatrolParties", "GetPartyTemplateForPatrolParty", "GetPatrolPartySpawnDuration" })
        {
            var start = src.IndexOf(method, System.StringComparison.Ordinal);
            Assert.AreNotEqual(-1, start, method + " must be overridden");
            var body = src.Substring(start, src.IndexOf(';', start) - start);
            var plan = body.IndexOf("Plan(", System.StringComparison.Ordinal);
            Assert.AreNotEqual(-1, plan, method + " must ask the castle plan");
            Assert.IsTrue(plan < body.IndexOf("base." + method, System.StringComparison.Ordinal),
                method + " must ask the castle plan before calling base");
        }
    }

    [TestMethod]
    public void TheModel_NamesTheTemplate_ByStrength_NotByAllowed()
    {
        // A refused castle still gets its template: vanilla's hourly refill asks for a live patrol's template without asking
        // whether the settlement may have one, and base returns null for a castle.
        var src = ReadSource("Main/Features/KeepWardens/Models/TaomSettlementPatrolModel.cs", stripComments: true);
        var start = src.IndexOf("GetPartyTemplateForPatrolParty", System.StringComparison.Ordinal);
        var body = src.Substring(start, src.IndexOf(';', start) - start);

        StringAssert.Contains(body, "Strength: > 0");
        Assert.IsFalse(body.Contains("Allowed"), "the template does not honour the switch, the level or the owner");
    }

    [TestMethod]
    public void TheModuleRegistration_ResolvesTheSizeHook_AndTheService()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IModLogger>());
        new KeepWardensModule().RegisterServices(container);

        var errors = container.Validate(typeof(ICastlePatrolSizeHook), typeof(CastlePatrolService), typeof(IGuardHouseAdapter), typeof(ICastlePatrolAdapter));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }
}
