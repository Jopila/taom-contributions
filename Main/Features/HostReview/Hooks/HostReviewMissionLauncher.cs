using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.MissionSpawnHandlers;
using TaleWorlds.MountAndBlade.Missions.Handlers;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;
using TAOM.Core.Logging;
using TAOM.Features.HostReview.Domain;

namespace TAOM.Features.HostReview.Hooks;

/// <summary>
/// Opens the Host Review mission: the map patch scene under the main party, the lineup on the defender
/// side, an empty attacker side, and the field-battle behaviors minus everything that ends a battle,
/// scores it, reinforces it or reads a map event. It also drops MountAgentLogic, which writes the party's item
/// roster, and the banner bearer, surgeon and highlight logics, which serve only a real battle. Both sides use
/// custom-battle combatants because a
/// party has no battle side without a map event.
/// </summary>
internal sealed class HostReviewMissionLauncher : IHostReviewMissionLauncher
{
    private readonly IModLogger _logger;

    public HostReviewMissionLauncher(IModLogger logger)
    {
        _logger = logger;
    }

    public bool TryOpen(IReadOnlyList<HostReviewSlot> lineup)
    {
        try
        {
            var characters = ExpandLineup(lineup);
            if (characters.Count == 0) return false;

            var party = MobileParty.MainParty;
            var patch = Campaign.Current.MapSceneWrapper.GetMapPatchAtPosition(party.Position);
            var scene = Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(patch, false);
            var record = BuildRecord(scene, patch, party);

            var player = BuildCombatant("{=!}Host Review", BattleSideEnum.Defender, characters);
            var enemy = BuildCombatant("{=!}Host Review (empty)", BattleSideEnum.Attacker, new List<CharacterObject>());

            var suppliers = new IMissionTroopSupplier[2];
            suppliers[(int)BattleSideEnum.Defender] = new HostReviewTroopSupplier(characters, MakeOrigin);
            suppliers[(int)BattleSideEnum.Attacker] = new HostReviewTroopSupplier(new List<CharacterObject>(), MakeOrigin);

            MissionState.OpenNew(HostReviewMissionViews.MissionName, record,
                _ => Behaviors(suppliers, player, enemy));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[HostReview] Opening the review failed: {ex}");
            return false;
        }
    }

    private static List<CharacterObject> ExpandLineup(IReadOnlyList<HostReviewSlot> lineup)
    {
        var characters = new List<CharacterObject>();
        var roster = MobileParty.MainParty.MemberRoster;
        foreach (var slot in lineup)
        {
            var character = roster.GetCharacterAtIndex(slot.RosterIndex);
            if (character == null) continue;
            for (var i = 0; i < slot.Count; i++)
                characters.Add(character);
        }

        return characters;
    }

    private static MissionInitializerRecord BuildRecord(string scene, MapPatchData patch, MobileParty party)
    {
        var record = new MissionInitializerRecord(scene)
        {
            TerrainType = (int)Campaign.Current.MapSceneWrapper.GetFaceTerrainType(party.CurrentNavigationFace),
            DamageToFriendsMultiplier = 0f,
            DamageFromPlayerToFriendsMultiplier = 0f,
            NeedsRandomTerrain = false,
            PlayingInCampaignMode = true,
            RandomTerrainSeed = MBRandom.RandomInt(10000),
            AtmosphereOnCampaign = Campaign.Current.Models.MapWeatherModel.GetAtmosphereModel(party.Position),
            SceneHasMapPatch = true,
            DecalAtlasGroup = 2,
            PatchCoordinates = patch.normalizedCoordinates,
            PatchEncounterDir = new Vec2(1f, 0f),
        };
        return record;
    }

    private static CustomBattleCombatant BuildCombatant(string name, BattleSideEnum side, List<CharacterObject> characters)
    {
        var hero = Hero.MainHero;
        var combatant = new CustomBattleCombatant(new TextObject(name), hero.Culture, hero.ClanBanner) { Side = side };
        foreach (var character in characters)
            combatant.AddCharacter(character, 1);
        if (characters.Count > 0)
            combatant.SetGeneral(characters[0]);
        return combatant;
    }

    // The invincible origin writes no wound, death or hit points back. No combat XP is awarded because
    // BattleAgentLogic is not in the behavior list (it would also read MapEvent.PlayerMapEvent).
    private static IAgentOriginBase MakeOrigin(CharacterObject character, int rank) =>
        new PartyAgentOrigin(PartyBase.MainParty, character, rank, default, alwaysWounded: false, isInvincible: true);

    private IEnumerable<MissionBehavior> Behaviors(
        IMissionTroopSupplier[] suppliers, CustomBattleCombatant player, CustomBattleCombatant enemy)
    {
        var playerName = Hero.MainHero.Name;
        return new MissionBehavior[]
        {
            new DefaultBattleMissionAgentSpawnLogic(suppliers, BattleSideEnum.Defender, Mission.BattleSizeType.Battle),
            new BattlePowerCalculationLogic(),
            new BattleSpawnLogic("battle_set"),
            new CustomBattleMissionSpawnHandler(player, enemy),
            new MissionOptionsComponent(),
            new MissionCombatantsLogic(null, player, player, enemy, Mission.MissionTeamAITypeEnum.FieldBattle, false),
            new AgentHumanAILogic(),
            new MissionAgentPanicHandler(),
            new BattleMissionAgentInteractionLogic(),
            new AgentMoraleInteractionLogic(),
            new AssignPlayerRoleInTeamMissionController(true, false, false, new List<string>()),
            new GeneralsAndCaptainsAssignmentLogic(null, playerName),
            new EquipmentControllerLeaveLogic(),
            new MissionHardBorderPlacer(),
            new MissionBoundaryPlacer(),
            new MissionBoundaryCrossingHandler(10f),
            new BattleDeploymentMissionController(false),
            new BattleDeploymentHandler(false),
            new HostReviewMissionLogic(_logger),
        };
    }
}
