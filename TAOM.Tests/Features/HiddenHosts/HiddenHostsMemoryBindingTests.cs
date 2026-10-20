using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// The engine facts the phase 3 memory rests on (v1.5.5 at the time of writing), one pin each. If an engine update moves
/// one, the memory's events, its fade or its XP grant would silently stop working, so each fails by name here instead.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HiddenHostsMemoryBindingTests
{
    private const BindingFlags AllMembers =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Type Named(string fullName)
    {
        var type = AccessTools.TypeByName(fullName);
        Assert.IsNotNull(type, fullName + " did not resolve.");
        return type;
    }

    private static List<CodeInstruction> Il(MethodBase method) => PatchProcessor.GetOriginalInstructions(method);

    private static List<string> Calls(MethodBase method) =>
        Il(method).Where(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Newobj) && ci.operand is MethodBase)
            .Select(ci => ((MethodBase)ci.operand).DeclaringType!.Name + "." + ((MethodBase)ci.operand).Name).ToList();

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MobilePartyIsVisibleSetter_RaisesThePartyVisibilityChangedEvent_OnlyOnAChange()
    {
        RequireGame();
        var party = Named("TaleWorlds.CampaignSystem.Party.MobileParty");
        var setter = party.GetProperty("IsVisible")!.SetMethod!;
        var il = Il(setter);

        var calls = Calls(setter);
        var first = calls.IndexOf("PartyBase.OnVisibilityChanged");
        Assert.IsTrue(first >= 0, "the setter calls PartyBase.OnVisibilityChanged: " + string.Join(", ", calls));
        Assert.IsTrue(il.Any(ci => ci.opcode == OpCodes.Beq || ci.opcode == OpCodes.Beq_S || ci.opcode == OpCodes.Bne_Un || ci.opcode == OpCodes.Bne_Un_S),
            "and it returns early when the value is unchanged, so an event means a change");

        var onChanged = Named("TaleWorlds.CampaignSystem.Party.PartyBase").GetMethod("OnVisibilityChanged", AllMembers)!;
        CollectionAssert.Contains(Calls(onChanged).Select(c => c.Substring(c.IndexOf('.') + 1)).ToList(), "OnPartyVisibilityChanged",
            "PartyBase.OnVisibilityChanged raises it through the campaign event dispatcher");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void CampaignEvents_ExposeTheFourEventsTheMemoryListensTo_WithTheArgumentsItsHandlersTake()
    {
        RequireGame();
        var events = Named("TaleWorlds.CampaignSystem.CampaignEvents");

        string Arguments(string name) =>
            string.Join(",", events.GetProperty(name, BindingFlags.Public | BindingFlags.Static)!.PropertyType.GetGenericArguments().Select(a => a.Name));
        var plain = events.GetProperty("HourlyTickEvent", BindingFlags.Public | BindingFlags.Static)!.PropertyType;

        Assert.AreEqual("PartyBase", Arguments("PartyVisibilityChangedEvent"));
        Assert.AreEqual("MobileParty,PartyBase", Arguments("MobilePartyDestroyed"));
        Assert.AreEqual("Single", Arguments("TickEvent"));
        Assert.AreEqual(0, plain.GetGenericArguments().Length, "the hourly tick carries no argument");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void DestroyPartyAction_RaisesMobilePartyDestroyedBeforeItRemovesTheParty()
    {
        RequireGame();
        var action = Named("TaleWorlds.CampaignSystem.Actions.DestroyPartyAction");
        var calls = Calls(action.GetMethod("ApplyInternal", AllMembers)!);

        int raised = calls.FindIndex(c => c.EndsWith(".OnMobilePartyDestroyed", StringComparison.Ordinal));
        int removed = calls.IndexOf("MobileParty.RemoveParty");

        Assert.IsTrue(raised >= 0, "ApplyInternal raises the event: " + string.Join(", ", calls));
        Assert.IsTrue(removed > raised, "and removes the party only afterwards");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void SettlementFind_IsADictionaryLookup_AndIsInspectedIsASettableProperty()
    {
        RequireGame();
        var settlement = Named("TaleWorlds.CampaignSystem.Settlements.Settlement");
        var find = settlement.GetMethod("Find", new[] { typeof(string) })!;

        var calls = Calls(find);

        CollectionAssert.Contains(calls, "MBObjectManager.GetObject");
        Assert.IsFalse(calls.Any(c => c.Contains("CampaignObjectManager")), "not the linear campaign-object walk a party lookup would be");
        var inspected = settlement.GetProperty("IsInspected")!;
        Assert.AreEqual(typeof(bool), inspected.PropertyType);
        Assert.IsNotNull(inspected.SetMethod, "the engine writes it each frame; TAOM only reads it");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void HeroAddSkillXp_TakesASkillAndRawXp_AndHandsThemToTheHeroDeveloper()
    {
        RequireGame();
        var hero = Named("TaleWorlds.CampaignSystem.Hero");
        var skill = Named("TaleWorlds.Core.SkillObject");
        var add = hero.GetMethod("AddSkillXp", new[] { skill, typeof(float) });

        Assert.IsNotNull(add, "Hero.AddSkillXp(SkillObject, float)");
        CollectionAssert.Contains(Calls(add!), "HeroDeveloper.AddSkillXp");
        var developer = Named("TaleWorlds.CampaignSystem.CharacterDevelopment.HeroDeveloper").GetMethod("AddSkillXp")!;
        var parameters = developer.GetParameters();
        CollectionAssert.AreEqual(new[] { "skill", "rawXp", "isAffectedByFocusFactor", "shouldNotify" }, parameters.Select(p => p.Name).ToArray());
        Assert.IsTrue(parameters[2].HasDefaultValue && (bool)parameters[2].DefaultValue! && parameters[3].HasDefaultValue,
            "focus and learning rate apply, as for vanilla's own Scouting XP, and level-ups notify");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void TheEngineHelpersTheMemoryCalls_KeepTheirShapes()
    {
        RequireGame();
        var ui = Named("TaleWorlds.CampaignSystem.ViewModelCollection.CampaignUIHelper");
        var hours = ui.GetMethod("GetHoursAndDaysTextFromHourValue", new[] { typeof(int) });
        var faction = Named("TaleWorlds.CampaignSystem.FactionManager").GetMethod("IsAtWarAgainstFaction");
        var party = Named("TaleWorlds.CampaignSystem.Party.MobileParty");

        Assert.IsNotNull(hours);
        Assert.AreEqual("TextObject", hours!.ReturnType.Name);
        Assert.IsTrue(hours.IsStatic);
        Assert.IsNotNull(faction);
        Assert.IsTrue(faction!.IsStatic && faction.GetParameters().Length == 2);
        Assert.AreEqual("Hero", party.GetProperty("EffectiveScout")!.PropertyType.Name);
        Assert.AreEqual("MobileParty", party.GetProperty("MainParty")!.PropertyType.Name);
    }

    [DataTestMethod]
    [DataRow("RefreshMobilePartyTooltip")]
    [DataRow("RefreshArmyTooltip")]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PartyAndArmyBuilders_PassTheTroopsGameTextAsTheTitleOfTheirTroopsSection(string builder)
    {
        RequireGame();
        var refresher = Named("TaleWorlds.CampaignSystem.ViewModelCollection.TooltipRefresherCollection");
        var il = Il(refresher.GetMethods(AllMembers).Single(m => m.Name == builder));

        int troops = il.FindIndex(ci => ci.opcode == OpCodes.Ldstr && (string)ci.operand == "str_map_tooltip_troops");
        int prisoners = il.FindIndex(ci => ci.opcode == OpCodes.Ldstr && (string)ci.operand == "str_map_tooltip_prisoners");
        Assert.IsTrue(troops >= 0 && prisoners > troops, builder + " loads the troops id, then the prisoners id, in that order");
        int call = il.FindIndex(troops, prisoners - troops, ci => ci.operand is MethodBase m && m.Name == "AddPartyTroopProperties");
        Assert.IsTrue(call > troops, builder + ": the troops text goes into the first AddPartyTroopProperties, before the prisoners");
    }

    // ---- Plain-sight stamps: the triggers (deep review H1)

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void CampaignTick_RaisesTheTickEvent_OnlyForAPositiveDt_SoAMenuThatStopsTimeNeverTicks()
    {
        RequireGame();
        var campaign = Named("TaleWorlds.CampaignSystem.Campaign");
        var il = Il(campaign.GetMethod("Tick", AllMembers)!);

        int raise = il.FindIndex(ci => ci.operand is MethodBase m && m.Name == "Tick" && m.DeclaringType!.Name.StartsWith("CampaignEvent", StringComparison.Ordinal));
        Assert.IsTrue(raise > 0, "Campaign.Tick raises the dispatcher's Tick");
        var before = il.Take(raise).ToList();
        int dt = before.FindIndex(ci => ci.opcode == OpCodes.Ldfld && ci.operand is FieldInfo f && f.Name == "_dt");
        Assert.IsTrue(dt >= 0, "the raise is guarded by _dt");
        Assert.IsTrue(before.Skip(dt).Take(4).Any(ci => ci.opcode == OpCodes.Ldc_R4 && Convert.ToSingle(ci.operand) == 0f),
            "against zero: no positive dt, no tick event (apart from the first two ticks of a campaign)");
        Assert.IsTrue(before.Skip(dt).Take(6).Any(ci => ci.opcode == OpCodes.Ble_Un || ci.opcode == OpCodes.Ble_Un_S || ci.opcode == OpCodes.Bgt
                                                         || ci.opcode == OpCodes.Bgt_S || ci.opcode == OpCodes.Bgt_Un_S || ci.opcode == OpCodes.Bgt_Un
                                                         || ci.opcode == OpCodes.Ble || ci.opcode == OpCodes.Ble_S),
            "with a branch on that comparison");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void GameMenuRunOnInit_RaisesGameMenuOpened_ForEveryMenu()
    {
        RequireGame();
        var menu = Named("TaleWorlds.CampaignSystem.GameMenus.GameMenu");
        var calls = Calls(menu.GetMethod("RunOnInit")!);

        Assert.IsTrue(calls.Any(c => c.EndsWith(".OnGameMenuOpened", StringComparison.Ordinal)), string.Join(", ", calls));
        var events = Named("TaleWorlds.CampaignSystem.CampaignEvents");
        var type = events.GetProperty("GameMenuOpened", BindingFlags.Public | BindingFlags.Static)!.PropertyType;
        Assert.AreEqual("MenuCallbackArgs", type.GetGenericArguments().Single().Name);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void ASaveRaisesOnBeforeSave_BeforeItStartsWriting()
    {
        RequireGame();
        var handler = Named("TaleWorlds.CampaignSystem.SaveHandler");
        var calls = Calls(handler.GetMethod("SaveTick", AllMembers)!);

        int before = calls.FindIndex(c => c.EndsWith(".OnBeforeSave", StringComparison.Ordinal));
        int save = calls.FindIndex(c => c.StartsWith("MBSaveLoad.", StringComparison.Ordinal));
        Assert.IsTrue(before >= 0 && save > before, string.Join(", ", calls));
        var events = Named("TaleWorlds.CampaignSystem.CampaignEvents");
        Assert.AreEqual(0, events.GetProperty("OnBeforeSaveEvent", BindingFlags.Public | BindingFlags.Static)!.PropertyType.GetGenericArguments().Length);
    }

    // ---- The player's own siege, both sides (deep review H3)

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MobilePartySiegeEvent_IsTheBesiegerCampsEvent_SoItIsNullForADefender()
    {
        RequireGame();
        var party = Named("TaleWorlds.CampaignSystem.Party.MobileParty");
        var getter = party.GetProperty("SiegeEvent")!.GetMethod!;

        var calls = Calls(getter);

        CollectionAssert.Contains(calls, "MobileParty.get_BesiegerCamp");
        CollectionAssert.Contains(calls, "BesiegerCamp.get_SiegeEvent");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PlayerSiegeEvent_FallsBackToTheSiegeOfTheSettlementThePlayerIsIn()
    {
        RequireGame();
        var siege = Named("TaleWorlds.CampaignSystem.Siege.PlayerSiege");
        var getter = siege.GetProperty("PlayerSiegeEvent", BindingFlags.Public | BindingFlags.Static)!.GetMethod!;

        var calls = Calls(getter);

        CollectionAssert.Contains(calls, "MobileParty.get_SiegeEvent");
        CollectionAssert.Contains(calls, "MobileParty.get_CurrentSettlement");
        CollectionAssert.Contains(calls, "Settlement.get_SiegeEvent");
        Assert.AreEqual("BesiegedSettlement", siege.GetProperty("BesiegedSettlement", BindingFlags.Public | BindingFlags.Static)!.Name);
    }
}
