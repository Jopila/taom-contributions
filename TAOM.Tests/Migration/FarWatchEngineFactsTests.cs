using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FarWatch;

namespace TAOM.Tests.Migration;

/// <summary>
/// The engine facts Far Watch (#847) rests on, one pin each, read from the installed assemblies (v1.5.5 when written; the
/// design's file and line citations are in docs/features/far-watch.md). If an engine update moves one, the feature would
/// silently stop working in a way no unit test of TAOM's own code can see, so each fails by name here. IL pins say what a call
/// list can show (a call is present, one comes before another), nothing about branches.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FarWatchEngineFactsTests
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

    /// <summary>Every called method as "Type.Name", in IL order, for call, callvirt and newobj.</summary>
    private static List<string> Calls(MethodBase method) =>
        Il(method).Where(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Newobj) && ci.operand is MethodBase)
            .Select(ci => ((MethodBase)ci.operand).DeclaringType!.Name + "." + ((MethodBase)ci.operand).Name).ToList();

    /// <summary>The method with exactly these parameter types (none for an empty list): a pin must read the overload it names, not the first of that name.</summary>
    private static MethodInfo Method(Type type, string name, params Type[] parameters)
    {
        var method = type.GetMethod(name, AllMembers, null, parameters, null);
        Assert.IsNotNull(method, type.Name + "." + name + "(" + string.Join(", ", parameters.Select(t => t.Name)) + ") did not resolve.");
        return method!;
    }

    // ---- 1. The sight cap

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void DefaultMapVisibilityModel_CapsTheNumberLast_WithMaximumSeeingRange_AfterItsLastAddFactor()
    {
        RequireGame();
        var model = Named("TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel");
        var calls = Calls(Method(model, "GetPartySpottingRange", Named("TaleWorlds.CampaignSystem.Party.MobileParty"), typeof(bool)));

        int max = calls.LastIndexOf("MapVisibilityModel.MaximumSeeingRange");
        int limit = calls.LastIndexOf("ExplainedNumber.LimitMax");
        int lastFactor = calls.LastIndexOf("ExplainedNumber.AddFactor");

        Assert.IsTrue(max >= 0, "the cap reads MaximumSeeingRange: " + string.Join(", ", calls));
        Assert.IsTrue(limit > max, "and LimitMax comes after it");
        Assert.IsTrue(limit > lastFactor, "and after the last AddFactor, which is why a factor added afterwards is still clamped");
        Assert.AreEqual(limit, calls.FindLastIndex(c => c.StartsWith("ExplainedNumber.", StringComparison.Ordinal)),
            "LimitMax is the last thing the method does to the number");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MaximumSeeingRange_HasExactlyOneCaller_InTheCampaignAssembly()
    {
        RequireGame();
        var model = Named("TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel");

        var callers = IlCallScanner.FindCallers(model.Assembly,
            m => m.Name == "MaximumSeeingRange" && m.DeclaringType != null && m.DeclaringType.Name.EndsWith("MapVisibilityModel", StringComparison.Ordinal),
            out var unreadable, out int scanned);

        Assert.IsTrue(scanned > 1000, "the scan walked the assembly: " + scanned);
        Assert.AreEqual(0, unreadable.Count, "unreadable bodies cannot vouch for the count: " + string.Join("; ", unreadable));
        CollectionAssert.AreEqual(new[] { "TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel.GetPartySpottingRange" }, callers,
            "so raising the cap moves nothing but the sight range");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void ExplainedNumber_TheLastLimitMaxWins_AndTheResultClampsToIt()
    {
        RequireGame();
        var number = new TaleWorlds.CampaignSystem.ExplainedNumber(80f);
        number.LimitMax(60f);
        Assert.AreEqual(60f, number.ResultNumber);

        number.LimitMax(90f);

        Assert.AreEqual(80f, number.ResultNumber);
    }

    // ---- 1b. The visibility pass and its search grid

    private const BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static Type PartyGrid() =>
        Named("TaleWorlds.CampaignSystem.Map.LocatorGrid`1").MakeGenericType(Named("TaleWorlds.CampaignSystem.Party.MobileParty"));

    /// <summary>
    /// The visibility pass searches the locator grid with radius = SeeingRange, every frame. The facts that decide what a larger
    /// radius costs: the grid is 32 cells of 5 map units a side; <c>GetBoundaries</c> takes a span of <c>floor((x + r) / 5) -
    /// floor((x - r) / 5)</c> cells per axis and caps it at width - 1 (31), so from about r = 75 most positions walk a whole axis;
    /// and the cell index wraps by modulo (<c>minX %= _width</c>), so a full walk visits nearly every party. There is NO step in
    /// that cost: it grows smoothly with the radius (docs/features/far-watch.md "What a higher cap costs"). The sight cap only
    /// bounds the growth, at 60 x (1 + <see cref="TAOM.Core.Domain.SpottingContributorComposer.MaxCapBonus"/>).
    /// </summary>
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void LocatorGrid_SpanIsFloorDifferenceCappedAtWidthLessOne_AndWrapsByModulo()
    {
        RequireGame();
        var open = Named("TaleWorlds.CampaignSystem.Map.LocatorGrid`1");
        float node = (float)open.GetField("DefaultGridNodeSize", AllMembers | BindingFlags.Static)!.GetRawConstantValue()!;
        int width = (int)open.GetField("DefaultGridWidth", AllMembers | BindingFlags.Static)!.GetRawConstantValue()!;
        int height = (int)open.GetField("DefaultGridHeight", AllMembers | BindingFlags.Static)!.GetRawConstantValue()!;
        Assert.AreEqual(5f, node);
        Assert.AreEqual(32, width);
        Assert.AreEqual(32, height);

        var vec = Named("TaleWorlds.Library.Vec2");
        var boundaries = Method(PartyGrid(), "GetBoundaries", vec, typeof(float),
            typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(int).MakeByRefType());
        var il = Il(boundaries);
        var calls = Calls(boundaries);
        Assert.AreEqual(2, calls.Count(c => c == "Math.Min"), "the span is capped to width - 1 and height - 1 cells: " + string.Join(", ", calls));
        Assert.IsTrue(il.Count(ci => ci.opcode == OpCodes.Sub) >= 4, "the span is max - min, and the cap is the grid size less one");
        Assert.AreEqual(2, calls.Count(c => c.EndsWith(".GetGridIndices", StringComparison.Ordinal)), "the corners come from the floored cell indices");
        Assert.IsTrue(il.Count(ci => ci.opcode == OpCodes.Rem) >= 2, "the first cell wraps by modulo, so a wide search wraps round the grid");

        var indices = Method(PartyGrid(), "GetGridIndices", vec, typeof(int).MakeByRefType(), typeof(int).MakeByRefType());
        Assert.IsTrue(Calls(indices).Any(c => c.EndsWith(".Floor", StringComparison.Ordinal)), "an index is the floored position over the node size");

        var cell = Method(PartyGrid(), "StartFindingLocatablesAroundPosition", vec, typeof(float));
        Assert.IsTrue(Calls(cell).Any(c => c.EndsWith(".GetBoundaries", StringComparison.Ordinal)), "a search starts from those boundaries");
    }

    /// <summary>
    /// Vanilla already searches the same grid every frame at SeeingRange + 25 for the footstep sounds, so a raised sight costs on
    /// two searches, not one. The IL shows the +25 added to the main party's SeeingRange before the search starts.
    /// </summary>
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MapScreen_TickStepSounds_SearchesTheGridAtSeeingRangePlusTwentyFive()
    {
        RequireGame();
        var screen = Named("SandBox.View.Map.MapScreen");
        var method = Method(screen, "TickStepSounds", typeof(float));
        var il = Il(method);

        int range = il.FindIndex(ci => ci.operand is MethodBase m && m.Name == "get_SeeingRange");
        Assert.IsTrue(range >= 0, "it reads the main party's SeeingRange: " + string.Join(", ", Calls(method)));
        int plus = il.FindIndex(range, ci => ci.opcode == OpCodes.Ldc_R4 && ci.operand is float f && f == 25f);
        Assert.IsTrue(plus > range, "and adds 25");
        Assert.AreEqual(OpCodes.Add, il[plus + 1].opcode);
        int search = il.FindIndex(plus, ci => ci.operand is MethodBase m && m.Name == "StartFindingLocatablesAroundPosition");
        Assert.IsTrue(search > plus, "before it starts a locator search");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void TheCampaignsMobilePartyLocator_IsAGridWithTheDefaultShape()
    {
        RequireGame();
        var getter = Named("TaleWorlds.CampaignSystem.Campaign").GetProperty("MobilePartyLocator", AllMembers | BindingFlags.Static | BindingFlags.Instance)!.GetMethod!;
        var il = Il(getter);

        int ctor = il.FindIndex(ci => ci.opcode == OpCodes.Newobj && ci.operand is ConstructorInfo c && c.DeclaringType!.Name.StartsWith("LocatorGrid", StringComparison.Ordinal));
        Assert.IsTrue(ctor >= 3, "the property builds a LocatorGrid: " + string.Join(", ", Calls(getter)));
        Assert.AreEqual(OpCodes.Ldc_R4, il[ctor - 3].opcode, "with the default node size");
        Assert.AreEqual(5f, (float)il[ctor - 3].operand);
        Assert.AreEqual(32, Convert.ToInt32(il[ctor - 2].operand ?? (il[ctor - 2].opcode == OpCodes.Ldc_I4_S ? 32 : 0)), "and the default width");
    }

    // ---- 2. Wait menus

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void GameMenu_SwitchToMenu_StartsAWaitMenusWait()
    {
        RequireGame();
        var calls = Calls(Method(Named("TaleWorlds.CampaignSystem.GameMenus.GameMenu"), "SwitchToMenu", typeof(string)));

        CollectionAssert.Contains(calls, "GameMenu.StartWait", string.Join(", ", calls));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void GameMenu_RunOnTick_EndsTheWaitBeforeItRunsTheConsequence()
    {
        RequireGame();
        var calls = Calls(Method(Named("TaleWorlds.CampaignSystem.GameMenus.GameMenu"), "RunOnTick", Named("TaleWorlds.CampaignSystem.GameState.MenuContext"), typeof(float)));

        int end = calls.IndexOf("GameMenu.EndWait");
        int consequence = calls.IndexOf("GameMenu.RunWaitMenuConsequence");
        Assert.IsTrue(end >= 0 && consequence > end, string.Join(", ", calls));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void GameMenu_RunMenuOptionConsequence_EndsTheWaitBeforeAnOptionsConsequence()
    {
        RequireGame();
        var calls = Calls(Method(Named("TaleWorlds.CampaignSystem.GameMenus.GameMenu"), "RunMenuOptionConsequence",
            Named("TaleWorlds.CampaignSystem.GameState.MenuContext"), typeof(int)));

        int end = calls.IndexOf("GameMenu.EndWait");
        int consequence = calls.IndexOf("GameMenuOption.RunConsequence");
        Assert.IsTrue(end >= 0 && consequence > end, string.Join(", ", calls));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MapState_OnLoadingFinished_RestartsAWaitMenuTheSaveWasIn()
    {
        RequireGame();
        var calls = Calls(Method(Named("TaleWorlds.CampaignSystem.GameState.MapState"), "OnLoadingFinished"));

        CollectionAssert.Contains(calls, "GameMenu.StartWait", string.Join(", ", calls));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void GameMenu_AddOption_InsertsOnlyWhenTheCountReachesTheIndex()
    {
        RequireGame();
        var add = Method(Named("TaleWorlds.CampaignSystem.GameMenus.GameMenu"), "AddOption",
            Named("TaleWorlds.CampaignSystem.GameMenus.GameMenuOption"), typeof(int));
        var il = Il(add);

        int count = il.FindIndex(ci => ci.operand is MethodBase m && m.Name == "get_Count");
        Assert.IsTrue(count >= 0, "the method reads the item list's Count");
        Assert.IsTrue(il.Skip(count).Take(6).Any(ci => ci.opcode == OpCodes.Ldarg_2 || ci.opcode == OpCodes.Ldarg_S),
            "and compares it with the index argument");
        Assert.IsTrue(il.Skip(count).Take(6).Any(ci => ci.opcode == OpCodes.Blt || ci.opcode == OpCodes.Blt_S),
            "branching to the plain append when the count is below the index (an insert needs Count >= index)");
        Assert.IsTrue(Calls(add).Contains("List`1.Insert") && Calls(add).Contains("List`1.Add"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MbEvent_ListenersAreInsertedAtTheHead_SoTheLastRegisteredRunsFirst()
    {
        RequireGame();
        var type = Named("TaleWorlds.CampaignSystem.MbEvent");
        var evt = Activator.CreateInstance(type)!;
        var order = new List<string>();
        var add = type.GetMethod("AddNonSerializedListener")!;
        add.Invoke(evt, new object[] { "first", (Action)(() => order.Add("first")) });
        add.Invoke(evt, new object[] { "second", (Action)(() => order.Add("second")) });

        type.GetMethod("Invoke")!.Invoke(evt, null);

        CollectionAssert.AreEqual(new[] { "second", "first" }, order, "the behavior added last handles the event first");
    }

    // ---- 3. The player's map events

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MapEvent_Initialize_PutsThePartiesOnTheirSides_BeforeItRaisesMapEventStarted()
    {
        RequireGame();
        var calls = Calls(Method(Named("TaleWorlds.CampaignSystem.MapEvents.MapEvent"), "Initialize", Named("TaleWorlds.CampaignSystem.Party.PartyBase"),
            Named("TaleWorlds.CampaignSystem.Party.PartyBase"), Named("TaleWorlds.CampaignSystem.MapEvents.MapEventComponent")));

        int sides = calls.LastIndexOf("PartyBase.set_MapEventSide");
        int raised = calls.FindIndex(c => c.EndsWith(".OnMapEventStarted", StringComparison.Ordinal));
        Assert.IsTrue(sides >= 0 && raised > sides, "the listener sees the main party already in the event: " + string.Join(", ", calls));
        Assert.AreEqual("MapEvent,PartyBase,PartyBase",
            string.Join(",", Named("TaleWorlds.CampaignSystem.CampaignEvents").GetProperty("MapEventStarted", BindingFlags.Public | BindingFlags.Static)!
                .PropertyType.GetGenericArguments().Select(a => a.Name)), "(also pinned by EnlistmentTickBindingTests)");
    }

    /// <summary>
    /// An attack on the camp is a party encounter. <c>EncounterManager.StartPartyEncounter</c> ends by raising the encounter event, for
    /// every encounter in the world, after the player encounter has been (re)started, so a listener sees the main party's
    /// meeting or battle whichever menu the engine chose.
    /// </summary>
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void EncounterManager_StartPartyEncounter_RaisesOnPartyEncounter_AsItsLastCall()
    {
        RequireGame();
        var party = Named("TaleWorlds.CampaignSystem.Party.PartyBase");
        var calls = Calls(Method(Named("TaleWorlds.CampaignSystem.EncounterManager"), "StartPartyEncounter", party, party));

        Assert.AreEqual("CampaignEventReceiver.OnPartyEncounter", calls[calls.Count - 1], "raised through the dispatcher's receiver: " + string.Join(", ", calls));
        Assert.AreEqual("CampaignEventDispatcher.get_Instance", calls[calls.Count - 2]);
        Assert.IsTrue(calls.IndexOf("EncounterManager.RestartPlayerEncounter") < calls.Count - 1 && calls.Contains("EncounterManager.RestartPlayerEncounter"),
            "after the player encounter is restarted: " + string.Join(", ", calls));

        var events = Named("TaleWorlds.CampaignSystem.CampaignEvents");
        Assert.AreEqual("PartyBase,PartyBase", string.Join(",", events.GetProperty("OnPartyEncounterEvent", BindingFlags.Public | BindingFlags.Static)!
            .PropertyType.GetGenericArguments().Select(a => a.Name)));
        var raise = Method(events, "OnPartyEncounter", party, party);
        CollectionAssert.Contains(Calls(raise), "MbEvent`2.Invoke", string.Join(", ", Calls(raise)));
    }

    /// <summary>
    /// The meeting an attack on the camp can open is <c>encounter_meeting</c>, with the out parameters startBattle and joinBattle
    /// left at their defaults (false): <c>PlayerEncounter.Init</c> then starts no battle, so <c>MapEventStarted</c> is never raised for
    /// it. The IL shows the string and the two false defaults; that it is the answer for a mobile party with no map event is read in the
    /// decompile (DefaultEncounterGameMenuModel.cs, the last return of GetEncounterMenu).
    /// </summary>
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void GetEncounterMenu_CanAnswerAPlainMeeting_WithNoBattleStarted()
    {
        RequireGame();
        var party = Named("TaleWorlds.CampaignSystem.Party.PartyBase");
        var model = Named("TaleWorlds.CampaignSystem.GameComponents.DefaultEncounterGameMenuModel");
        var method = Method(model, "GetEncounterMenu", party, party, typeof(bool).MakeByRefType(), typeof(bool).MakeByRefType());
        var il = Il(method);

        Assert.IsTrue(il.Any(ci => ci.opcode == OpCodes.Ldstr && (string)ci.operand == "encounter_meeting"), "the meeting menu is an answer");
        int falseDefaults = 0;
        for (int i = 0; i + 2 < Math.Min(il.Count, 40); i++)
            if ((il[i].opcode == OpCodes.Ldarg_S || il[i].opcode == OpCodes.Ldarg_3 || il[i].opcode == OpCodes.Ldarg_S) && il[i + 1].opcode == OpCodes.Ldc_I4_0 && il[i + 2].opcode == OpCodes.Stind_I1)
                falseDefaults++;
        Assert.AreEqual(2, falseDefaults, "startBattle and joinBattle start false");
        CollectionAssert.Contains(Calls(Method(Named("TaleWorlds.CampaignSystem.Encounters.PlayerEncounter"), "Init", party, party,
            Named("TaleWorlds.CampaignSystem.Settlements.Settlement"))), "PlayerEncounter.StartBattle", "Init starts a battle only on the answer's say-so");
    }

    // ---- 4. Heroes, roles and the settlements near

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void Hero_SetPartyBelongedTo_StripsEveryRoleOfAHeroWhoLeaves()
    {
        RequireGame();
        var calls = Calls(Method(Named("TaleWorlds.CampaignSystem.Hero"), "SetPartyBelongedTo", Named("TaleWorlds.CampaignSystem.Party.MobileParty")));

        CollectionAssert.Contains(calls, "MobileParty.RemoveAllPartyRolesOfHero", string.Join(", ", calls));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void TownAllFiefs_YieldsTheTownsAndTheCastles_FromTheCampaignsTwoLists()
    {
        RequireGame();
        var town = Named("TaleWorlds.CampaignSystem.Settlements.Town");
        var getter = town.GetProperty("AllFiefs", BindingFlags.Public | BindingFlags.Static)!.GetMethod!;
        var iterator = town.GetNestedTypes(BindingFlags.NonPublic).Single(t => t.Name.Contains("get_AllFiefs"));
        var calls = Calls(iterator.GetMethod("MoveNext", AllMembers)!);

        CollectionAssert.Contains(calls, "Campaign.get_AllTowns", string.Join(", ", calls));
        CollectionAssert.Contains(calls, "Campaign.get_AllCastles");
        Assert.IsTrue(calls.IndexOf("Campaign.get_AllTowns") < calls.IndexOf("Campaign.get_AllCastles"), "towns first, then castles");
        Assert.IsNotNull(getter);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void FormationClass_HasTheOrdinalsTheBucketMappingReads()
    {
        RequireGame();
        var formation = Named("TaleWorlds.Core.FormationClass");

        Assert.AreEqual(0, (int)Enum.Parse(formation, "Infantry"));
        Assert.AreEqual(1, (int)Enum.Parse(formation, "Ranged"));
        Assert.AreEqual(2, (int)Enum.Parse(formation, "Cavalry"));
        Assert.AreEqual(3, (int)Enum.Parse(formation, "HorseArcher"));
        Assert.AreEqual(4, (int)Enum.Parse(formation, "Skirmisher"));
        Assert.AreEqual(5, (int)Enum.Parse(formation, "HeavyInfantry"));
        Assert.AreEqual(6, (int)Enum.Parse(formation, "LightCavalry"));
        Assert.AreEqual(7, (int)Enum.Parse(formation, "HeavyCavalry"));
        Assert.AreEqual(8, (int)Enum.Parse(formation, "General"), "the first class the mapping leaves out");
        foreach (var name in new[] { "Infantry", "HeavyInfantry" }) Assert.AreEqual(0, FarWatchMath.BucketOf((int)Enum.Parse(formation, name)), name);
        foreach (var name in new[] { "Ranged", "Skirmisher" }) Assert.AreEqual(1, FarWatchMath.BucketOf((int)Enum.Parse(formation, name)), name);
        foreach (var name in new[] { "Cavalry", "LightCavalry", "HeavyCavalry" }) Assert.AreEqual(2, FarWatchMath.BucketOf((int)Enum.Parse(formation, name)), name);
        Assert.AreEqual(3, FarWatchMath.BucketOf((int)Enum.Parse(formation, "HorseArcher")));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheMembersTheAdapterAndHooksCall_KeepTheirShapes()
    {
        RequireGame();
        var party = Named("TaleWorlds.CampaignSystem.Party.MobileParty");
        var hero = Named("TaleWorlds.CampaignSystem.Hero");
        var role = Named("TaleWorlds.CampaignSystem.PartyRole");
        var menu = Named("TaleWorlds.CampaignSystem.GameMenus.GameMenu");

        Assert.IsNotNull(party.GetMethod("GetRoleHolder", new[] { role }), "MobileParty.GetRoleHolder(PartyRole)");
        Assert.IsTrue(Enum.IsDefined(role, "Scout"));
        Assert.AreEqual("Hero", party.GetMethod("GetRoleHolder", new[] { role })!.ReturnType.Name);
        Assert.AreEqual(typeof(bool), hero.GetProperty("IsWounded")!.PropertyType);
        Assert.AreEqual(typeof(bool), hero.GetProperty("IsPrisoner")!.PropertyType);
        Assert.IsNotNull(hero.GetMethod("AddSkillXp", new[] { Named("TaleWorlds.Core.SkillObject"), typeof(float) }), "Hero.AddSkillXp(SkillObject, float)");
        Assert.IsNotNull(hero.GetProperty("AllAliveHeroes", BindingFlags.Public | BindingFlags.Static), "Hero.AllAliveHeroes");
        Assert.IsNotNull(Named("TaleWorlds.CampaignSystem.CharacterObject").GetMethod("GetFormationClass"), "CharacterObject.GetFormationClass()");
        var vec = Named("TaleWorlds.Library.Vec2");
        var start = party.GetMethod("StartFindingLocatablesAroundPosition", new[] { vec, typeof(float) });
        Assert.IsNotNull(start, "MobileParty.StartFindingLocatablesAroundPosition(Vec2, float)");
        Assert.IsTrue(start!.IsStatic);
        Assert.IsNotNull(Named("TaleWorlds.Library.InformationManager").GetMethod("ShowInquiry",
            new[] { Named("TaleWorlds.Library.InquiryData"), typeof(bool), typeof(bool) }), "InformationManager.ShowInquiry(InquiryData, bool, bool)");
        Assert.IsNotNull(menu.GetMethod("SetTargetedWaitingTimeAndInitialProgress", new[] { typeof(float), typeof(float) }));
        Assert.IsNotNull(menu.GetMethod("SetProgressOfWaitingInMenu", new[] { typeof(float) }));
        Assert.IsNotNull(menu.GetMethod("EndWait", Type.EmptyTypes));
        Assert.IsTrue(menu.GetProperty("IsWaitActive")!.CanRead);
        var leave = Named("TaleWorlds.CampaignSystem.GameMenus.GameMenuOption+LeaveType");
        foreach (var name in new[] { "Submenu", "Wait", "Leave", "Mission" }) Assert.IsTrue(Enum.IsDefined(leave, name), name);
        var events = Named("TaleWorlds.CampaignSystem.CampaignEvents");
        Assert.AreEqual("CampaignGameStarter",
            events.GetProperty("OnAfterSessionLaunchedEvent", BindingFlags.Public | BindingFlags.Static)!.PropertyType.GetGenericArguments().Single().Name);
        Assert.AreEqual(0, events.GetProperty("HourlyTickEvent", BindingFlags.Public | BindingFlags.Static)!.PropertyType.GetGenericArguments().Length);
    }
}
