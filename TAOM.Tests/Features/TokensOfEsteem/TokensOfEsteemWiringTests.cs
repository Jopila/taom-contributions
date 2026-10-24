using System;
using System.Collections.Generic;
using System.IO;
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
using TAOM.Features.Execution;
using TAOM.Features.TokensOfEsteem;
using TAOM.Features.TokensOfEsteem.Hooks;
using TAOM.Features.Relations;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.TokensOfEsteem;

/// <summary>
/// Wiring guards for Tokens of Esteem. The module owns save data (the cooldown ledger), so a dependency its behavior cannot
/// resolve throws out of OnGameStart and no campaign starts. The behavior is built with <c>new</c> for every campaign, so each
/// campaign has its own ledger: a container singleton is constructed once per process.
/// </summary>
[TestClass]
public class TokensOfEsteemWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheTokensOfEsteemModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<TokensOfEsteemModule>().Count());
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.TokensOfEsteem.TokensOfEsteemModule()"));
    }

    [TestMethod]
    public void TheSingleOwnerFiles_AreNotEditedForThisFeature()
    {
        Assert.IsFalse(RepoPaths.ReadSource("Main/IoC.cs", stripComments: true).Contains("TokensOfEsteem"));
        Assert.IsFalse(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true).Contains("TokensOfEsteem"));
    }

    [TestMethod]
    public void Module_OwnsSaveData_IsNotParked_AndAddsNoPatchOrMissionBehavior()
    {
        var module = new TokensOfEsteemModule();

        Assert.AreEqual("TokensOfEsteem", module.Id);
        Assert.IsTrue(module.OwnsSaveData, "the cooldown ledger is saved in SyncData");
        Assert.IsNull(module.ParkedReason);
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void Module_DeclaresOneCampaignBehavior_BuiltWithNew_NeverFromTheContainer()
    {
        var module = new TokensOfEsteemModule();
        var src = RepoPaths.ReadSource("Main/Features/TokensOfEsteem/TokensOfEsteemModule.cs", stripComments: true);

        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(GiftBehavior), module.CampaignBehaviors[0].BehaviorType);
        Assert.IsTrue(src.Contains("new GiftBehavior("), "the behavior must be built per campaign");
        Assert.IsFalse(src.Contains("Resolve<GiftBehavior>"));
    }

    [TestMethod]
    public void Behavior_OwnsAFreshLedgerPerInstance_AndNeverResetsSavedStateFromSessionLaunched()
    {
        var src = RepoPaths.ReadSource("Main/Features/TokensOfEsteem/Hooks/GiftBehavior.cs", stripComments: true);

        var flow = RepoPaths.ReadSource("Main/Features/TokensOfEsteem/GiftConversationFlow.cs", stripComments: true);
        Assert.IsTrue(src.Contains("_flow = new GiftConversationFlow("), "the behavior builds its flow, so each campaign has its own");
        Assert.IsTrue(Regex.IsMatch(flow, @"GiftCooldownLedger\s+Ledger\s*\{\s*get;\s*\}\s*=\s*new\s*(GiftCooldownLedger)?\s*\("),
            "the ledger is a field of the flow, so it is new with every campaign's behavior");
        var launched = Regex.Match(src, @"void OnSessionLaunched\([^)]*\)\s*\{(.*?)\r?\n    \}", RegexOptions.Singleline);
        Assert.IsTrue(launched.Success, "OnSessionLaunched not found");
        Assert.IsFalse(launched.Groups[1].Value.Contains("Ledger") || launched.Groups[1].Value.Contains("Decode"),
            "persisted state must not be reset from OnSessionLaunched");
        Assert.IsTrue(src.Contains("CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched)"));
    }

    [TestMethod]
    public void Behavior_BindsItsLinesToTheFlow_WithNoPerStarterGuardAndNoForwarders()
    {
        var src = RepoPaths.ReadSource("Main/Features/TokensOfEsteem/Hooks/GiftBehavior.cs", stripComments: true);

        StringAssert.Contains(src, "GiftDialogLines.Register(starter, _flow);");
        Assert.IsFalse(src.Contains("_lastSessionStarter"), "the behavior is built per campaign: no guard");
        Assert.IsFalse(src.Contains("_conversation"), "the behavior holds no conversation adapter of its own");
        Assert.IsFalse(Regex.IsMatch(src, @"internal\s+(bool|void)\s+\w+\("), "the rules are called on the flow, not forwarded by the behavior");
    }

    [TestMethod]
    public void EntryPoints_StayUnder150Lines()
    {
        foreach (var file in new[] { "GiftBehavior", "GiftDialogLines" })
        {
            var lines = RepoPaths.ReadSource($"Main/Features/TokensOfEsteem/Hooks/{file}.cs").Split('\n').Length;
            Assert.IsTrue(lines < 150, $"{file} is {lines} lines (ADR-002)");
        }
    }

    [TestMethod]
    public void DialogLines_UseTheGiftIds_OnBothBranches_WithTheFirstRepeatableLineAndABackLineAtPriorityOne()
    {
        var src = RepoPaths.ReadSource("Main/Features/TokensOfEsteem/Hooks/GiftDialogLines.cs", stripComments: true);

        var ids = Regex.Matches(src, @"Add(?:Player|Dialog|RepeatablePlayer)Line\(\s*""([^""]+)""").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        Assert.IsTrue(ids.Count >= 10, "expected the whole tree, found " + ids.Count);
        Assert.IsTrue(ids.All(id => id.StartsWith("taom_gift_")), "every line id is a taom_gift_ id: " + string.Join(", ", ids.Where(i => !i.StartsWith("taom_gift_"))));
        Assert.AreEqual(ids.Count, ids.Distinct().Count(), "a duplicate line id");
        Assert.IsFalse(src.Contains("taom_oath"), "the oath line belongs to another feature");
        StringAssert.Contains(src, "\"hero_main_options\"");
        StringAssert.Contains(src, "\"lord_talk_speak_diplomacy_2\"");
        Assert.AreEqual(1, Regex.Matches(src, "AddRepeatablePlayerLine").Count);
        Assert.IsTrue(Regex.IsMatch(src, @"""taom_gift_back"".*?,\s*1\s*\)", RegexOptions.Singleline),
            "the back line sits at priority 1, last in the list");
    }

    [TestMethod]
    public void EveryPlayerFacingKey_IsATaomGiftKey()
    {
        foreach (var file in new[] { "Hooks/GiftDialogLines.cs", "Hooks/GiftBehavior.cs", "GiftService.cs", "GiftConversationFlow.cs" })
        {
            var src = RepoPaths.ReadSource("Main/Features/TokensOfEsteem/" + file, stripComments: true);
            foreach (Match key in Regex.Matches(src, @"\{=([A-Za-z0-9_!]+)\}"))
                Assert.IsTrue(key.Groups[1].Value == "!" || key.Groups[1].Value.StartsWith("taom_gift_"), file + ": " + key.Value);
        }
        foreach (var file in new[] { "IGiftConversationAdapter.cs", "GiftConversationAdapter.cs", "GiftHeroAdapter.cs" })
        {
            var src = RepoPaths.ReadSource("Main/Adapters/" + file, stripComments: true);
            foreach (Match key in Regex.Matches(src, @"\{=([A-Za-z0-9_!]+)\}"))
                Assert.IsTrue(key.Groups[1].Value == "!" || key.Groups[1].Value.StartsWith("taom_gift_"), file + ": " + key.Value);
        }
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_RegistersTheServiceGraph_AndTheBehaviorDeclResolves()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IRaceManager>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<IRelationGrantService>());
        container.RegisterInstance(Substitute.For<IPartyItemRosterAdapter>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<TAOM.Features.Messengers.IMessengerConversationState>());
        var module = new TokensOfEsteemModule();

        module.RegisterServices(container);

        Assert.IsNotNull(container.Resolve<GiftService>());
        Assert.IsNotNull(container.Resolve<IGiftCatalogProvider>());
        Assert.IsNotNull(container.Resolve<IGiftSettingsProvider>());
        Assert.IsNotNull(container.Resolve<IGiftHeroAdapter>());
        Assert.IsNotNull(container.Resolve<IGiftItemAdapter>());
        Assert.IsNotNull(container.Resolve<IGiftConversationAdapter>());
        var first = module.CampaignBehaviors[0].Create(container);
        var second = module.CampaignBehaviors[0].Create(container);
        Assert.IsNotNull(first);
        Assert.AreNotSame(first, second, "a behavior per campaign");
        Assert.AreNotSame(((GiftBehavior)first).Ledger, ((GiftBehavior)second).Ledger);
    }

    // ---- the dialog tree, pinned from the source (a CampaignGameStarter cannot be built outside the game)

    private static Dictionary<string, string> DialogStatements()
    {
        var src = RepoPaths.ReadSource("Main/Features/TokensOfEsteem/Hooks/GiftDialogLines.cs", stripComments: true);
        var map = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(src, @"s\.Add(?:Player|Dialog|RepeatablePlayer)Line\(\s*""([^""]+)""(.*?);\s*\r?\n", RegexOptions.Singleline))
            map[m.Groups[1].Value] = Regex.Replace(m.Groups[2].Value, @"\s+", " ");
        return map;
    }

    private static int PriorityOf(string statement)
    {
        var m = Regex.Match(statement, @",\s*Priority(?:\s*\+\s*(\d+))?\s*\)\s*$");
        Assert.IsTrue(m.Success, "no Priority at the end of: " + statement);
        return 100 + (m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0);
    }

    [TestMethod]
    public void DialogLines_TheHeroAnswersInOrder_RefusalsThenNothingThenThePrompt()
    {
        var lines = DialogStatements();

        Assert.IsTrue(PriorityOf(lines["taom_gift_refuse_hostile"]) > PriorityOf(lines["taom_gift_refuse_alignment"]));
        Assert.IsTrue(PriorityOf(lines["taom_gift_refuse_alignment"]) > PriorityOf(lines["taom_gift_empty"]));
        Assert.IsTrue(PriorityOf(lines["taom_gift_empty"]) > PriorityOf(lines["taom_gift_prompt"]), "empty must outrank prompt, or an empty bag is asked what it brought");
        Assert.IsTrue(PriorityOf(lines["taom_gift_prompt"]) > PriorityOf(lines["taom_gift_deferred"]));
    }

    [TestMethod]
    public void DialogLines_TheThanksAreInOrder_SuitedThenPlainThenFailed()
    {
        var lines = DialogStatements();

        Assert.IsTrue(PriorityOf(lines["taom_gift_thanks_suited"]) > PriorityOf(lines["taom_gift_thanks"]), "suited must outrank plain, or a prized gift gets the plain thanks");
        Assert.IsTrue(PriorityOf(lines["taom_gift_thanks"]) > PriorityOf(lines["taom_gift_failed"]));
    }

    [TestMethod]
    public void DialogLines_ConditionsConsequencesAndOutputTokens_AreWhereTheFlowExpectsThem()
    {
        var lines = DialogStatements();

        StringAssert.Contains(lines["taom_gift_refuse_hostile"], "\"taom_gift_asked\", \"lord_pretalk\"");
        StringAssert.Contains(lines["taom_gift_refuse_hostile"], "f.GateIs(GiftGateKind.RefusesHostile)");
        StringAssert.Contains(lines["taom_gift_refuse_alignment"], "f.GateIs(GiftGateKind.RefusesAlignment)");
        StringAssert.Contains(lines["taom_gift_empty"], "\"taom_gift_asked\", \"lord_pretalk\"");
        StringAssert.Contains(lines["taom_gift_empty"], "f.HasNoOptions, null");
        StringAssert.Contains(lines["taom_gift_prompt"], "\"taom_gift_asked\", \"taom_gift_list\"");
        StringAssert.Contains(lines["taom_gift_prompt"], "f.GateIs(GiftGateKind.Open)");
        StringAssert.Contains(lines["taom_gift_deferred"], "\"taom_gift_asked\", \"lord_pretalk\"");

        var pick = lines["taom_gift_pick"];
        StringAssert.Contains(pick, "\"taom_gift_list\", \"taom_gift_given\", \"{=!}{GIFT_ROW}\"");
        StringAssert.Contains(pick, "\"taom_gift_asked\"", "the continue-listing token leads back to the hero's prompt");
        StringAssert.Contains(pick, "f.ListRow, f.Pick, Priority");
        StringAssert.Contains(lines["taom_gift_back"], "\"taom_gift_list\", \"lord_pretalk\"");

        StringAssert.Contains(lines["taom_gift_thanks_suited"], "\"taom_gift_given\", \"lord_pretalk\"");
        StringAssert.Contains(lines["taom_gift_thanks_suited"], "f.GaveSuited, null");
        StringAssert.Contains(lines["taom_gift_thanks"], "f.Gave, null");
        StringAssert.Contains(lines["taom_gift_failed"], "\"taom_gift_given\", \"lord_pretalk\", ");
        foreach (var id in lines.Keys.Where(k => k.StartsWith("taom_gift_thanks") || k == "taom_gift_failed"))
            StringAssert.Contains(lines[id], "lord_pretalk", id);
    }

    [TestMethod]
    public void DialogLines_BothEntryLines_LeadToTheHeroAnswer_AndCallTheFlow()
    {
        var src = RepoPaths.ReadSource("Main/Features/TokensOfEsteem/Hooks/GiftDialogLines.cs", stripComments: true);

        StringAssert.Contains(src, "Entry(s, f, \"taom_gift_offer_companion\", \"hero_main_options\", GiftEntry.Companion);");
        StringAssert.Contains(src, "Entry(s, f, \"taom_gift_offer_lord\", \"lord_talk_speak_diplomacy_2\", GiftEntry.Lord);");
        var helper = src.Substring(src.IndexOf("private static void Entry(", StringComparison.Ordinal));
        StringAssert.Contains(helper, "\"taom_gift_asked\"");
        StringAssert.Contains(helper, "{=taom_gift_offer}I have a gift for you.");
        StringAssert.Contains(helper, "() => f.CanOffer(entry), () => f.Ask(entry), Priority");
        StringAssert.Contains(helper, "ChooseClickable(f, entry, out explanation)");
    }

    // ---- wording

    private static readonly Dictionary<string, string> GiftEnglish = new()
    {
        ["taom_gift_offer"] = "I have a gift for you.",
        ["taom_gift_refuse_hostile"] = "I will not take gifts from you.",
        ["taom_gift_refuse_alignment"] = "I take nothing from those who serve the Shadow.",
        ["taom_gift_empty"] = "You carry nothing I can accept.",
        ["taom_gift_prompt"] = "What do you have for me?",
        ["taom_gift_deferred"] = "Another time, perhaps.",
        ["taom_gift_more"] = "Let me look through my things again.",
        ["taom_gift_back"] = "Never mind.",
        ["taom_gift_thanks_suited"] = "You know what I like. Thank you.",
        ["taom_gift_thanks"] = "Thank you. That is kind of you.",
        ["taom_gift_failed"] = "Something is not right. Let us leave it.",
        ["taom_gift_cooldown"] = "It is too soon for another gift. Days left: {DAYS}.",
        ["taom_gift_cannot_afford"] = "You do not carry that much gold.",
        ["taom_gift_no_effect"] = "This would not improve your relation.",
        ["taom_gift_row_good"] = "Give {GOOD_NAME} (you carry {GOOD_COUNT}).",
        ["taom_gift_row_purse"] = "Give a purse of {PURSE_GOLD} gold.",
    };

    private static Dictionary<string, string> GiftRows(string relativePath)
    {
        var text = File.ReadAllText(RepoPaths.RepoPath(relativePath.Split('/')));
        var rows = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(text, @"<string\s+id=""(taom_gift_[a-z_]+)""\s+text=""([^""]*)"""))
        {
            Assert.IsFalse(rows.ContainsKey(m.Groups[1].Value), relativePath + ": " + m.Groups[1].Value + " twice");
            rows[m.Groups[1].Value] = System.Net.WebUtility.HtmlDecode(m.Groups[2].Value);
        }
        return rows;
    }

    [TestMethod]
    public void TheEnglishRows_AreTheFinalNeutralWording_AndThereIsNoRelationGainKey()
    {
        var rows = GiftRows("Main/_Module/ModuleData/taom_module_strings.xml");

        CollectionAssert.AreEquivalent(GiftEnglish.Keys.ToList(), rows.Keys.ToList(), "the registered taom_gift_ keys");
        foreach (var pair in GiftEnglish)
            Assert.AreEqual("{=" + pair.Key + "}" + pair.Value, rows[pair.Key], pair.Key);
    }

    [TestMethod]
    public void EveryLanguageFile_HoldsExactlyTheGiftKeys_NoneStale()
    {
        var oldEnglish = new[]
        {
            "This is just what I would have chosen. Thank you.", "You carry nothing I could accept.", "What have you brought?",
            "You gave a gift too recently. Days left: {DAYS}.", "Something is not as you said. Let us leave it.",
            "This would not change how they think of you.", "Your relation with {HERO} changed by {GAIN}.",
        };
        var languages = Directory.GetDirectories(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"));
        Assert.AreEqual(12, languages.Length);
        foreach (var dir in languages)
        {
            var file = Directory.GetFiles(dir, "std_taom_module_strings_*.xml").Single();
            var rows = GiftRows("Main/_Module/ModuleData/Languages/" + Path.GetFileName(dir) + "/" + Path.GetFileName(file));
            CollectionAssert.AreEquivalent(GiftEnglish.Keys.ToList(), rows.Keys.ToList(), Path.GetFileName(file));
            foreach (var text in rows.Values)
                CollectionAssert.DoesNotContain(oldEnglish, text, Path.GetFileName(file) + ": a stale row");
        }
    }

    [TestMethod]
    public void TheEnglish_NamesNoGender()
    {
        foreach (var pair in GiftEnglish)
            Assert.IsFalse(Regex.IsMatch(pair.Value, @"\b(he|she|his|her|him|hers|himself|herself|lady|lord)\b", RegexOptions.IgnoreCase), pair.Key + ": " + pair.Value);
    }
}
