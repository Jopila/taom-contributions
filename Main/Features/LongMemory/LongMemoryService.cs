using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.LongMemory.Models;
using TAOM.Features.Relations;

namespace TAOM.Features.LongMemory;

/// <summary>
/// Long Memory (docs/features/long-memory.md, #865): the extra relation the player loses with a village owner's clan leader and
/// the village's notables for raiding the village or forcing it to give goods or recruits, on top of whatever vanilla already
/// takes. Pure over the raw facts a caller's reader hands it; every rule is decided here, so the entry points (a campaign behavior
/// with four listeners) stay thin. Each entry point takes the reader, not the facts, and calls it only after every gate, so a
/// switch that is off, a dedicated server or an option that is not one of the two pinned ids never touches the engine.
/// <para>
/// One pairing per act: the side of the village owner's realm against the player's side
/// (<see cref="IAlignmentService.ResolveSide"/> through <see cref="SideMultipliers"/>, the same pair rule Good Deeds uses), which
/// scales the owner's clan leader and every notable alike. A raid is also scaled by the damage it did
/// (<see cref="VillageHostileActionFacts.RaidDamage"/>). Each loss is rounded half away from zero and dropped when it scales to 0.
/// Losses go through <see cref="RelationGrantServiceExtensions.GrantStrongestPerOwner"/> (the strongest once per relation owner,
/// counted by what landed); one line is shown only when something landed.
/// </para>
/// A village the player's clan owns, and an act the player's own party does not lead, cost nothing. The service keeps no state, so
/// it needs no per-campaign reset. A co-op peer that defers to the host, and a dedicated server, lose nothing.
/// </summary>
public sealed class LongMemoryService
{
    private const string RaidReason = "long memory: raid";
    private const string CoercionReason = "long memory: forced taking";

    private readonly ILongMemorySettingsProvider _settings;
    private readonly ILongMemoryNoticeAdapter _notice;
    private readonly IAlignmentService _alignment;
    private readonly IRelationGrantService _grants;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _server;

    public LongMemoryService(ILongMemorySettingsProvider settings, ILongMemoryNoticeAdapter notice,
        IAlignmentService alignment, IRelationGrantService grants, ICoopSessionProvider coop, IDedicatedServerProvider server)
    {
        _settings = settings;
        _notice = notice;
        _alignment = alignment;
        _grants = grants;
        _coop = coop;
        _server = server;
    }

    /// <summary>
    /// A raid map event finished. It counts when the player led it on a village the player's clan does not own, and the event
    /// did damage. The winner is not the rule, because the battle against a militia is itself a raid map event the attackers can
    /// win with no damage done, and an event that took no hit points pays 0. The owner's clan leader and every living notable
    /// lose the raid amounts (50 each by default), scaled by the damage (the hit points this event took on the village's 0 to 1
    /// health scale, no floor: the events of one raid sum to at most about one hit (0.05) more than 1) and by the owner's side. <paramref name="read"/> reads the
    /// village and runs once, after the switches and the authority gate. Returns the number of relation owners whose relation
    /// changed.
    /// </summary>
    public int OnRaidCompleted(Func<VillageHostileActionFacts?> read)
    {
        var settings = Allowed(raid: true);
        if (settings == null)
            return 0;
        var facts = read();
        if (facts == null || !Eligible(facts))
            return 0;

        var multiplier = MultiplierOf(settings.Multipliers, facts);
        var landed = Charge(facts, settings.RaidOwnerAmount, settings.RaidNotableAmount, multiplier, facts.RaidDamage, RaidReason);
        if (landed > 0)
            _notice.ShowRaid(facts.VillageName);
        return landed;
    }

    /// <summary>
    /// A force-supplies or force-volunteers map event finished (the villagers resisted and a fight followed). Only an attacker
    /// win counts: the owner's clan leader and every living notable lose the coercion amounts (20 and 25 by default), scaled by the
    /// owner's side. <paramref name="read"/> runs once, after the attacker-win test, the switches and the authority gate. Returns
    /// the number of relation owners whose relation changed.
    /// </summary>
    public int OnCoercionCompleted(bool attackerWon, Func<VillageHostileActionFacts?> read)
    {
        if (!attackerWon)
            return 0;
        var settings = Allowed(raid: false);
        return settings == null ? 0 : Coerce(settings, read());
    }

    /// <summary>
    /// A game menu option was selected. Only the "Continue" of the two no-resist menus counts
    /// (<see cref="LongMemoryRules.IsNoResistCoercionOption"/>): the villagers gave way and the engine fires no map event, so the
    /// option is the only signal. <paramref name="read"/> reads the village the player is at and runs once, after the id test, the
    /// switches and the authority gate, so any other option never touches the engine. Returns the number of relation owners
    /// whose relation changed.
    /// </summary>
    public int OnMenuOptionSelected(string? optionId, Func<VillageHostileActionFacts?> read)
    {
        if (!LongMemoryRules.IsNoResistCoercionOption(optionId))
            return 0;
        var settings = Allowed(raid: false);
        return settings == null ? 0 : Coerce(settings, read());
    }

    private int Coerce(LongMemorySettings settings, VillageHostileActionFacts? facts)
    {
        if (facts == null || !Eligible(facts))
            return 0;

        var multiplier = MultiplierOf(settings.Multipliers, facts);
        var landed = Charge(facts, settings.CoercionOwnerAmount, settings.CoercionNotableAmount, multiplier, 1f, CoercionReason);
        if (landed > 0)
            _notice.ShowCoercion(facts.VillageName);
        return landed;
    }

    // A dedicated server has no player to lose relation, and a co-op peer that is not the authority leaves a shared-world change
    // to the host (CoopInterop: ShouldDeferToHost also covers co-op mods TAOM cannot probe).
    private bool Suppressed => _server.IsDedicatedServer || _coop.ShouldDeferToHost;

    // The settings when this peer may apply the act and the master switch and the act's own switch are on, otherwise null.
    private LongMemorySettings? Allowed(bool raid)
    {
        if (Suppressed)
            return null;
        var settings = _settings.Get();
        return settings.Enabled && (raid ? settings.RaidEnabled : settings.CoercionEnabled) ? settings : null;
    }

    // A village the player's clan owns is the player's own, and an act the player's party does not lead is somebody else's.
    private static bool Eligible(VillageHostileActionFacts facts) => facts.IsVillage && facts.PlayerLeads && !facts.PlayerClanOwns;

    private float MultiplierOf(SideMultipliers multipliers, VillageHostileActionFacts facts) =>
        multipliers.For(_alignment.ResolveSide(facts.Player), _alignment.ResolveSide(facts.OwnerSide));

    // The owner's clan leader and each notable lose their own amount. A dead hero and the player are skipped, and the strongest
    // loss lands once per relation owner, by GrantStrongestPerOwner. The settings clamp keeps an amount at 0 or more and Penalty
    // never returns a gain, so Long Memory never gives relation.
    private int Charge(VillageHostileActionFacts facts, int ownerAmount, int notableAmount, float multiplier, float scale, string reason)
    {
        var asks = new List<RelationAsk> { Ask(facts.OwnerLeader, ownerAmount, multiplier, scale) };
        foreach (var notable in facts.Notables)
            asks.Add(Ask(notable, notableAmount, multiplier, scale));
        return _grants.GrantStrongestPerOwner(asks, reason);
    }

    private static RelationAsk Ask(RelationTarget? hero, int amount, float multiplier, float scale) =>
        new(hero, LongMemoryRules.Penalty(amount, multiplier, scale));
}
