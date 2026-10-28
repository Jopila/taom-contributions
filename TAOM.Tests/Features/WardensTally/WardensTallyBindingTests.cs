using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TAOM.Features.WardensTally;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.WardensTally;

/// <summary>
/// Pins the vanilla seams Warden's Tally rides (#903), reading the installed engine with no campaign: the private init still
/// has its shape, both dungeon menus still use it, it still sets the title exactly when it sets the intro variable (the
/// listener's sign that vanilla's text is there to follow), the menu-opened event still fires after the init ran, the rebuilt
/// sentence is vanilla's own key and text, the count of "prisoners" is still the heroes of the settlement party and its
/// garrison parties, and a garrison's captures still go to its home settlement.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class WardensTallyBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static MethodInfo Target()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var method = AccessTools.Method(typeof(PlayerTownVisitCampaignBehavior), "town_keep_dungeon_on_init", new[] { typeof(MenuCallbackArgs) });
        Assert.IsNotNull(method, "PlayerTownVisitCampaignBehavior.town_keep_dungeon_on_init did not resolve.");
        return method!;
    }

    private static List<CodeInstruction> Il(MethodBase body) => PatchProcessor.GetOriginalInstructions(body).ToList();

    private static IEnumerable<string> Strings(MethodBase body) =>
        Il(body).Where(ci => ci.opcode == OpCodes.Ldstr).Select(ci => (string)ci.operand);

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void Target_IsThePrivateStaticVoidInitTakingMenuCallbackArgs_InInstalledEngine()
    {
        var target = Target();

        Assert.AreEqual(typeof(PlayerTownVisitCampaignBehavior), target.DeclaringType);
        Assert.AreEqual(typeof(void), target.ReturnType);
        Assert.IsTrue(target.IsStatic);
        Assert.IsTrue(target.IsPrivate);
        var parameters = target.GetParameters();
        Assert.AreEqual(1, parameters.Length);
        Assert.AreEqual(typeof(MenuCallbackArgs), parameters[0].ParameterType);
    }

    [DataTestMethod]
    [DataRow("town_keep_dungeon")]
    [DataRow("castle_dungeon")]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheDungeonMenu_IsStillRegisteredWithThatInit_AndTheIntroVariableAsItsText(string menuId)
    {
        var target = Target();

        var registered = typeof(PlayerTownVisitCampaignBehavior)
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(m => m.GetMethodBody() != null)
            .Select(m => Il(m))
            .Where(il => il.Any(ci => ci.opcode == OpCodes.Ldstr && (string)ci.operand == menuId))
            .ToList();

        Assert.IsTrue(registered.Count > 0, menuId + " is no longer registered by PlayerTownVisitCampaignBehavior.");
        Assert.IsTrue(
            registered.Any(il =>
            {
                var at = il.FindIndex(ci => ci.opcode == OpCodes.Ldstr && (string)ci.operand == menuId);
                var window = il.Skip(at).Take(6).ToList();
                return window.Any(ci => ci.opcode == OpCodes.Ldstr && (string)ci.operand == "{=!}{PRISONER_INTRODUCTION}")
                    && window.Any(ci => ci.opcode == OpCodes.Ldftn && ci.operand is MethodInfo mi && mi == target);
            }),
            menuId + " no longer opens with the PRISONER_INTRODUCTION text and town_keep_dungeon_on_init: the tally would miss it.");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheInit_SetsTheTitleOnlyBeforeItSetsTheIntroVariable_AfterCheckingTheNextLocation()
    {
        var il = Il(Target());

        var check = il.FindIndex(ci => ci.operand is MethodInfo mi && mi.Name == "CheckAndOpenNextLocation");
        var title = il.FindIndex(ci => ci.opcode == OpCodes.Stfld && ci.operand is FieldInfo f && f.Name == nameof(MenuCallbackArgs.MenuTitle));
        var intro = il.FindIndex(ci => ci.opcode == OpCodes.Ldstr && (string)ci.operand == "PRISONER_INTRODUCTION");

        Assert.IsTrue(check >= 0, "the init no longer asks MenuHelper.CheckAndOpenNextLocation");
        Assert.IsTrue(title > check, "the title is no longer set after the location check: a set title would not mean vanilla ran its branch");
        Assert.IsTrue(intro > title, "the intro variable is no longer set after the title");
        Assert.AreEqual(1, il.Count(ci => ci.opcode == OpCodes.Stfld && ci.operand is FieldInfo f && f.Name == nameof(MenuCallbackArgs.MenuTitle)),
            "the title is set in more than one place");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheRebuiltSentences_AreVanillasOwnKeysAndTexts_AsTheInitBuildsThem()
    {
        var strings = Strings(Target()).ToList();

        CollectionAssert.Contains(strings, "{=" + WardensTallyTexts.VanillaPrisonersKey + "}" + WardensTallyTexts.VanillaPrisoners);
        CollectionAssert.Contains(strings, "PRISONER_COUNT");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheInitsCount_IsTheNumberOfPrisonerHeroes_OfTheCurrentSettlement()
    {
        var calls = Il(Target()).Where(ci => ci.operand is MethodInfo).Select(ci => ((MethodInfo)ci.operand).Name).ToList();

        Assert.AreEqual(2, calls.Count(n => n == "GetPrisonerHeroes"), "the init counts the prisoner heroes once to pick the sentence and once to number it");
        CollectionAssert.Contains(calls, "get_CurrentSettlement");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void GetPrisonerHeroes_StillReadsTheSettlementPartyAndEachGarrisonPartysPrisonRoster()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var method = AccessTools.Method(typeof(SettlementComponent), nameof(SettlementComponent.GetPrisonerHeroes));
        Assert.IsNotNull(method);
        var calls = Il(method!).Where(ci => ci.operand is MethodInfo).Select(ci => ((MethodInfo)ci.operand).Name).ToList();

        // ReadDungeon enumerates the same parties: Settlement.Party plus every party in Settlement.Parties with IsGarrison.
        CollectionAssert.Contains(calls, "get_Parties");
        CollectionAssert.Contains(calls, "get_IsGarrison");
        CollectionAssert.Contains(calls, "get_PrisonRoster");
        CollectionAssert.Contains(calls, "get_IsHero");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void RunOnInit_InvokesTheInitBeforeItRaisesTheMenuOpenedEvent_OnTheSameArgs()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var run = AccessTools.Method(typeof(GameMenu), nameof(GameMenu.RunOnInit));
        Assert.IsNotNull(run, "GameMenu.RunOnInit is gone: the listener would never see the init's result.");
        var il = Il(run!);

        var init = il.FindIndex(ci => ci.operand is MethodInfo mi && mi.Name == "Invoke" && mi.DeclaringType!.Name == "OnInitDelegate");
        var title = il.FindIndex(ci => ci.opcode == OpCodes.Stfld && ci.operand is FieldInfo f && f.Name == nameof(GameMenu.MenuTitle) || ci.operand is MethodInfo set && set.Name == "set_MenuTitle");
        var opened = il.FindIndex(ci => ci.operand is MethodInfo mi && mi.Name == "OnGameMenuOpened");

        Assert.IsTrue(init >= 0, "the OnInit delegate is no longer invoked in RunOnInit");
        Assert.IsTrue(opened > init, "OnGameMenuOpened no longer comes after the init delegate: the listener would read an unset title");
        Assert.IsNotNull(typeof(CampaignEvents).GetProperty("GameMenuOpened", BindingFlags.Public | BindingFlags.Static), "CampaignEvents.GameMenuOpened is gone");
        Assert.IsTrue(title > init && title < opened, "the menu title is no longer copied between the init and the event");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void ALootedGarrisonsPrisoners_StillGoToItsHomeSettlementsParty()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var getter = AccessTools.PropertyGetter(typeof(MapEventParty), "RosterToReceiveLootPrisoners");
        Assert.IsNotNull(getter, "MapEventParty.RosterToReceiveLootPrisoners is gone");
        var calls = Il(getter!).Where(ci => ci.operand is MethodInfo).Select(ci => ((MethodInfo)ci.operand).Name).ToList();

        // One chance per dungeon: a garrison's captures are not kept in the garrison party, so the settlement is every lord's captor.
        CollectionAssert.Contains(calls, "get_IsGarrison");
        CollectionAssert.Contains(calls, "get_HomeSettlement");
    }
}
