using TAOM.Features.CoopInterop;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace TAOM.Features.SpecialResources;

/// <summary>
/// The pure decisions behind special-resource earning (who won, whether an event type pays the
/// battle credit), extracted so they can be tested without
/// a running campaign (<c>MapEvent</c> is sealed and unconstructible in a unit test). The behavior
/// keeps the plumbing; this keeps the policy — same split as <c>PatchShieldPolicy</c>.
/// </summary>
public static class SpecialResourceEarnPolicy
{
    /// <summary>
    /// Did the player's side win?
    ///
    /// This replaced a gate that asked whether the player IS the winning side's
    /// <c>LeaderParty.LeaderHero</c>. That framing conflated participating with commanding, and the
    /// consequence was not multiplayer-only: in ordinary single-player, a player who joins any
    /// lord's army stops being the leader party's hero, so every victory they fought in paid
    /// nothing. Multiplayer only made it total — under a client/server split no player leads the
    /// authoritative side either, so nobody ever earned (field report 2026-08-03 §6).
    /// </summary>
    /// <param name="playerSide">
    /// <c>MapEvent.PlayerSide</c> — the side the player's own party is on, whoever commands it.
    /// </param>
    /// <param name="winningSide">
    /// <c>MapEvent.WinningSide</c>, which is <see cref="BattleSideEnum.None"/> unless the battle
    /// actually resolved to a victory. Under co-op, clients routinely observe an unresolved state
    /// because the server is authoritative — that must never read as a win.
    /// </param>
    public static bool IsPlayerVictory(BattleSideEnum playerSide, BattleSideEnum winningSide)
    {
        if (winningSide == BattleSideEnum.None) return false;
        if (playerSide == BattleSideEnum.None) return false;
        return playerSide == winningSide;
    }

    /// <summary>
    /// Does a won map event pay the generic battle credit? A raid and forced supplies or volunteers
    /// (#770) are village-hostile events: the engine ends them as an attacker victory, and a raid pays
    /// <c>per_raid</c> through <c>RaidCompleted</c>, so the militia fight and the loot pay nothing here.
    /// The engine also fights real battles against lord or other field parties inside those events
    /// (a defending player when an AI lord raids their village, a lord party inside a village the
    /// player raids or extorts, an army raid), and a won real fight pays. Every other event type
    /// always pays, hideout clears included.
    /// </summary>
    /// <param name="type">The map event's type.</param>
    /// <param name="beatAFieldParty">
    /// True when the enemy side held a party that is neither militia nor villagers and started with
    /// healthy troops.
    /// </param>
    public static bool PaysBattleCredit(MapEvent.BattleTypes type, bool beatAFieldParty) =>
        beatAFieldParty
        || (type != MapEvent.BattleTypes.Raid
            && type != MapEvent.BattleTypes.IsForcingSupplies
            && type != MapEvent.BattleTypes.IsForcingVolunteers);

    /// <summary>
    /// Who may earn a special resource on this process (#967 C1). A client never writes, because the
    /// authority holds the only balance. Only a hero the player-control query calls a player earns: with
    /// no session that is the local MainHero, in a session a registered co-op player, and the authority's
    /// own MainHero is nobody's. With no session a dedicated server earns nothing (its MainHero is the
    /// idle world-gen hero); in a session its registered guests are real players.
    /// </summary>
    /// <param name="sessionActive">A co-op session is live.</param>
    /// <param name="isAuthority">This peer runs the authoritative campaign (true in single-player).</param>
    /// <param name="isDedicated">This process is a dedicated server.</param>
    /// <param name="control">What the player-control query says about the candidate hero.</param>
    public static bool MayEarn(bool sessionActive, bool isAuthority, bool isDedicated, PlayerControl control) =>
        isAuthority && control == PlayerControl.Player && (sessionActive || !isDedicated);
}
