using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.KeepWardens;
using TAOM.Features.KeepWardens.Models;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.KeepWardens;

/// <summary>Wiring guard for Keep Wardens (#888): the module line, its one model slot, and its (empty) behavior, patch and save footprint.</summary>
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
    public void TheModule_DeclaresNoPatchCategoryAndNoBehavior()
    {
        var module = new KeepWardensModule();

        Assert.AreEqual(0, module.PatchCategories.Count, "vanilla's PatrolPartiesCampaignBehavior already queues, spawns and refills");
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheModule_DeclaresOneCampaignModel_ExtendingTheDefaultSettlementPatrolModel()
    {
        var decl = new KeepWardensModule().GameModels.Single();

        Assert.AreEqual(ModelTarget.Campaign, decl.Target, "patrol parties exist only in a campaign");
        Assert.AreEqual(typeof(SettlementPatrolModel), decl.SlotType);
        Assert.AreEqual(typeof(TaomSettlementPatrolModel), decl.ModelType);
        Assert.AreEqual(typeof(DefaultSettlementPatrolModel), decl.ModelType.BaseType,
            "extend the model SandBox installs: towns and every refusal must keep its answer");
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

        var errors = container.Validate(typeof(ICastlePatrolSizeHook), typeof(CastlePatrolService));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
    }
}
