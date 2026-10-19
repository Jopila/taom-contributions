using System.Collections.Generic;
using TAOM.Features.CultureDoctrine.Doctrines;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Tactics;

/// <summary>
/// The engine side of <see cref="StandGround"/>: the scene's choke points and forests as an
/// immutable <see cref="GroundCandidate"/> array. The lists it reads are public fields of the
/// team AI, built in its constructor from every tactical entity of the mission, region children
/// included (`TeamAIComponent.cs:96-97`; each is a mission object registered in `OnPreInit`,
/// `MissionObject.cs:121-143`), and read by vanilla on the same tick (`TeamAIComponent.cs:309`,
/// `TacticHoldChokePoint.cs:187-191`). The anchor copies them once, lazily, on its first stand
/// decision (the team-AI tick), and scoring then reads only the copy: no engine list is touched
/// again and nothing allocates per decision. The installer counts the kinds in the same copy for
/// the registration line, so the log and the picker see one classification.
/// </summary>
internal static class StandGroundScan
{
    /// <summary>One copy of every choke point and every forest region the team AI lists.</summary>
    public static GroundCandidate[] Copy(Team team)
    {
        var ai = team.TeamAI;
        var positions = ai?.TacticalPositions;
        var regions = ai?.TacticalRegions;
        var found = new List<GroundCandidate>();
        if (positions != null)
        {
            for (var i = 0; i < positions.Count; i++)
            {
                var tp = positions[i];
                if (tp != null && tp.TacticalPositionType == TacticalPosition.TacticalPositionTypeEnum.ChokePoint)
                    found.Add(GroundCandidate.Choke(tp.Position.AsVec2, tp.Direction, tp.Width, tp.IsInsurmountable));
            }
        }
        if (regions != null)
        {
            for (var i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                if (region != null && region.tacticalRegionType == TacticalRegion.TacticalRegionTypeEnum.Forest)
                    found.Add(GroundCandidate.Forest(region.Position.AsVec2));
            }
        }
        return found.ToArray();
    }
}
