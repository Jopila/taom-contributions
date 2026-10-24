using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.ArmyManagement;
using TaleWorlds.Localization;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.RedArrow;

/// <summary>
/// Drift guard for the Red Arrow's two mirrored engine rules (#873). The service reproduces branches 2 to 12 of
/// <c>DefaultArmyManagementCalculationModel.CheckPartyEligibility</c> and the map-action check of
/// <c>CampaignUIHelper.GetMapScreenActionIsEnabledWithReason</c>, with the siege blocks lifted. If either engine body
/// calls, tests or words anything differently, the mirror is stale: re-read the engine method, update
/// <c>RedArrowService</c>, <c>ArmyCallReason</c> and <c>RedArrowTexts</c>, then re-pin the new sequence here.
/// </summary>
[TestClass]
public class RedArrowBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static readonly string[] PartyEligibilityPin =
    {
        "ldc.i4 1", "call PlayerSiege::get_PlayerSiegeEvent()", "brfalse", "ldc.i4 0",
        "ldstr \"str_action_disabled_reason_siege\"", "ldnull", "call GameTexts::FindText(String,String)", "brtrue",
        "ldc.i4 0", "ldstr \"{=f6vTzVar}Does not have a mobile party.\"", "ldnull",
        "newobj TextObject::.ctor(String,Dictionary<String,Object>)", "callvirt MobileParty::get_LeaderHero()",
        "call Hero::get_MainHero()", "callvirt Hero::get_MapFaction()", "brtrue", "ldnull", "callvirt IFaction::get_Leader()",
        "bne.un", "ldc.i4 0", "ldstr \"{=ipLqVv1f}You cannot invite the ruler's party to your army.\"", "ldnull",
        "newobj TextObject::.ctor(String,Dictionary<String,Object>)", "callvirt MobileParty::get_Army()", "brfalse",
        "callvirt MobileParty::get_Army()", "call Hero::get_MainHero()", "callvirt Hero::get_PartyBelongedTo()", "brtrue",
        "ldnull", "call MobileParty::get_Army()", "beq", "ldc.i4 0", "ldstr \"{=aROohsat}Already in another army.\"", "ldnull",
        "newobj TextObject::.ctor(String,Dictionary<String,Object>)", "callvirt MobileParty::get_Army()", "brfalse",
        "callvirt MobileParty::get_Army()", "call Hero::get_MainHero()", "callvirt Hero::get_PartyBelongedTo()", "brtrue",
        "ldnull", "call MobileParty::get_Army()", "bne.un", "ldc.i4 0", "ldstr \"{=Vq8yavES}Already in army.\"", "ldnull",
        "newobj TextObject::.ctor(String,Dictionary<String,Object>)", "callvirt MobileParty::get_MapEvent()", "brtrue",
        "callvirt MobileParty::get_SiegeEvent()", "brtrue", "callvirt MobileParty::get_CurrentSettlement()", "brfalse",
        "callvirt MobileParty::get_CurrentSettlement()", "callvirt Settlement::get_IsUnderSiege()", "brfalse", "ldc.i4 0",
        "ldstr \"{=pkbUiKFJ}Currently fighting an enemy.\"", "ldnull",
        "newobj TextObject::.ctor(String,Dictionary<String,Object>)",
        "callvirt ArmyManagementCalculationModel::GetPartySizeScore(MobileParty)", "call Campaign::get_Current()",
        "callvirt Campaign::get_Models()", "callvirt GameModels::get_ArmyManagementCalculationModel()",
        "callvirt ArmyManagementCalculationModel::get_PlayerMobilePartySizeRatioToCallToArmy()", "bgt.un", "ldc.i4 0",
        "ldstr \"{=SVJlOYCB}Party has less men than 40% of it's party size limit.\"", "ldnull",
        "newobj TextObject::.ctor(String,Dictionary<String,Object>)", "callvirt MobileParty::get_IsDisbanding()", "brtrue",
        "call Campaign::get_Current()", "callvirt Campaign::GetCampaignBehavior<IDisbandPartyCampaignBehavior>()", "brtrue",
        "ldc.i4 0", "callvirt IDisbandPartyCampaignBehavior::IsPartyWaitingForDisband(MobileParty)", "brfalse", "ldc.i4 0",
        "ldstr \"{=tFGM0yav}This party is disbanding.\"", "ldnull",
        "newobj TextObject::.ctor(String,Dictionary<String,Object>)", "call MobileParty::get_MainParty()",
        "callvirt MobileParty::get_IsCurrentlyAtSea()", "brfalse", "ldc.i4 0",
        "callvirt MobileParty::get_HasNavalNavigationCapability()", "brfalse",
        "ldstr \"{=gFixGQsr}You cannot call a party to your army while your party is at sea.\"", "ldnull",
        "newobj TextObject::.ctor(String,Dictionary<String,Object>)",
        "ldstr \"{=nqq84Dzq}Party cannot reach your army since it has no ships.\"", "ldnull",
        "newobj TextObject::.ctor(String,Dictionary<String,Object>)", "callvirt MobileParty::get_IsInRaftState()", "brfalse",
        "ldc.i4 0", "ldstr \"{=TbXDmh3t}This party is lost at sea.\"", "ldnull",
        "newobj TextObject::.ctor(String,Dictionary<String,Object>)", "call MobileParty::get_MainParty()",
        "callvirt MobileParty::get_NavigationCapability()",
        "call DistanceHelper::FindClosestDistanceFromMobilePartyToMobileParty(MobileParty,MobileParty,NavigationType,Single&)",
        "call Campaign::get_Current()", "callvirt Campaign::get_Models()",
        "callvirt GameModels::get_ArmyManagementCalculationModel()",
        "callvirt ArmyManagementCalculationModel::get_MaximumDistanceToCallToArmy()", "ble.un", "ldc.i4 0",
        "ldstr \"{=UINgZDN5}You can not call a party that is far away.\"", "ldnull",
        "newobj TextObject::.ctor(String,Dictionary<String,Object>)", "ldnull",
    };

    private static readonly string[] MapActionPin =
    {
        "call Hero::get_MainHero()", "callvirt Hero::get_IsPrisoner()", "brfalse",
        "ldstr \"str_action_disabled_reason_prisoner\"", "ldnull", "call GameTexts::FindText(String,String)", "ldc.i4 0",
        "call MobileParty::get_MainParty()", "callvirt MobileParty::get_IsInRaftState()", "brfalse",
        "ldstr \"str_action_disabled_reason_raft_state\"", "ldnull", "call GameTexts::FindText(String,String)", "ldc.i4 0",
        "call MobileParty::get_MainParty()", "callvirt MobileParty::get_IsInFerryState()", "brfalse",
        "ldstr \"str_action_disabled_reason_ferry_state\"", "ldnull", "call GameTexts::FindText(String,String)", "ldc.i4 0",
        "call CampaignMission::get_Current()", "brfalse", "ldstr \"{=FdzsOvDq}This action is disabled while in a mission\"",
        "ldnull", "newobj TextObject::.ctor(String,Dictionary<String,Object>)", "ldc.i4 0",
        "call PlayerEncounter::get_Current()", "brfalse", "call PlayerEncounter::get_EncounterSettlement()", "brfalse",
        "call PlayerEncounter::get_EncounterSettlement()", "ldfld Settlement::Village", "brtrue", "ldc.i4 0",
        "call Village::get_VillageState()", "ldc.i4 1", "ceq", "brfalse", "call MobileParty::get_MainParty()",
        "callvirt MobileParty::get_MapEvent()", "brtrue", "ldc.i4 0", "call MapEvent::get_IsRaid()", "brfalse",
        "ldstr \"str_action_disabled_reason_raid\"", "ldnull", "call GameTexts::FindText(String,String)", "ldc.i4 0",
        "call PlayerEncounter::get_EncounterSettlement()", "callvirt Settlement::get_IsUnderSiege()", "brfalse",
        "ldstr \"str_action_disabled_reason_siege\"", "ldnull", "call GameTexts::FindText(String,String)", "ldc.i4 0",
        "ldstr \"str_action_disabled_reason_encounter\"", "ldnull", "call GameTexts::FindText(String,String)", "ldc.i4 0",
        "call PlayerSiege::get_PlayerSiegeEvent()", "brfalse", "ldstr \"str_action_disabled_reason_siege\"", "ldnull",
        "call GameTexts::FindText(String,String)", "ldc.i4 0", "call MobileParty::get_MainParty()",
        "callvirt MobileParty::get_MapEvent()", "brfalse",
        "ldstr \"{=MIylzRc5}You can't perform this action while you are in a map event.\"", "ldnull",
        "newobj TextObject::.ctor(String,Dictionary<String,Object>)", "ldc.i4 0", "call TextObject::GetEmpty()", "ldc.i4 1",
    };

    // ArmyManagementItemVM.UpdateEligibility: the postfix mirrors its skip branch and overwrites both of its outputs.
    private static readonly string[] UpdateEligibilityPin = {
        "call Campaign::get_Current()",
        "callvirt Campaign::get_Models()",
        "brtrue",
        "ldnull",
        "call GameModels::get_ArmyManagementCalculationModel()",
        "ldc.i4 1",
        "call TextObject::GetEmpty()",
        "ldfld ArmyManagementItemVM::CanJoinBackWithoutCost",
        "brtrue",
        "call ArmyManagementItemVM::get_IsInCart()",
        "brfalse",
        "call ArmyManagementItemVM::get_IsAlreadyWithPlayer()",
        "brtrue",
        "ldc.i4 0",
        "ldstr \"{=idRXFzQ6}Already added to the army.\"",
        "ldnull",
        "newobj TextObject::.ctor(String,Dictionary<String,Object>)",
        "ldfld ArmyManagementItemVM::Party",
        "ldflda ArmyManagementItemVM::_eligibilityReason",
        "callvirt ArmyManagementCalculationModel::CheckPartyEligibility(MobileParty,TextObject&)",
        "brfalse",
        "ldflda ArmyManagementItemVM::_eligibilityReason",
        "call CampaignUIHelper::GetMapScreenActionIsEnabledWithReason(TextObject&)",
        "call ArmyManagementItemVM::set_IsEligible(Boolean)"
    };

    private static void AssertPinned(System.Reflection.MethodBase? method, string name, string[] pinned)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.IsNotNull(method, name + " is gone or changed its parameters.");
        var il = method!.GetMethodBody()?.GetILAsByteArray();
        Assert.IsNotNull(il, name + " has no readable IL body.");
        var tokens = IlCallScanner.Fingerprint(method, il!).ToArray();
        Assert.AreNotEqual(0, tokens.Length, name + " fingerprinted to nothing; the scan failed, not the method.");
        Assert.IsTrue(tokens.SequenceEqual(pinned),
            name + " changed, so the Red Arrow's mirror of it is stale (see this class's summary). Actual sequence:\n{ "
            + string.Join(", ", tokens.Select(c => "\"" + c.Replace("\"", "\\\"") + "\"")) + " }");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void CheckPartyEligibility_CallsAndWordsExactlyThePinnedSequence()
    {
        AssertPinned(
            AccessTools.Method(typeof(DefaultArmyManagementCalculationModel), nameof(DefaultArmyManagementCalculationModel.CheckPartyEligibility),
                new[] { typeof(MobileParty), typeof(TextObject).MakeByRefType() }),
            "DefaultArmyManagementCalculationModel.CheckPartyEligibility", PartyEligibilityPin);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void GetMapScreenActionIsEnabledWithReason_CallsAndWordsExactlyThePinnedSequence()
    {
        AssertPinned(
            AccessTools.Method(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetMapScreenActionIsEnabledWithReason),
                new[] { typeof(TextObject).MakeByRefType() }),
            "CampaignUIHelper.GetMapScreenActionIsEnabledWithReason", MapActionPin);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void UpdateEligibility_CallsAndWordsExactlyThePinnedSequence()
    {
        AssertPinned(
            AccessTools.Method(typeof(ArmyManagementItemVM), nameof(ArmyManagementItemVM.UpdateEligibility), Type.EmptyTypes),
            "ArmyManagementItemVM.UpdateEligibility", UpdateEligibilityPin);
    }
}
