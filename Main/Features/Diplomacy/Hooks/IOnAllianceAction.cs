namespace TAOM.Features.Diplomacy.Hooks;

public interface IOnAllianceAction
{
    bool ShouldPreventAllianceEnd(string kingdomAId, string kingdomBId);
    // causedByPlayer: the declaration is the player's own act, which lifts the phase vetoes.
    bool ShouldPreventWarDeclaration(string factionAId, string factionBId, bool causedByPlayer = false);
    // The refusal text for a war between two kingdoms, or null when it is allowed or the co-op host
    // governs. Logs nothing: the engine asks it for every AI proposal.
    string GetWarRefusalText(string kingdomAId, string kingdomBId);
    // How far to push the AI score for declaring this war down: 0 under a co-op host or when either
    // side is not a kingdom, else the War of the Ring service's penalty.
    float GetDeclareWarScorePenalty(
        string factionAId, bool factionAIsKingdom, string factionBId, bool factionBIsKingdom);
}
