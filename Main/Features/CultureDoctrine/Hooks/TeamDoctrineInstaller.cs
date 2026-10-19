using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.CultureDoctrine.Doctrines;
using TAOM.Features.CultureDoctrine.Domain;
using TAOM.Features.CultureDoctrine.Hooks.Tactics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks;

/// <summary>
/// One team's doctrine, from its combatants to its registered tactic list: resolve the side's
/// culture and Tactics skill, build the roster, construct every tactic, then swap the team's
/// list in one go. Called once per team from <c>CultureDoctrineMissionLogic.EarlyStart</c> on
/// the main thread, before the first <c>Team.Tick</c> reads the list.
/// </summary>
public static class TeamDoctrineInstaller
{
    /// <summary>Installs the roster and returns the TAOM tactics it registered (for the status
    /// line) plus the registration log line. <paramref name="horseStandEnabled"/> is the MCM switch,
    /// read once per team here at install for the tactics (the favour reads it again on every stat
    /// update); the stand is armed only for a defending AI team whose culture opted in
    /// (<see cref="HorseStand.PolicyFor"/>), so the player's own team never stands.</summary>
    public static string Install(Mission mission, Team team, DoctrineCatalog catalog, MissionCombatantsLogic combatants, List<TaomTacticBase> taomTactics, bool horseStandEnabled)
    {
        var selection = TeamCombatantSelector.Select(mission, combatants, team);
        var profile = SideProfile.From(selection.Combatants, selection.SideTacticsSkill);
        var doctrine = catalog.Resolve(profile.CultureId);
        var side = team.Side == BattleSideEnum.Attacker ? DoctrineSide.Attacker : DoctrineSide.Defender;
        var roster = TacticRoster.Build(doctrine, profile.TacticsSkill, side, CaravanTacticsRule.Ensured(mission, team.Side));
        RoutedFormationGuard.Protect(team, doctrine.Formations);
        var stand = HorseStand.PolicyFor(horseStandEnabled, doctrine.HorseStand, team.Side == BattleSideEnum.Defender, team.IsPlayerTeam);

        // Build the whole list before touching the team, so a construction failure leaves
        // vanilla's list intact rather than an emptied one.
        var tactics = TacticFactory.CreateAll(roster, team);
        team.ClearTacticOptions();
        for (var i = 0; i < tactics.Count; i++)
        {
            team.AddTacticOption(tactics[i]);
            if (tactics[i] is TaomTacticBase taom)
            {
                taom.Engagement = catalog.Engagement;
                taom.StandPolicy = stand;
                taom.StandTunables = catalog.HorseStand;
                taomTactics.Add(taom);
            }
        }
        team.ResetTactic();

        var registered = string.Join(", ", roster.Select(r => r.Tactic + "*" + r.Multiplier.ToString("0.00", CultureInfo.InvariantCulture)));
        var standLine = "off";
        if (stand != null)
        {
            var chokes = 0;
            var forests = 0;
            foreach (var candidate in StandGroundScan.Copy(team))
            {
                if (candidate.Kind == GroundKind.Choke)
                    chokes++;
                else
                    forests++;
            }
            standLine = $"on chokes={chokes} forests={forests} dismount={(stand.Dismount ? "yes" : "no")}";
        }
        return $"[Doctrine] team={team.TeamIndex} side={team.Side} player={(team.IsPlayerTeam ? "yes" : "no")} culture={profile.CultureId ?? "none"} doctrine={doctrine.CultureId} troops={profile.TroopCount} tacticsSkill={profile.TacticsSkill} morale={(doctrine.Morale.NeverRout ? "never-rout" : "routs")}{(doctrine.Morale.Bravery == 0f ? "" : "/bravery " + doctrine.Morale.Bravery.ToString("+0;-0", CultureInfo.InvariantCulture))} horseStand={standLine} registered=[{registered}]";
    }
}
