using TAOM.Features.CoopInterop;

namespace TAOM.Features.RedArrow;

/// <summary>
/// The Red Arrow (#873, docs/features/red-arrow.md): while the player leads an army that is besieging, vanilla's Army
/// Management screen refuses every call and every transfer. This service decides when that block is lifted and answers the
/// three questions the screen asks, from snapshots of primitives (ADR-007): is a party eligible
/// (<see cref="CheckParty"/>), is the map action allowed (<see cref="CheckMapAction"/>) and is a row's transfer disabled
/// (<see cref="TransferDisabled"/>). Every answer is null for "use vanilla's", which is the case whenever
/// <see cref="Applies"/> is false.
/// <para>
/// <see cref="CheckParty"/> reproduces branches 2 to 12 of <c>DefaultArmyManagementCalculationModel.CheckPartyEligibility</c>
/// (v1.5.5 :405-479) in vanilla's order, so the first failing check names the reason exactly as vanilla would; only branch 1,
/// the player's siege, is gone. Two comparisons are positive requirements instead of vanilla's negated ones, so a NaN size
/// score or distance refuses the party where vanilla's <c>score &lt;= ratio</c> and <c>distance &gt; limit</c> would both be
/// false and let it in. <see cref="CheckMapAction"/> is <c>CampaignUIHelper.GetMapScreenActionIsEnabledWithReason</c>
/// (:2440-2497) with both siege blocks lifted for the player's own besieged settlement and nothing else changed.
/// </para>
/// </summary>
public sealed class RedArrowService
{
    private readonly IRedArrowSettingsProvider _settings;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicatedServer;

    public RedArrowService(IRedArrowSettingsProvider settings, ICoopSessionProvider coop, IDedicatedServerProvider dedicatedServer)
    {
        _settings = settings;
        _coop = coop;
        _dedicatedServer = dedicatedServer;
    }

    /// <summary>
    /// The switch is on, this is singleplayer (any co-op session, a client deferring to a host and a dedicated server keep
    /// vanilla), and the player leads an army from the besieger side of a siege. A defender has a siege event but no besieger
    /// camp, so it keeps vanilla's block, and so does a player with no army: no army is created mid-siege.
    /// </summary>
    public bool Applies(PlayerSiegeState player) =>
        player.IsBesieger
        && player.LeadsArmy
        && _settings.Enabled
        && !_coop.IsSessionActive
        && !_coop.ShouldDeferToHost
        && !_dedicatedServer.IsDedicatedServer;

    /// <summary>Null when vanilla decides. <see cref="ArmyCallReason.None"/> means the party can be called.</summary>
    public ArmyCallReason? CheckParty(PlayerSiegeState player, ArmyCallCandidate candidate)
    {
        if (!Applies(player))
            return null;

        if (!candidate.HasParty)
            return ArmyCallReason.NoParty;
        if (candidate.IsFactionLeaderParty)
            return ArmyCallReason.RulersParty;
        if (candidate.Army == CandidateArmy.OtherArmy)
            return ArmyCallReason.InAnotherArmy;
        if (candidate.Army == CandidateArmy.PlayersArmy)
            return ArmyCallReason.InThisArmy;
        if (candidate.IsFightingOrBesieged)
            return ArmyCallReason.Fighting;
        if (!(candidate.SizeScore > candidate.MinSizeScore))
            return ArmyCallReason.TooSmall;
        if (candidate.IsDisbanding)
            return ArmyCallReason.Disbanding;

        if (candidate.MainPartyAtSea)
            return candidate.HasNavalCapability ? ArmyCallReason.AtSea : ArmyCallReason.NoShips;
        if (candidate.IsInRaftState)
            return ArmyCallReason.LostAtSea;
        if (!(candidate.Distance <= candidate.MaxDistance))
            return ArmyCallReason.TooFar;
        return ArmyCallReason.None;
    }

    /// <summary>Null when vanilla decides. <see cref="MapActionReason.None"/> means the action is allowed.</summary>
    public MapActionReason? CheckMapAction(PlayerSiegeState player, MapActionState map)
    {
        if (!Applies(player))
            return null;

        if (map.IsPrisoner)
            return MapActionReason.Prisoner;
        if (map.IsInRaftState)
            return MapActionReason.RaftState;
        if (map.IsInFerryState)
            return MapActionReason.FerryState;
        if (map.InMission)
            return MapActionReason.InMission;
        if (map.HasEncounter)
        {
            if (!map.EncounterHasSettlement)
                return MapActionReason.Encounter;
            if (map.RaidAtEncounter)
                return MapActionReason.Raid;
            // Vanilla's first siege block. The player's own besieged settlement is the one siege the screen is for.
            if (map.EncounterSettlementUnderSiege && !map.EncounterSettlementIsOwnSiege)
                return MapActionReason.Siege;
        }

        // Vanilla's second siege block (PlayerSiege.PlayerSiegeEvent != null) is the player's own siege here: Applies needs a
        // besieger camp, so there is nothing left to refuse.
        return map.HasMapEvent ? MapActionReason.MapEvent : MapActionReason.None;
    }

    /// <summary>
    /// Null when vanilla decides (it disables every row during a siege). Otherwise only the player's own row stays
    /// disabled, so a party can be added to the army or released from it.
    /// </summary>
    public bool? TransferDisabled(PlayerSiegeState player, bool isMainHero) => Applies(player) ? isMainHero : (bool?)null;
}
