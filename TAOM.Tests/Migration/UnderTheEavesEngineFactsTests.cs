using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.UnderTheEaves;

namespace TAOM.Tests.Migration;

/// <summary>
/// The engine facts Under the Eaves (#849) rests on, one pin each, read from the installed assemblies (v1.5.5 when written).
/// If an engine update moves one, the forest rule would silently read the wrong terrain or double an effect vanilla already
/// applies, in a way no unit test of TAOM's own code can see, so each fails by name here. IL pins say what a call list or a
/// constant load can show (a call is present, one comes before another, a literal is loaded), nothing about branches.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class UnderTheEavesEngineFactsTests
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

    private static MethodInfo Method(Type type, string name, params Type[] parameters)
    {
        var method = type.GetMethod(name, AllMembers, null, parameters, null);
        Assert.IsNotNull(method, type.Name + "." + name + "(" + string.Join(", ", parameters.Select(t => t.Name)) + ") did not resolve.");
        return method!;
    }

    private static Type MobileParty() => Named("TaleWorlds.CampaignSystem.Party.MobileParty");

    /// <summary>The terrain test the engine makes: the face of a party, then its terrain type from the scene.</summary>
    private static void AssertReadsAPartysFaceAndItsTerrainType(List<string> calls, string where)
    {
        int face = calls.IndexOf("MobileParty.get_CurrentNavigationFace");
        int terrain = calls.IndexOf("IMapScene.GetFaceTerrainType");
        Assert.IsTrue(face >= 0, where + " reads MobileParty.CurrentNavigationFace: " + string.Join(", ", calls));
        Assert.IsTrue(terrain > face, where + " then asks the map scene for that face's terrain type: " + string.Join(", ", calls));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void DefaultMapVisibilityModel_GetPartySpottingRange_ReadsTheSpottersOwnFace_AndItsTerrainType()
    {
        RequireGame();
        var model = Named("TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel");

        var calls = Calls(Method(model, "GetPartySpottingRange", MobileParty(), typeof(bool)));

        AssertReadsAPartysFaceAndItsTerrainType(calls, "the sight range (one source for the Dunland feat and the forest rule)");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MobileParty_CurrentNavigationFace_IsThePositionsFace()
    {
        RequireGame();
        var getter = MobileParty().GetProperty("CurrentNavigationFace", AllMembers)!.GetGetMethod(true)!;

        var calls = Calls(getter);

        CollectionAssert.AreEqual(new[] { "MobileParty.get_Position", "CampaignVec2.get_Face" }, calls,
            "the party's own cached face, not a fresh scene lookup: the adapter reads the very member vanilla reads");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void MapScene_GetFaceTerrainType_IsTheFaceGroupIndex_SoTheAdapterMayReadItDirectly()
    {
        RequireGame();
        var scene = Named("SandBox.MapScene");
        var face = Named("TaleWorlds.Library.PathFaceRecord");

        var method = Method(scene, "GetFaceTerrainType", face);
        var loads = Il(method).Where(ci => ci.opcode == OpCodes.Ldfld && ci.operand is FieldInfo).Select(ci => ((FieldInfo)ci.operand).Name).ToList();

        CollectionAssert.AreEqual(new[] { "FaceGroupIndex" }, loads, "the terrain type IS the face group index");
        Assert.IsTrue(Il(method).Any(ci => ci.opcode == OpCodes.Call && ci.operand is MethodBase m && m.Name == "IsValid"),
            "after an IsValid check: an invalid face raises Debug.FailedAssert, which the adapter avoids by checking first");
    }

    /// <summary>
    /// Vanilla already hides a party that stands in forest: the TARGET side of the same test, in the ratio method. That is why this
    /// rule is the spotter's side only and the target's is left alone (docs/features/under-the-eaves.md). The literal read here is
    /// the shared constant: a forest target keeps 0.7 of its ratio.
    /// </summary>
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void DefaultMapVisibilityModel_TheRatioMethod_ComparesTheTargetsTerrainWithForest_AndLoadsMinusPointThree()
    {
        RequireGame();
        var model = Named("TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel");
        var method = Method(model, "GetPartySpottingRatioForMainPartySeeingRange", MobileParty());

        AssertReadsAPartysFaceAndItsTerrainType(Calls(method), "the target ratio");
        var il = Il(method);
        int forest = il.FindIndex(ci => ci.opcode == OpCodes.Ldc_I4_4);
        Assert.IsTrue(forest >= 0, "it compares the terrain with the constant 4 (Forest)");
        Assert.IsTrue(il.Any(ci => ci.opcode == OpCodes.Ldc_R4 && ci.operand is float f && Math.Abs(f - -0.3f) < 0.00001f),
            "and loads the -0.3 forest factor");
    }

    /// <summary>
    /// Keen Sight is the vanilla perk for a forest sight penalty ("{VALUE}% sight penalty for traveling in forests"): its scout role
    /// and its -0.5 primary bonus are what the rule's <c>KeenSightShare</c> (0.5, the share of the penalty that stays) mirrors, and
    /// the contributor asks the party for it through <c>HasPerk</c> as vanilla's target ratio does. Read from the perk's initializer,
    /// because the perk objects exist only inside a running game.
    /// </summary>
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void KeenSight_IsTheScoutsForestSightPerk_WithAPrimaryBonusOfMinusHalf()
    {
        RequireGame();
        var perks = Named("TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks");
        var role = Named("TaleWorlds.CampaignSystem.PartyRole");
        var il = Il(Method(perks, "InitializeAll"));

        int text = il.FindIndex(ci => ci.opcode == OpCodes.Ldstr && ci.operand is string str && str.Contains("sight penalty for traveling in forests"));
        Assert.IsTrue(text >= 0, "the Keen Sight description is in DefaultPerks.InitializeAll");
        Assert.AreEqual(Convert.ToInt32(Enum.Parse(role, "Scout")), Convert.ToInt32(il[text + 1].operand ?? ConstantOf(il[text + 1])),
            "the primary role is Scout (the party's effective scout owns the perk)");
        Assert.IsTrue(il[text + 2].opcode == OpCodes.Ldc_R4 && il[text + 2].operand is float bonus && Math.Abs(bonus - -0.5f) < 0.00001f,
            "the primary bonus is -0.5: the penalty keeps half, the rule's KeenSightShare");
        Assert.AreEqual(UnderTheEavesRule.KeenSightShare, 1f + -0.5f, 0.00001f);
    }

    /// <summary>The value of a short-form integer load (ldc.i4.0 to ldc.i4.8), whose operand is null.</summary>
    private static int ConstantOf(CodeInstruction instruction) =>
        instruction.opcode == OpCodes.Ldc_I4_0 ? 0 : instruction.opcode == OpCodes.Ldc_I4_1 ? 1 : instruction.opcode == OpCodes.Ldc_I4_2 ? 2
        : instruction.opcode == OpCodes.Ldc_I4_3 ? 3 : instruction.opcode == OpCodes.Ldc_I4_4 ? 4 : instruction.opcode == OpCodes.Ldc_I4_5 ? 5
        : instruction.opcode == OpCodes.Ldc_I4_6 ? 6 : instruction.opcode == OpCodes.Ldc_I4_7 ? 7 : 8;

    /// <summary>
    /// The one forest effect vanilla gives a spotter itself: a holder of the Battanian forest speed feat gets +0.15 sight in forest (TAOM
    /// gives that feat to Dunland and Khand), which nets against the penalty to -0.15 for them. The rule leaves it alone.
    /// </summary>
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void DefaultMapVisibilityModel_GetPartySpottingRange_GivesTheBattanianForestFeatPointFifteen()
    {
        RequireGame();
        var model = Named("TaleWorlds.CampaignSystem.GameComponents.DefaultMapVisibilityModel");
        var method = Method(model, "GetPartySpottingRange", MobileParty(), typeof(bool));

        Assert.IsTrue(Calls(method).Contains("DefaultCulturalFeats.get_BattanianForestSpeedFeat"), "it asks for the Battanian forest feat");
        Assert.IsTrue(Il(method).Any(ci => ci.opcode == OpCodes.Ldc_R4 && ci.operand is float f && Math.Abs(f - 0.15f) < 0.00001f),
            "and adds the 0.15 factor");
    }
}
