using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>#834: the engine surface and the source shapes the personal spoils lean on.</summary>
[TestClass]
public class PersonalSpoilsWiringTests
{
    [TestMethod]
    public void TheMissionBehavior_QueuesHumanEnemyKillsByCharacterId_NeverTheAgent()
    {
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs", stripComments: true);
        StringAssert.Contains(src, "ConcurrentQueue<string>");
        StringAssert.Contains(src, "affectedAgent.IsHuman");
        StringAssert.Contains(src, "Character.StringId");
        StringAssert.Contains(src, "KilledCharacterIds");
    }

    [TestMethod]
    public void TheSamplerTick_TreatsAControlledSoldierAsHeroDown()
    {
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs", stripComments: true);
        var tick = src.Substring(src.IndexOf("OnMissionTick"), src.IndexOf("OnAgentRemoved") - src.IndexOf("OnMissionTick"));
        StringAssert.Contains(tick, "CharacterObject.PlayerCharacter");
    }

    [TestMethod]
    public void TheKillTest_IsTheHeroByCharacter_NotTheMainAgent()
    {
        // A soldier the player takes over after falling is never a hero (CanTakeControlOfAgent needs
        // !Character.IsHero), so his kills are not the player's.
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs", stripComments: true);
        var body = src.Substring(src.IndexOf("OnAgentRemoved"));
        StringAssert.Contains(body, "CharacterObject.PlayerCharacter");
        Assert.IsFalse(body.Contains("IsMainAgent"), "OnAgentRemoved must not key on the controlled agent");
    }

    [TestMethod]
    public void TheMaintenanceReset_ClearsTheAccumulatorThroughItsOwnReset()
    {
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/ServiceMaintenanceService.cs", stripComments: true);
        StringAssert.Contains(src, "_meritAccumulator?.ResetForNewSession()");
    }

    [TestMethod]
    public void TheLootAdapter_GuardsEmptyEquipmentAndBanners()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/BattleLootAdapter.cs", stripComments: true);
        StringAssert.Contains(src, "FirstBattleEquipment");
        StringAssert.Contains(src, "HasBannerComponent");
        StringAssert.Contains(src, "GetLootedItemFromTroop");
        StringAssert.Contains(src, "GetExpectedLootedItemValueFromCasualty");
        StringAssert.Contains(src, "AddToCounts(element, 1)");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheBattleRewardModelMembers_StillResolveWithTheirSignatures()
    {
        var expected = typeof(BattleRewardModel).GetMethod(
            "GetExpectedLootedItemValueFromCasualty", new[] { typeof(Hero), typeof(CharacterObject) });
        Assert.IsNotNull(expected, "GetExpectedLootedItemValueFromCasualty(Hero, CharacterObject) is gone");
        Assert.AreEqual(typeof(float), expected!.ReturnType);

        var looted = typeof(BattleRewardModel).GetMethod(
            "GetLootedItemFromTroop", new[] { typeof(CharacterObject), typeof(float) });
        Assert.IsNotNull(looted, "GetLootedItemFromTroop(CharacterObject, float) is gone");
        Assert.AreEqual(typeof(EquipmentElement), looted!.ReturnType);

        Assert.IsNotNull(typeof(ItemRoster).GetMethod("AddToCounts", new[] { typeof(EquipmentElement), typeof(int) }),
            "ItemRoster.AddToCounts(EquipmentElement, int) is gone");
        Assert.IsNotNull(typeof(ItemObject).GetProperty("HasBannerComponent"));
        Assert.IsNotNull(typeof(BasicCharacterObject).GetProperty("FirstBattleEquipment"));
        Assert.IsNotNull(typeof(EquipmentElement).GetMethod("GetModifiedItemName"));
    }
}
