using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.FieldCamp;
using TAOM.Features.Provender;
using TAOM.Features.Provender.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Provender;

/// <summary>
/// Wiring guards for the Provender-master. The module owns save data (the two orders), so a dependency its behavior cannot
/// resolve fails the campaign start closed. The behavior is built with <c>new</c> for every campaign, so each campaign starts
/// with both orders off: a container singleton is constructed once per process. The dialog tree is pinned from the source,
/// because a CampaignGameStarter cannot be built outside the game.
/// </summary>
[TestClass]
public class ProvenderWiringTests
{
    private const string Root = "Main/Features/Provender/";

    [TestMethod]
    public void FeatureModules_ListTheProvenderModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<ProvenderModule>().Count());
        StringAssert.Contains(RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true), "new Features.Provender.ProvenderModule()");
    }

    [TestMethod]
    public void TheSingleOwnerFiles_AreNotEditedForThisFeature()
    {
        Assert.IsFalse(RepoPaths.ReadSource("Main/IoC.cs", stripComments: true).Contains("Provender"));
        Assert.IsFalse(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true).Contains("Provender"));
    }

    [TestMethod]
    public void Module_OwnsSaveData_IsNotParked_AndAddsNoPatchModelOrMissionBehavior()
    {
        var module = new ProvenderModule();

        Assert.AreEqual("Provender", module.Id);
        Assert.IsTrue(module.OwnsSaveData, "the orders are saved in SyncData");
        Assert.IsNull(module.ParkedReason);
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(ProvenderBehavior), module.CampaignBehaviors[0].BehaviorType);
    }

    [TestMethod]
    public void Module_BuildsTheBehaviorWithNew_NeverFromTheContainer()
    {
        var src = RepoPaths.ReadSource(Root + "ProvenderModule.cs", stripComments: true);

        StringAssert.Contains(src, "new ProvenderBehavior(");
        Assert.IsFalse(src.Contains("Resolve<ProvenderBehavior>"));
    }

    [TestMethod]
    public void Behavior_OwnsTheOrdersThroughItsFlow_AndNeverResetsThemFromSessionLaunched()
    {
        var src = RepoPaths.ReadSource(Root + "Hooks/ProvenderBehavior.cs", stripComments: true);
        var flow = RepoPaths.ReadSource(Root + "ProvenderConversationFlow.cs", stripComments: true);

        StringAssert.Contains(src, "_flow = new ProvenderConversationFlow(");
        Assert.IsTrue(Regex.IsMatch(flow, @"ProvenderOrders\s+Orders\s*\{\s*get;\s*\}\s*=\s*new\s*(ProvenderOrders)?\s*\("));
        StringAssert.Contains(src, "AutoFoodKey = \"_taom_provender_autoFood\"");
        StringAssert.Contains(src, "SlaughterKey = \"_taom_provender_slaughter\"");
        var launched = Regex.Match(src, @"void OnSessionLaunched\([^)]*\)\s*=>(.*?);", RegexOptions.Singleline);
        Assert.IsTrue(launched.Success, "OnSessionLaunched not found");
        Assert.AreEqual("ProvenderDialogLines.Register(starter, _flow)", launched.Groups[1].Value.Trim());
    }

    [TestMethod]
    public void Behavior_RunsTheOrdersOnlyForTheMainParty()
    {
        var src = RepoPaths.ReadSource(Root + "Hooks/ProvenderBehavior.cs", stripComments: true);

        Assert.AreEqual(2, Regex.Matches(src, @"party == MobileParty\.MainParty").Count, "both settlement handlers check the main party");
        StringAssert.Contains(src, "CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered)");
        StringAssert.Contains(src, "CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft)");
        StringAssert.Contains(src, "CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick)");
    }

    [TestMethod]
    public void EntryPoints_StayUnder150Lines()
    {
        foreach (var file in new[] { "Hooks/ProvenderBehavior.cs", "Hooks/ProvenderDialogLines.cs" })
        {
            var lines = RepoPaths.ReadSource(Root + file).Split('\n').Length;
            Assert.IsTrue(lines < 150, $"{file} is {lines} lines (ADR-002)");
        }
    }

    [TestMethod]
    public void DialogLines_UseProvenderIds_EnterFromHeroMainOptions_AndExposeTheHubToken()
    {
        var src = RepoPaths.ReadSource(Root + "Hooks/ProvenderDialogLines.cs", stripComments: true);

        var ids = Regex.Matches(src, @"Add(?:Player|Dialog)Line\(\s*""([^""]+)""").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        var helperIds = Regex.Matches(src, @"MoraleLine\(s, f, ""([^""]+)""").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        ids.AddRange(helperIds);
        Assert.IsTrue(ids.Count >= 15, "expected the whole tree, found " + ids.Count);
        Assert.IsTrue(ids.All(id => id.StartsWith("taom_provender_")), string.Join(", ", ids.Where(i => !i.StartsWith("taom_provender_"))));
        Assert.AreEqual(ids.Count, ids.Distinct().Count(), "a duplicate line id");
        Assert.AreEqual(4, helperIds.Count, "two pay lines and two drink lines (from the stores, or with a priced purchase)");
        StringAssert.Contains(src, "\"taom_provender_entry\", \"hero_main_options\"");
        StringAssert.Contains(src, "HubToken = \"taom_provender_orders\"");
        Assert.IsTrue(Regex.IsMatch(src, @"""taom_provender_done"", HubToken, ""lord_pretalk"",.*?,\s*1\s*\)", RegexOptions.Singleline),
            "\"That is all.\" sits last at priority 1 and leaves to lord_pretalk");
    }

    [TestMethod]
    public void TheMoraleLines_ShareNoTextVariable()
    {
        // The conversation view resolves every option's text only after all the conditions have run
        // (MissionConversationVM builds its items from CurOptions), so a variable two lines share shows the last line's value.
        var src = RepoPaths.ReadSource(Root + "Hooks/ProvenderDialogLines.cs", stripComments: true);
        var lines = Regex.Matches(src, @"MoraleLine\(s, f, ""([^""]+)"", MoraleOption\.\w+,\s*""([^""]+)""").Cast<Match>().ToList();
        Assert.AreEqual(4, lines.Count);

        var owner = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var line in lines)
        {
            var vars = Regex.Matches(line.Groups[2].Value, @"\{([A-Z_]+)\}").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.IsTrue(vars.Count > 0, line.Groups[1].Value + " names no variable");
            foreach (var name in vars)
            {
                Assert.IsFalse(owner.TryGetValue(name, out var other) && other != line.Groups[1].Value,
                    $"{{{name}}} is used by both {other} and {line.Groups[1].Value}");
                owner[name] = line.Groups[1].Value;
                StringAssert.Contains(src, $"SetTextVariable(\"{name}\"", "every variable a line shows is set");
            }
        }
    }

    [TestMethod]
    public void TheAdapter_BuysWithOneSellCallPerType_SoThePlayerSeesOneGoldNotice()
    {
        // Each SellItemsAction call the player pays shows a gold notice and a coin sound (DefaultNotificationsCampaignBehavior),
        // so a loop of one-unit calls would show one notice per unit.
        var src = RepoPaths.ReadSource("Main/Adapters/ProvenderWorldAdapter.cs", stripComments: true);
        var buy = Regex.Match(src, @"public PurchaseResult Buy\(.*?\r?\n    \}", RegexOptions.Singleline).Value;

        Assert.AreEqual(1, Regex.Matches(src, @"SellItemsAction\.Apply\(").Count);
        StringAssert.Contains(buy, "SellItemsAction.Apply(");
        Assert.IsFalse(Regex.IsMatch(buy, @"\b(for|while|foreach)\s*\("), "no loop around the sell call");
    }

    [TestMethod]
    public void EveryPlayerFacingKey_IsAProvenderKey()
    {
        foreach (var file in new[] { Root + "Hooks/ProvenderDialogLines.cs", Root + "Hooks/ProvenderBehavior.cs", Root + "ProvenderService.cs",
                     Root + "ProvenderConversationFlow.cs", "Main/Adapters/ProvenderWorldAdapter.cs" })
        {
            var src = RepoPaths.ReadSource(file, stripComments: true);
            foreach (Match key in Regex.Matches(src, @"\{=([A-Za-z0-9_!]+)\}"))
                Assert.IsTrue(key.Groups[1].Value.StartsWith("taom_provender_"), file + ": " + key.Value);
        }
    }

    [TestMethod]
    public void TheAdapter_ReadsTheOfficerFromTheRole_NeverTheEffectiveQuartermaster()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/ProvenderWorldAdapter.cs", stripComments: true);

        Assert.AreEqual(2, System.Text.RegularExpressions.Regex.Matches(src, @"GetRoleHolder\(PartyRole\.Quartermaster\)").Count,
            "both role reads (the party snapshot and the talk check's QuartermasterId) use the Quartermaster role");
        Assert.IsFalse(src.Contains("EffectiveQuartermaster"), "it falls back to the leader, who is the player");
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_RegistersTheServiceGraph_AndTheBehaviorDeclResolves()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IRaceManager>());
        container.RegisterInstance(Substitute.For<ICampService>());
        container.RegisterInstance(Substitute.For<IHeroSkillXpAdapter>());
        container.RegisterInstance(Substitute.For<IPartyItemRosterAdapter>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IEnlistmentStateQuery>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        var module = new ProvenderModule();

        module.RegisterServices(container);

        Assert.IsNotNull(container.Resolve<ProvenderService>());
        var first = module.CampaignBehaviors[0].Create(container);
        var second = module.CampaignBehaviors[0].Create(container);
        Assert.AreNotSame(first, second, "a behavior per campaign");
        Assert.AreNotSame(((ProvenderBehavior)first).Orders, ((ProvenderBehavior)second).Orders);
    }
}
