using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TAOM.Adapters;
using TAOM.Features.CreatureBandits;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>
/// #870: the raid-host adapter is engine-bound, so its IL is the testable surface. These pin the facts the plan's engine
/// checks (v1.5.5) depend on: a bandit party always has a real hideout (a null home crashes
/// <c>BanditSpawnCampaignBehavior</c>'s per-hideout count on create, destroy and every load), a raid is ordered through
/// <c>SetPartyAiAction</c> with a refresh (a pinned party never thinks), a pin is always paired with its release and a
/// rethink, and a despawn goes through <c>DestroyPartyAction</c> behind its safety checks.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class RaidHostAdapterIlTests
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static MethodBase[] CallsOf(string methodName)
    {
        var method = typeof(RaidHostAdapter).GetMethod(methodName, All)!;
        return IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToArray();
    }

    private static bool Calls(MethodBase[] calls, System.Type type, string name) =>
        calls.Any(m => m.DeclaringType == type && m.Name == name);

    [TestMethod]
    public void SpawnHost_CreatesABanditPartyWithAHideout_NeverALooterParty()
    {
        var calls = CallsOf(nameof(RaidHostAdapter.SpawnHost));

        Assert.IsTrue(Calls(calls, typeof(BanditPartyComponent), nameof(BanditPartyComponent.CreateBanditParty)));
        Assert.IsFalse(Calls(calls, typeof(BanditPartyComponent), nameof(BanditPartyComponent.CreateLooterParty)),
            "a looter party takes a related settlement, not a hideout, and a template the host does not use");
    }

    [TestMethod]
    public void NoMethodOfTheAdapter_SetsACustomHomeSettlement()
    {
        var offenders = typeof(RaidHostAdapter).GetMethods(All)
            .Where(m => m.GetMethodBody() != null)
            .Where(m => IlCallScanner.ExtractCalledMethods(m, m.GetMethodBody()!.GetILAsByteArray())
                .Any(c => c.Name == "SetCustomHomeSettlement" || c.Name == "SetHomeHideout"))
            .Select(m => m.Name)
            .ToList();

        Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
    }

    [TestMethod]
    public void SpawnHost_BothTheProbeAndTheSpawn_ChooseTheHideoutByTheSameHelper()
    {
        Assert.IsTrue(Calls(CallsOf(nameof(RaidHostAdapter.Probe)), typeof(RaidHostAdapter), "NearestHideout"));
        Assert.IsTrue(Calls(CallsOf(nameof(RaidHostAdapter.SpawnHost)), typeof(RaidHostAdapter), "NearestHideout"));
    }

    [TestMethod]
    public void SpawnHost_NamesThePartyThroughItsCustomName()
    {
        Assert.IsTrue(CallsOf(nameof(RaidHostAdapter.SpawnHost)).Any(m => m.Name == "SetCustomName"));
    }

    [TestMethod]
    public void OrderRaid_UsesTheAiActionAndRefreshesTheParty()
    {
        var calls = CallsOf(nameof(RaidHostAdapter.OrderRaid));

        Assert.IsTrue(Calls(calls, typeof(SetPartyAiAction), nameof(SetPartyAiAction.GetActionForRaidingSettlement)));
        Assert.IsTrue(Calls(calls, typeof(MobilePartyAi), nameof(MobilePartyAi.CheckPartyNeedsUpdate)));
    }

    [TestMethod]
    public void OrderEngageAndGoHome_UseTheAiActionAndRefreshTheParty()
    {
        var engage = CallsOf(nameof(RaidHostAdapter.OrderEngage));
        var home = CallsOf(nameof(RaidHostAdapter.OrderGoHome));

        Assert.IsTrue(Calls(engage, typeof(SetPartyAiAction), nameof(SetPartyAiAction.GetActionForEngagingParty)));
        Assert.IsTrue(Calls(engage, typeof(MobilePartyAi), nameof(MobilePartyAi.CheckPartyNeedsUpdate)));
        Assert.IsTrue(Calls(home, typeof(SetPartyAiAction), nameof(SetPartyAiAction.GetActionForVisitingSettlement)));
        Assert.IsTrue(Calls(home, typeof(MobilePartyAi), nameof(MobilePartyAi.CheckPartyNeedsUpdate)));
    }

    [TestMethod]
    public void CompleteSpawn_MarksTheVisualDirty_StartsTheTradePurse_AndPinsTheAi()
    {
        var calls = CallsOf(nameof(RaidHostAdapter.CompleteSpawn));

        Assert.IsTrue(calls.Any(m => m.Name == "SetVisualAsDirty"));
        Assert.IsTrue(Calls(calls, typeof(MobileParty), nameof(MobileParty.InitializePartyTrade)));
        Assert.IsTrue(Calls(calls, typeof(MobilePartyAi), nameof(MobilePartyAi.SetDoNotMakeNewDecisions)));
    }

    [TestMethod]
    public void ReleasePin_UnpinsAndAsksForARethink()
    {
        var calls = CallsOf(nameof(RaidHostAdapter.ReleasePin));

        Assert.IsTrue(Calls(calls, typeof(MobilePartyAi), nameof(MobilePartyAi.SetDoNotMakeNewDecisions)));
        Assert.IsTrue(Calls(calls, typeof(MobilePartyAi), "set_" + nameof(MobilePartyAi.RethinkAtNextHourlyTick)));
    }

    [TestMethod]
    public void Despawn_ChecksTheSafetyConditions_ThenDestroysThroughTheEnginesAction()
    {
        var calls = CallsOf(nameof(RaidHostAdapter.Despawn));

        Assert.IsTrue(Calls(calls, typeof(MobileParty), "get_" + nameof(MobileParty.MapEvent)), "never a party in a battle");
        Assert.IsTrue(Calls(calls, typeof(MobileParty), "get_" + nameof(MobileParty.IsMainParty)), "never the player's party");
        Assert.IsTrue(Calls(calls, typeof(MobileParty), "get_" + nameof(MobileParty.IsActive)));
        Assert.IsTrue(Calls(calls, typeof(DestroyPartyAction), nameof(DestroyPartyAction.Apply)));
    }

    [TestMethod]
    public void PartiesNear_SearchesTheLocatorGrid_NotEveryParty()
    {
        var calls = CallsOf(nameof(RaidHostAdapter.PartiesNear));

        Assert.IsTrue(Calls(calls, typeof(MobileParty), nameof(MobileParty.StartFindingLocatablesAroundPosition)));
        Assert.IsFalse(Calls(calls, typeof(MobileParty), "get_" + nameof(MobileParty.All)), "no walk over every party");
        Assert.IsFalse(Calls(calls, typeof(MobileParty), "get_" + nameof(MobileParty.AllLordParties)), "no walk over every lord party");
    }

    [TestMethod]
    public void PartiesNear_ReadsAnArmyLeadersWholeArmy_NotJustItsOwnRoster()
    {
        // Vanilla's own engage test (DefaultMobilePartyAIModel, the strength compare near line 208) weighs a leader by its
        // army: a battle with the leader pulls the whole army in.
        var calls = CallsOf(nameof(RaidHostAdapter.PartiesNear));

        Assert.IsTrue(Calls(calls, typeof(Army), "get_" + nameof(Army.EstimatedStrength)), "an army leader is weighed by its army");
        Assert.IsTrue(Calls(calls, typeof(Army), "get_" + nameof(Army.LeaderParty)), "the leader comparison is read (the compare itself is not pinned)");
        Assert.IsTrue(Calls(calls, typeof(PartyBase), "get_" + nameof(PartyBase.EstimatedStrength)), "any other party by its own roster");
    }

    [TestMethod]
    public void PartiesNear_SkipsPartiesTheEngineIgnores_AndEveryPartyInASiegeCamp()
    {
        // PartyBase.IInteractablePoint.CanPartyInteract refuses a ShouldBeIgnored party, and vanilla weighs a besieger by
        // its whole camp (DefaultMobilePartyAIModel, near line 208), so no party with a BesiegerCamp is a candidate.
        var calls = CallsOf(nameof(RaidHostAdapter.PartiesNear));

        Assert.IsTrue(Calls(calls, typeof(MobileParty), "get_" + nameof(MobileParty.ShouldBeIgnored)));
        Assert.IsTrue(Calls(calls, typeof(MobileParty), "get_" + nameof(MobileParty.BesiegerCamp)));
        Assert.IsFalse(Calls(calls, typeof(BesiegerCamp), "get_" + nameof(BesiegerCamp.LeaderParty)), "no leader exception");
    }

    [TestMethod]
    public void TheAdapter_HoldsNoCampaignObject_NoCampaign_NoVillage_NoTypeThatHoldsThem()
    {
        // A singleton that keeps a Campaign, or a Village (which reaches its settlement and the campaign), roots the
        // finished campaign's whole object graph after a load. Villages are iterated live on every call instead.
        var offenders = typeof(RaidHostAdapter).GetFields(All)
            .Where(f => Holds(f.FieldType, new HashSet<System.Type>()))
            .Select(f => f.Name + ": " + f.FieldType.Name)
            .ToList();

        Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
    }

    private static bool Holds(System.Type type, HashSet<System.Type> seen)
    {
        if (type == typeof(Campaign) || type == typeof(Village))
            return true;
        if (!seen.Add(type))
            return false;
        if (type.HasElementType && Holds(type.GetElementType()!, seen))
            return true;
        if (type.IsGenericType && type.GetGenericArguments().Any(a => Holds(a, seen)))
            return true;
        if (type.Namespace != null && type.Namespace.StartsWith("System", System.StringComparison.Ordinal))
            return false;
        return type.GetFields(All).Any(f => Holds(f.FieldType, seen));
    }

    [TestMethod]
    public void ReadVillages_WalksTheLiveVillageList_AndTheAdapterKeepsNoRaidableRuleOfItsOwn()
    {
        var calls = CallsOf(nameof(RaidHostAdapter.ReadVillages));

        Assert.IsTrue(Calls(calls, typeof(Village), "get_" + nameof(Village.All)), "iterated live on every call");
        var reading = CallsOf("ReadingOf");
        Assert.IsTrue(Calls(calls, typeof(RaidHostAdapter), "ReadingOf"));
        Assert.IsTrue(Calls(reading, typeof(Village), "get_" + nameof(Village.Hearth)), "the hearth is read live");
        Assert.IsTrue(Calls(reading, typeof(Village), "get_" + nameof(Village.VillageState)), "so is the state");
        Assert.IsTrue(Calls(CallsOf(nameof(RaidHostAdapter.ReadVillage)), typeof(RaidHostAdapter), "ReadingOf"), "one reading for both entry points");
        var rules = typeof(RaidHostAdapter).GetMembers(All).Where(m => m.Name.Contains("Raidable")).Select(m => m.Name).ToList();
        Assert.AreEqual(0, rules.Count, "which villages are raidable and nearest is FireAndTributeMath's, not the adapter's: " + string.Join(", ", rules));
    }

    [TestMethod]
    public void TheAdapter_KeepsNoInstanceState_EveryCallResolvesItsIdsAgainstTheLiveCampaign()
    {
        var instanceFields = typeof(RaidHostAdapter).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        Assert.AreEqual(0, instanceFields.Length, string.Join(", ", instanceFields.Select(f => f.Name)));
    }

    [TestMethod]
    public void SpawnHost_UsesTheCreatureBandsOutOfSightRule_NotACopyOfIt()
    {
        var calls = CallsOf(nameof(RaidHostAdapter.SpawnHost));

        Assert.IsTrue(Calls(calls, typeof(CreatureBandParties), "OutOfPlayerSight"));
        var copies = typeof(RaidHostAdapter).GetMembers(All).Where(m => m.Name.Contains("OutOfPlayerSight") || m.Name.Contains("OutOfSightRetries")).ToList();
        Assert.AreEqual(0, copies.Count, "one rule, one place");
    }

    [TestMethod]
    public void SpawnHost_FindsTheNearestTownThroughTheEnginesHelper()
    {
        var calls = CallsOf(nameof(RaidHostAdapter.SpawnHost));

        Assert.IsTrue(Calls(calls, typeof(SettlementHelper), nameof(SettlementHelper.FindNearestSettlementToPoint)));
        Assert.IsFalse(typeof(RaidHostAdapter).GetMembers(All).Any(m => m.Name == "NearestTownId"), "no hand-written scan of Settlement.All");
    }
}
