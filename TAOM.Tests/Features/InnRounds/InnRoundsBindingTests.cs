using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.InnRounds;

/// <summary>
/// Pins the vanilla seams Inn Rounds rides (#901) against the installed engine, with no campaign: the tavern keeper's two
/// dialog states and the lines they read, the gold, market, location, morale, relation and text calls the adapter makes, and the
/// two drink goods. The adapter's own source is pinned to the calls it must make.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class InnRoundsBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static MethodInfo Method(Type type, string name, params Type[] parameters)
    {
        RequireGame();
        var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, parameters, null);
        Assert.IsNotNull(method, type.Name + "." + name + " did not resolve.");
        return method!;
    }

    private static System.Collections.Generic.List<string> Strings(Type type)
    {
        RequireGame();
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsAbstract && m.GetMethodBody() != null)
            .SelectMany(m => PatchProcessor.GetOriginalInstructions(m))
            .Where(ci => ci.opcode == OpCodes.Ldstr)
            .Select(ci => (string)ci.operand)
            .ToList();
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheTavernKeepersStates_AndTheLinesWeReuse_ExistInTheInstalledEngine()
    {
        RequireGame();
        var type = AccessTools.TypeByName("SandBox.CampaignBehaviors.TavernEmployeesCampaignBehavior");
        Assert.IsNotNull(type, "TavernEmployeesCampaignBehavior moved.");
        var strings = Strings(type!);

        Assert.IsTrue(strings.Contains("tavernkeeper_talk"), "vanilla no longer has the tavernkeeper_talk state");
        Assert.IsTrue(strings.Contains("tavernkeeper_pretalk"), "vanilla no longer has the tavernkeeper_pretalk state");
        Assert.IsTrue(strings.Contains("{=ds294zxi}Anything else?"), "vanilla's 'Anything else?' key or default moved");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void ThePretalkState_IsLeftBy_AnAnythingElseLineBackToTheTalkState()
    {
        RequireGame();
        var type = AccessTools.TypeByName("SandBox.CampaignBehaviors.TavernEmployeesCampaignBehavior")!;
        var strings = Strings(type);

        // AddDialogLine("1972", "tavernkeeper_pretalk", "tavernkeeper_talk", "{=ds294zxi}Anything else?", ...): the four string
        // loads sit together in this order, so a replaced or reworded line breaks the pin before a player meets a dead end.
        var found = Enumerable.Range(0, strings.Count - 3).Any(i =>
            strings[i + 1] == "tavernkeeper_pretalk" && strings[i + 2] == "tavernkeeper_talk" && strings[i + 3] == "{=ds294zxi}Anything else?");
        Assert.IsTrue(found, "the pretalk line is no longer 'Anything else?' back to the talk state");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheGoldCalls_KeepTheirSignatures()
    {
        var pay = Method(typeof(GiveGoldAction), "ApplyForCharacterToSettlement", typeof(Hero), typeof(Settlement), typeof(int), typeof(bool));
        var back = Method(typeof(GiveGoldAction), "ApplyForSettlementToCharacter", typeof(Settlement), typeof(Hero), typeof(int), typeof(bool));

        Assert.IsTrue(pay.IsStatic && back.IsStatic);
        Assert.AreEqual(typeof(void), pay.ReturnType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheMarketPriceCall_StillTakesTheItemThePartyAndTheDirection()
    {
        var price = Method(typeof(TownMarketData), "GetPrice", typeof(ItemObject), typeof(MobileParty), typeof(bool), typeof(PartyBase));

        Assert.AreEqual(typeof(int), price.ReturnType);
        Assert.AreEqual("isSelling", price.GetParameters()[2].Name);
        Assert.IsNotNull(typeof(Town).GetProperty("MarketData"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheTavernCalls_KeepTheirSignatures()
    {
        var find = Method(typeof(LocationComplex), "GetLocationWithId", typeof(string));

        Assert.AreEqual(typeof(Location), find.ReturnType);
        Assert.AreEqual(typeof(int), typeof(Location).GetProperty("CharacterCount")?.PropertyType, "Location.CharacterCount moved");
        var mission = typeof(CampaignMission).GetProperty("Current");
        Assert.IsNotNull(mission, "CampaignMission.Current moved");
        Assert.AreEqual(typeof(Location), mission!.PropertyType.GetProperty("Location")?.PropertyType, "ICampaignMission.Location moved");
        Assert.IsNotNull(typeof(Settlement).GetProperty("LocationComplex"), "Settlement.LocationComplex moved");
        Assert.IsNotNull(typeof(Settlement).GetProperty("OwnerClan"), "Settlement.OwnerClan moved");
        Assert.IsNotNull(typeof(Settlement).GetField("Culture"), "Settlement.Culture moved");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheMoraleProperty_IsAReadWriteFloat_AndTheRelationReadIsAFloat()
    {
        RequireGame();
        var morale = typeof(MobileParty).GetProperty("RecentEventsMorale");
        Assert.IsNotNull(morale);
        Assert.AreEqual(typeof(float), morale!.PropertyType);
        Assert.IsTrue(morale.CanRead && morale.CanWrite);
        Assert.AreEqual(typeof(float), Method(typeof(Hero), "GetRelationWithPlayer").ReturnType);
        Assert.IsNotNull(typeof(TaleWorlds.CampaignSystem.Roster.TroopRoster).GetProperty("TotalManCount"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void TheClampOfTheMoraleSetter_IsInTheEngine()
    {
        RequireGame();
        var setter = typeof(MobileParty).GetProperty("RecentEventsMorale")!.GetSetMethod()!;
        var constants = PatchProcessor.GetOriginalInstructions(setter)
            .Where(ci => ci.opcode == OpCodes.Ldc_R4)
            .Select(ci => Convert.ToSingle(ci.operand))
            .ToList();

        Assert.IsTrue(constants.Contains(-100f) && constants.Contains(100f), "the setter no longer clamps to [-100, 100]: the 'landed' read-back is still right, but re-read the contract");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheTextAndOccupationAndRenownCalls_Exist()
    {
        var text = Method(typeof(GameTexts), "TryGetText", typeof(string), typeof(TaleWorlds.Localization.TextObject).MakeByRefType(), typeof(string));
        Assert.AreEqual(typeof(bool), text.ReturnType);
        Assert.IsTrue(Enum.IsDefined(typeof(Occupation), "Tavernkeeper"));
        Method(typeof(GainRenownAction), "Apply", typeof(Hero), typeof(float), typeof(bool));
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheCharmSkillId_IsCharm()
    {
        var strings = Strings(typeof(DefaultSkills));

        Assert.IsTrue(strings.Contains("Charm"), "DefaultSkills no longer creates the skill 'Charm'");
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void BeerAndWine_AreTheOnlyDrinkGoods_InSandBoxCore()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        var game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        var items = Path.Combine(game, "Modules", "SandBoxCore", "ModuleData", "items", "horses_and_others.xml");
        if (!File.Exists(items))
            Assert.Inconclusive("horses_and_others.xml not found at " + items);

        var xml = File.ReadAllText(items);
        foreach (var id in new[] { "beer", "wine" })
            Assert.IsTrue(Regex.IsMatch(xml, "<Item\\s+id=\"" + id + "\"[^>]*item_category=\"" + id + "\"", RegexOptions.Singleline), id + " is no longer a trade good");

        var drinks = Regex.Matches(xml, "<Item\\s+id=\"([^\"]+)\"[^>]*item_category=\"(beer|wine|mead|spirits|ale)\"", RegexOptions.Singleline)
            .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        CollectionAssert.AreEquivalent(new[] { "beer", "wine" }, drinks, "another drink good exists: revisit cultureDrinks");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheSkillXpRead_IsAPublicFloat_OnTheHeroDeveloper()
    {
        var read = Method(typeof(TaleWorlds.CampaignSystem.CharacterDevelopment.HeroDeveloper), "GetSkillXp", typeof(SkillObject));

        Assert.AreEqual(typeof(float), read.ReturnType);
        Assert.IsTrue(read.IsPublic);
    }
}
