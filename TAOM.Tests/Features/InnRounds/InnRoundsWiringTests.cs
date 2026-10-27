using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.InnRounds;
using TAOM.Features.InnRounds.Hooks;
using TAOM.Features.Messengers;
using TAOM.Features.NotableServices;
using TAOM.Features.Relations;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.InnRounds;

/// <summary>
/// Wiring guards. The feature owns save data (the day and count of the rounds stood) and no patch and no model: one dialog
/// behavior built with <c>new</c> for every campaign, registered through its module and nowhere else. The engine calls the
/// behavior's members, so the shape rules are pinned on the comment-stripped sources.
/// </summary>
[TestClass]
public class InnRoundsWiringTests
{
    private const string Folder = "Main/Features/InnRounds";

    private static string Source(string relative) => RepoPaths.ReadSource(relative, stripComments: true);

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<InnRoundsModule>().Count());
        Assert.AreEqual(1, Regex.Matches(Source("Main/Composition/FeatureModules.cs"), Regex.Escape("new Features.InnRounds.InnRoundsModule()")).Count);
    }

    [TestMethod]
    public void TheSingleOwnerFiles_NameNothingOfTheFeature()
    {
        foreach (var file in new[] { "Main/IoC.cs", "Main/SubModule.cs" })
            Assert.IsFalse(Source(file).Contains("InnRounds"), file + " wires the feature by hand AND through its module");
    }

    [TestMethod]
    public void TheModule_OwnsSaveData_AndAddsOneBehavior_AndNothingElse()
    {
        var module = new InnRoundsModule();

        Assert.AreEqual("InnRounds", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsTrue(module.OwnsSaveData);
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
    }

    [TestMethod]
    public void Module_BuildsTheBehaviorWithNew_NeverFromTheContainer()
    {
        var src = Source(Folder + "/InnRoundsModule.cs");

        Assert.IsTrue(src.Contains("new InnRoundsDialogBehavior("));
        Assert.IsFalse(src.Contains("Resolve<InnRoundsDialogBehavior>"));
        Assert.IsFalse(Source(Folder + "/InnRoundsIoC.cs").Contains("InnRoundsDialogBehavior"));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheBehavior_IsANewInstanceForEachCampaign()
    {
        var module = new InnRoundsModule();
        using var container = Container();
        module.RegisterServices(container);

        var first = module.CampaignBehaviors[0].Create(container);
        var second = module.CampaignBehaviors[0].Create(container);

        Assert.IsInstanceOfType(first, typeof(InnRoundsDialogBehavior));
        Assert.AreNotSame(first, second);
    }

    private static Container Container()
    {
        var container = new Container();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(Path.Combine(Path.GetTempPath(), "taom-ir-wiring-" + Guid.NewGuid().ToString("N")));
        container.RegisterInstance(paths);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<IMessengerConversationState>());
        container.RegisterInstance(Substitute.For<IRandomSource>());
        container.RegisterInstance(Substitute.For<IHeroSkillXpAdapter>());
        container.RegisterInstance(Substitute.For<IHeroRenownAdapter>());
        container.RegisterInstance(Substitute.For<IMapRevealAdapter>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<IRelationGrantService>());
        new NotableServicesModule().RegisterServices(container);
        return container;
    }

    [TestMethod]
    public void TheServiceGraph_ResolvesAgainstTheSharedRegistrations()
    {
        using var container = Container();
        new InnRoundsModule().RegisterServices(container);

        Assert.AreSame(container.Resolve<InnRoundsService>(), container.Resolve<InnRoundsService>());
        Assert.IsInstanceOfType(container.Resolve<IInnRoundsAdapter>(), typeof(InnRoundsAdapter));
        Assert.IsInstanceOfType(container.Resolve<IInnRoundsConfigProvider>(), typeof(InnRoundsConfigProvider));
        Assert.IsInstanceOfType(container.Resolve<IInnRoundsSettingsProvider>(), typeof(InnRoundsSettingsProvider));
        Assert.AreEqual(1, container.GetServiceRegistrations().Count(r => r.ServiceType == typeof(HoldsRevealService)), "the shared reveal is NotableServices' registration");
    }

    [TestMethod]
    public void TheIoC_RegistersNothingTheOtherFeaturesOwn()
    {
        var src = Source(Folder + "/InnRoundsIoC.cs");

        foreach (var shared in new[] { "HoldsRevealService", "IMapRevealAdapter", "IHeroSkillXpAdapter", "IHeroRenownAdapter", "IRandomSource",
                     "IMessengerConversationState", "ICoopSessionProvider", "IDedicatedServerProvider" })
            Assert.IsFalse(src.Contains("Register<" + shared) || src.Contains("<" + shared + ","), shared + " is registered by another feature");
    }

    [TestMethod]
    public void TheService_HasNoInterface_ItHasOneImplementationAndNoFake()
    {
        Assert.IsNull(typeof(InnRoundsService).Assembly.GetType("TAOM.Features.InnRounds.IInnRoundsService"));
        Assert.IsFalse(File.Exists(RepoPaths.RepoPath("Main", "Features", "InnRounds", "IInnRoundsService.cs")));
    }

    [TestMethod]
    public void TheSettingsProvider_HasOnePublicParameterlessConstructor_AndOneInternalTestOne()
    {
        var all = typeof(InnRoundsSettingsProvider).GetConstructors(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        Assert.AreEqual(2, all.Length);
        Assert.AreEqual(1, typeof(InnRoundsSettingsProvider).GetConstructors().Length);
        Assert.AreEqual(0, typeof(InnRoundsSettingsProvider).GetConstructors()[0].GetParameters().Length);
    }

    [TestMethod]
    public void TheFeature_NeverTouchesHarmony_AndSavesOnlyInTheBehavior()
    {
        foreach (var file in Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "InnRounds"), "*.cs", SearchOption.AllDirectories))
        {
            var src = RepoPaths.StripComments(File.ReadAllText(file));
            Assert.IsFalse(src.Contains("HarmonyLib"), file);
            Assert.IsFalse(src.Contains("[HarmonyPatch"), file);
            if (!file.EndsWith("InnRoundsDialogBehavior.cs", StringComparison.Ordinal))
                Assert.IsFalse(src.Contains("dataStore.SyncData"), file);
        }
    }

    [TestMethod]
    public void EveryEntryPointFile_StaysUnder150Lines()
    {
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "InnRounds", "Hooks"), "*.cs");

        Assert.AreEqual(3, files.Length, "the texts, the lines and the behavior");
        foreach (var file in files)
            Assert.IsTrue(File.ReadAllLines(file).Length < 150, "ADR-002: " + Path.GetFileName(file));
    }

    [TestMethod]
    public void TheBehavior_SubscribesTheSessionLaunchAndTheConversationEnd_AndRunsEveryMemberBehindTheFaultLog()
    {
        var src = Source(Folder + "/Hooks/InnRoundsDialogBehavior.cs");

        StringAssert.Contains(src, "CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched)");
        StringAssert.Contains(src, "CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded)");
        StringAssert.Contains(src, "new ScreenFaultLog(logger, \"InnRounds\")");
        foreach (var site in new[] { "\"session launched\"", "\"party round condition\"", "\"house round condition\"",
                     "\"party round clickable condition\"", "\"house round clickable condition\"", "\"party round consequence\"",
                     "\"house round consequence\"", "\"reveal condition\"", "\"reveal clickable condition\"", "\"reveal consequence\"" })
            StringAssert.Contains(src, site);
        Assert.IsTrue(Regex.IsMatch(src, @"void OnConversationEnded\([^)]*\)\s*=>\s*_outcome = RevealOutcome\.Unavailable;"));
    }

    [TestMethod]
    public void TheBehavior_SavesTheDayAndTheCountUnderTwoKeysOfItsOwn()
    {
        var src = Source(Folder + "/Hooks/InnRoundsDialogBehavior.cs");

        StringAssert.Contains(src, "DayKey = \"_taom_innrounds_day\"");
        StringAssert.Contains(src, "CountKey = \"_taom_innrounds_count\"");
        StringAssert.Contains(src, "dataStore.SyncData(DayKey, ref day)");
        StringAssert.Contains(src, "dataStore.SyncData(CountKey, ref count)");
        Assert.AreEqual(2, Regex.Matches(src, @"dataStore\.SyncData\(").Count);
    }

    [TestMethod]
    public void DialogLines_SitOnTheTavernKeeperTalk_AtPriority111_WithLocalizedText_AndClickableConditions()
    {
        var src = Source(Folder + "/Hooks/InnRoundsDialogLines.cs");

        Assert.AreEqual(3, Regex.Matches(src, "AddPlayerLine\\(\"taom_round_\\w+\", Talk,").Count);
        StringAssert.Contains(src, "Talk = \"tavernkeeper_talk\"");
        StringAssert.Contains(src, "Pretalk = \"tavernkeeper_pretalk\"");
        StringAssert.Contains(src, "Priority = 111");
        StringAssert.Contains(src, "{=taom_round_");
        StringAssert.Contains(src, "b.PartyClickable(out why)");
        StringAssert.Contains(src, "b.HouseClickable(out why)");
        StringAssert.Contains(src, "b.RevealClickable(out why)");
        Assert.IsFalse(src.Contains("\"start\""), "no line rewrites a vanilla state");
    }

    [TestMethod]
    public void TheFailedRevealReply_GoesBackToTheTalkState_AsVanillasTurndownDoes()
    {
        var src = Source(Folder + "/Hooks/InnRoundsDialogLines.cs");

        Assert.IsTrue(Regex.IsMatch(src, "AddDialogLine\\(\"taom_round_reveal_failed\", \"taom_round_reveal_reply\", Talk,"),
            "the failed reveal says 'Anything else?' and must not feed the pretalk state, whose own line says it again");
        StringAssert.Contains(src, "VanillaAnythingElse = \"{=ds294zxi}Anything else?\"");
    }

    [TestMethod]
    public void NoTwoLines_ShareAVariable_AndNoLineReadsVanillasGlobalPrice()
    {
        var lines = Source(Folder + "/Hooks/InnRoundsDialogLines.cs");
        var behavior = Source(Folder + "/Hooks/InnRoundsDialogBehavior.cs");

        var variables = Regex.Matches(lines, "(?:Party|House|Reveal)LineVariable = \"([A-Z_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        CollectionAssert.AreEquivalent(new[] { "TAOM_ROUND_PARTY_LINE", "TAOM_ROUND_HOUSE_LINE", "TAOM_ROUND_REVEAL_LINE" }, variables);
        Assert.AreEqual(variables.Count, variables.Distinct().Count());
        foreach (var other in new[] { "TAOM_NS_REVEAL_LINE", "TAOM_NS_INTRODUCE_LINE" })
            Assert.IsFalse(variables.Contains(other), "Notable Services' variable");
        foreach (var src in new[] { lines, behavior, Source(Folder + "/Hooks/InnRoundsTexts.cs") })
            Assert.IsFalse(Regex.IsMatch(src, "MBTextManager\\.SetTextVariable\\(\"PRICE\""), "vanilla's global PRICE is never read or written");
        StringAssert.Contains(lines, "\"{=!}{\" + PartyLineVariable + \"}\"");
        StringAssert.Contains(lines, "\"{=!}{\" + HouseLineVariable + \"}\"");
        StringAssert.Contains(lines, "\"{=!}{\" + RevealLineVariable + \"}\"");
    }

    [TestMethod]
    public void TheNotEnoughGoldRow_IsVanillasByKey_AndNoRowIsRegisteredForIt()
    {
        StringAssert.Contains(Source("Main/Features/NotableServices/Hooks/NotableServicesTexts.cs"), "{=d0kbtGYn}You don't have enough gold.");
        Assert.IsFalse(File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml")).Contains("d0kbtGYn"));
        Assert.IsFalse(Source(Folder + "/Hooks/InnRoundsTexts.cs").Contains("d0kbtGYn"), "reuse NotableServicesTexts.NotEnoughGold");
    }

    [TestMethod]
    public void TheKeepersFailedReply_IsVanillasAnythingElseByKey_NeverARegisteredRow()
    {
        StringAssert.Contains(Source(Folder + "/Hooks/InnRoundsDialogLines.cs"), "{=ds294zxi}Anything else?");
        Assert.IsFalse(File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml")).Contains("ds294zxi"));
    }

    [TestMethod]
    public void TheConditions_MakeNoWrite_InTheServiceOrTheBehavior()
    {
        var service = Source(Folder + "/InnRoundsService.cs");
        var behavior = Source(Folder + "/Hooks/InnRoundsDialogBehavior.cs");
        var writes = new[] { "PayTown(", "AddMorale(", "AddSkillXp(", "AddSkillXpLanded(", "AddClanRenown(", "Track(", "ledger.Record(", "Ledger.Record(" };

        foreach (var name in new[] { "public RoundQuote? QuoteParty(", "public RoundQuote? QuoteHouse(", "public bool OffersReveal()",
                     "public bool CanAffordReveal()", "private RoundQuote? Quote(", "private int PricePerHead(", "private TavernSnapshot? Keeper()" })
            foreach (var write in writes)
                Assert.IsFalse(Member(service, name).Contains(write), name + " calls " + write);

        foreach (var name in new[] { "internal bool CanParty()", "internal bool CanHouse()", "internal bool PartyClickable(", "internal bool HouseClickable(",
                     "private bool Clickable(", "internal bool CanReveal()", "internal bool RevealClickable(", "internal bool Answered(" })
            Assert.IsFalse(Regex.IsMatch(Member(behavior, name), @"_service\.(StandParty|StandHouse|Reveal)\("), name + " writes");
    }

    // One member's text: from its name to the blank line before the next 4-space-indented member.
    private static string Member(string src, string name)
    {
        var start = src.IndexOf(name, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, name + " is missing");
        var rest = src.Substring(start);
        var end = Regex.Match(rest, @"\n\s*\n    (?=\S)");
        return end.Success ? rest.Substring(0, end.Index) : rest;
    }

    [TestMethod]
    public void EveryGate_IsReadByTheKeeperFilter_AndEachConsequenceReChecksItThroughTheService()
    {
        var service = Source(Folder + "/InnRoundsService.cs");

        StringAssert.Contains(service, "CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _server)");
        Assert.IsFalse(service.Contains("IsSessionActive"), "the four-term form lives in CoopSessionPolicy only");
        StringAssert.Contains(service, "!_settings.Enabled || !MayWriteWorld || _messenger.IsOpen");
        StringAssert.Contains(service, "IsTavernkeeper: true, InTown: true");
        foreach (var write in new[] { "private bool Stand(", "public RevealOutcome Reveal()" })
            StringAssert.Contains(Member(service, write), "Keeper()", write + " must re-check every gate");
        StringAssert.Contains(Member(service, "private RoundQuote? Quote("), "keeper == null");
    }

    [TestMethod]
    public void TheSettings_AreListedAsPlayerLocal_InTheCoopRelevanceTable()
    {
        var src = Source("Main/Features/CoopInterop/CoopSettingsRelevance.cs");
        var start = src.IndexOf("PlayerLocal =", StringComparison.Ordinal);
        var block = src.Substring(start, src.IndexOf("};", start, StringComparison.Ordinal) - start);

        foreach (var name in new[] { "EnableInnRounds", "InnRoundsMoraleScale", "InnRoundsRenownScale" })
        {
            StringAssert.Contains(block, "\"" + name + "\"");
            Assert.IsFalse(CoopSettingsRelevance.IsSimulationRelevant(typeof(TaomSettings).GetProperty(name)!), name);
        }
    }

    // ---- the adapter's own calls, pinned on its source ----

    private static string Adapter() => RepoPaths.ReadSource("Main/Adapters/InnRoundsAdapter.cs", stripComments: true);

    [TestMethod]
    public void TheAdapter_PaysTheTown_ByTheSettlementAction_AndGivesBackBySettlementToCharacter()
    {
        var pay = Member(Adapter(), "public bool PayTown(");

        StringAssert.Contains(pay, "GiveGoldAction.ApplyForCharacterToSettlement(player, settlement, price)");
        StringAssert.Contains(pay, "before - player.Gold");
        StringAssert.Contains(pay, "GiveBack(settlement, player, taken)");
        StringAssert.Contains(Adapter(), "GiveGoldAction.ApplyForSettlementToCharacter(from, to, amount, disableNotification: true)");
    }

    [TestMethod]
    public void TheAdapter_PricesTheDrinkAsABuyer_FromTheTownsMarket()
    {
        var price = Member(Adapter(), "public int? DrinkPrice(");

        StringAssert.Contains(price, "?.Town?.MarketData");
        StringAssert.Contains(price, "market.GetPrice(item, Campaign.Current?.MainParty, isSelling: false)");
    }

    [TestMethod]
    public void TheAdapter_CountsTheTavern_OnlyWhenTheConversationRunsInIt()
    {
        var count = Member(Adapter(), "public int TavernHeadcount(");

        StringAssert.Contains(count, "?.LocationComplex?.GetLocationWithId(TavernLocationId)");
        StringAssert.Contains(count, "tavern != null && CampaignMission.Current?.Location == tavern ? tavern.CharacterCount : 0");
        Assert.IsFalse(count.Contains("GetListOfCharactersInLocation"), "the room is counted only from the tavern the player stands in");
        StringAssert.Contains(Adapter(), "TavernLocationId = \"tavern\"");
    }

    [TestMethod]
    public void TheAdapter_ReadsTheKeeperFromTheConversationCharacter_AndTheOwnersLeaderRelation()
    {
        var keeper = Member(Adapter(), "public TavernSnapshot? Keeper()");

        StringAssert.Contains(keeper, "OneToOneConversationCharacter");
        StringAssert.Contains(keeper, "Occupation.Tavernkeeper");
        StringAssert.Contains(keeper, "settlement.IsTown");
        StringAssert.Contains(keeper, "OwnerClan?.Leader?.GetRelationWithPlayer()");
        StringAssert.Contains(keeper, "faction.IsAtWarWith(playerFaction)");
    }

    [TestMethod]
    public void TheAdapter_ReadsTheMoraleBack_AndTheDay_AndTheHeadcount()
    {
        var src = Adapter();

        StringAssert.Contains(Member(src, "public float AddMorale("), "party.RecentEventsMorale - before");
        StringAssert.Contains(src, "(int)CampaignTime.Now.ToDays");
        StringAssert.Contains(src, "Campaign.Current?.MainParty?.MemberRoster?.TotalManCount");
        StringAssert.Contains(Member(src, "public float AddMorale("), "Campaign.Current?.MainParty");
        Assert.IsFalse(src.Contains("MobileParty.MainParty"), "the static getter dereferences Campaign.Current without a null check");
    }

    [TestMethod]
    public void TheSkillAdapter_ReturnsTheDifferenceOfTheDevelopersXp_NotTheAsk()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/HeroSkillXpAdapter.cs", stripComments: true);
        var landed = Member(src, "private bool Add(");

        StringAssert.Contains(landed, "GetSkillXp(skill)");
        StringAssert.Contains(landed, "AddSkillXp(skill, xp)");
        StringAssert.Contains(landed, "after - before");
        Assert.AreEqual(2, Regex.Matches(landed, "GetSkillXp\\(skill\\)").Count, "read before and after");
        StringAssert.Contains(RepoPaths.ReadSource("Main/Adapters/IHeroSkillXpAdapter.cs", stripComments: true), "bool AddSkillXp(string heroId, string skillId, float xp);");
    }

    [TestMethod]
    public void TheAdapter_NamesTheDrinkByTheCultureVariation_AndFallsBackToAle()
    {
        var src = Adapter();

        StringAssert.Contains(src, "DrinkTextId = \"str_taom_round_drink\"");
        StringAssert.Contains(src, "GameTexts.TryGetText(DrinkTextId, out var named, cultureId)");
        StringAssert.Contains(src, "{=taom_round_drink_default}ale");
    }
}
