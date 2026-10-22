using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>
/// #870 slice 2: the tribute envoy's adapters are engine-bound, so their IL is the testable surface. These pin the engine
/// members the slice depends on (v1.5.5): the player's Charm and kingdom, the one debit path, the multi-selection inquiry,
/// the shared map-free test, and the two readings the raid adapter gained (a village's owning clan, the main party).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TributeEnvoyAdapterIlTests
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static MethodBase[] CallsOf(System.Type type, string methodName)
    {
        var method = type.GetMethod(methodName, All)!;
        return IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToArray();
    }

    private static bool Calls(MethodBase[] calls, System.Type type, string name) =>
        calls.Any(m => m.DeclaringType == type && m.Name == name);

    [TestMethod]
    public void ReadPlayer_ReadsCharmAndTheClansKingdom_FromTheMainHeroAndThePlayerClan()
    {
        var calls = CallsOf(typeof(TributeEnvoyAdapter), nameof(TributeEnvoyAdapter.ReadPlayer));

        Assert.IsTrue(Calls(calls, typeof(Hero), "get_MainHero"));
        Assert.IsTrue(Calls(calls, typeof(Clan), "get_PlayerClan"));
        Assert.IsTrue(Calls(calls, typeof(Clan), "get_Kingdom"));
        Assert.IsTrue(Calls(calls, typeof(Hero), nameof(Hero.GetSkillValue)));
        Assert.IsTrue(Calls(calls, typeof(DefaultSkills), "get_Charm"));
    }

    [TestMethod]
    public void ReadPlayer_CountsTheVillagesBoundToTheClansFiefs_NotClanVillages()
    {
        var calls = CallsOf(typeof(TributeEnvoyAdapter), "CountVillages");
        var read = CallsOf(typeof(TributeEnvoyAdapter), nameof(TributeEnvoyAdapter.ReadPlayer));

        Assert.IsTrue(Calls(calls, typeof(Clan), "get_Fiefs"));
        Assert.IsTrue(Calls(calls, typeof(TaleWorlds.CampaignSystem.Settlements.Town), "get_Villages"));
        Assert.IsFalse(Calls(calls, typeof(Clan), "get_Villages"));
        Assert.IsFalse(Calls(read, typeof(Clan), "get_Villages"));
        Assert.IsFalse(Calls(calls, typeof(TaleWorlds.CampaignSystem.Settlements.Village), "get_All"), "the count must not walk every village in the world");
        Assert.IsFalse(Calls(read, typeof(TaleWorlds.CampaignSystem.Settlements.Village), "get_All"), "the count must not walk every village in the world");
    }

    [TestMethod]
    public void ReadPlayer_GuardsTheCampaign_BeforeTouchingTheMainHero()
    {
        var calls = CallsOf(typeof(TributeEnvoyAdapter), nameof(TributeEnvoyAdapter.ReadPlayer));

        int campaign = System.Array.FindIndex(calls, m => m.DeclaringType == typeof(Campaign) && m.Name == "get_Current");
        int hero = System.Array.FindIndex(calls, m => m.DeclaringType == typeof(Hero) && m.Name == "get_MainHero");
        Assert.IsTrue(campaign >= 0, "ReadPlayer reads Campaign.Current");
        Assert.IsTrue(hero < 0 || campaign < hero, "Campaign.Current is checked before Hero.MainHero is read");
    }

    [TestMethod]
    public void Payment_GoesThroughTheOnePurseAdapter_NeverItsOwnGoldAction()
    {
        var calls = CallsOf(typeof(TributeEnvoyAdapter), nameof(TributeEnvoyAdapter.TryCharge));

        Assert.IsTrue(Calls(calls, typeof(ICareerPurseAdapter), nameof(ICareerPurseAdapter.ChargeGold)));
        Assert.IsFalse(calls.Any(m => m.Name.Contains("GiveGold")));
    }

    [TestMethod]
    public void ShowOffer_UsesTheMultiSelectionInquiry_AndNeverASceneNotification()
    {
        var calls = CallsOf(typeof(EnvoyPresenterAdapter), nameof(EnvoyPresenterAdapter.ShowOffer));

        Assert.IsTrue(Calls(calls, typeof(MBInformationManager), nameof(MBInformationManager.ShowMultiSelectionInquiry)));
        Assert.IsFalse(calls.Any(m => m.Name.Contains("SceneNotification")));
    }

    [TestMethod]
    public void IsMapFree_IsTheSharedGate()
    {
        var calls = CallsOf(typeof(EnvoyPresenterAdapter), nameof(EnvoyPresenterAdapter.IsMapFree));

        Assert.IsTrue(calls.Any(m => m.DeclaringType?.Name == "MapPopupGate" && m.Name == "IsMapFree"));
    }

    [TestMethod]
    public void SpareMainParty_UsesTheEnginesOwnDoNotAttackCall()
    {
        var calls = CallsOf(typeof(RaidHostAdapter), nameof(RaidHostAdapter.SpareMainParty));

        Assert.IsTrue(Calls(calls, typeof(MobilePartyAi), nameof(MobilePartyAi.SetDoNotAttackMainParty)));
    }

    [TestMethod]
    public void TheRaidAdapter_ReadsAVillagesOwnerThroughItsBoundFief_AndFlagsTheMainParty()
    {
        var reading = typeof(RaidHostAdapter).GetMethod("ReadingOf", All)!;
        var readingCalls = IlCallScanner.ExtractCalledMethods(reading, reading.GetMethodBody()!.GetILAsByteArray()).ToArray();
        var near = CallsOf(typeof(RaidHostAdapter), nameof(RaidHostAdapter.PartiesNear));

        Assert.IsTrue(Calls(readingCalls, typeof(TaleWorlds.CampaignSystem.Settlements.Village), "get_Bound"));
        Assert.IsTrue(Calls(readingCalls, typeof(TaleWorlds.CampaignSystem.Settlements.Settlement), "get_OwnerClan"));
        Assert.IsTrue(Calls(near, typeof(MobileParty), "get_IsMainParty"));
        Assert.IsTrue(Calls(near, typeof(MobileParty), "get_MainParty"), "the army leader that carries the main party is flagged");
        Assert.IsTrue(Calls(near, typeof(MobileParty), "get_AttachedTo"));
    }
}
