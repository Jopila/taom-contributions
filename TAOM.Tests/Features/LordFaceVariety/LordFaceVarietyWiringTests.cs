using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.LordFaceVariety;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LordFaceVariety;

/// <summary>
/// Wiring guard for Lord Face Variety (#952). The pass must run when a campaign is CREATED and must not run
/// when a save is loaded: existing saves keep the faces they have.
/// </summary>
[TestClass]
public class LordFaceVarietyWiringTests
{
    private const string BehaviorPath = "Main/Features/LordFaceVariety/LordFaceVarietyBehavior.cs";

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<LordFaceVarietyModule>().Count());
    }

    [TestMethod]
    public void Module_DeclaresTheBehavior()
    {
        var decls = new LordFaceVarietyModule().CampaignBehaviors;

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual(typeof(LordFaceVarietyBehavior), decls[0].BehaviorType);
    }

    [TestMethod]
    public void Module_RegistersTheServiceAsASingleton()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        new LordFaceVarietyModule().RegisterServices(container);

        Assert.AreSame(container.Resolve<LordFaceVarietyService>(), container.Resolve<LordFaceVarietyService>());
    }

    [TestMethod]
    public void Behavior_SubscribesOnceToTheEndOfNewGameCreationAndToNoLoadEvent()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        Assert.AreEqual(1, Regex.Matches(src, "AddNonSerializedListener").Count);
        StringAssert.Contains(src, "CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener");
        // The end event fires once, after follow-ups 0..99, on a new game only; the plain event fires before them.
        Assert.IsFalse(src.Contains("CampaignEvents.OnNewGameCreatedEvent"), "heroes made by follow-ups would be missed");
        Assert.IsFalse(src.Contains("Loaded"), "a loaded save keeps its faces (no OnGameLoaded or OnGameEarlyLoaded)");
        Assert.IsFalse(src.Contains("SessionLaunched"), "the pass belongs to campaign creation only");
        Assert.IsFalse(src.Contains("SessionStart"), "the pass belongs to campaign creation only");
    }

    [TestMethod]
    public void Behavior_CatchIsScopedToTheServiceRunInVary()
    {
        var src = RepoPaths.ReadSource(BehaviorPath, stripComments: true);

        StringAssert.Matches(src, new Regex(
            @"private void Vary\(\)\s*\{\s*try\s*\{\s*_service\.Run\(\);\s*\}\s*catch \(Exception ex\)"));
        Assert.AreEqual(1, Regex.Matches(src, @"catch \(").Count);
        Assert.AreEqual(1, Regex.Matches(src, @"_service\.Run\(").Count);
    }
}
