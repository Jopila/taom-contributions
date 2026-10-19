using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.HardQuestions.Domain;

namespace TAOM.Features.HardQuestions;

/// <summary>
/// The costs of a hard deed, shared by the forced question and the strip: relation changes scaled by side
/// through <see cref="IExecutionRelationService"/> (enemy sides: only lords on the victim's side take the
/// hit; same side: 1.5 times for everyone), trait experience scaled by a configured multiplier per case, and
/// companion judgement.
/// </summary>
public sealed class HardQuestionsPenaltyApplier
{
    /// <summary>Honor and Mercy run from -2 to 2 (DefaultTraits), so a companion's level is at most this.</summary>
    internal const int TraitMaxLevel = 2;

    private readonly IAlignmentService _alignment;
    private readonly IExecutionRelationService _relations;
    private readonly IArmouryPlayerAdapter _player;
    private readonly IHardQuestionsActionAdapter _actions;
    private readonly IPlayerTraitAdapter _traits;
    private readonly IHardQuestionsConfigProvider _config;
    private readonly IDedicatedServerProvider _server;

    public HardQuestionsPenaltyApplier(IAlignmentService alignment, IExecutionRelationService relations,
        IArmouryPlayerAdapter player, IHardQuestionsActionAdapter actions, IPlayerTraitAdapter traits,
        IHardQuestionsConfigProvider config,
        IDedicatedServerProvider server)
    {
        _alignment = alignment;
        _relations = relations;
        _player = player;
        _actions = actions;
        _traits = traits;
        _config = config;
        _server = server;
    }

    /// <summary>
    /// Charges the player's relation. The engine keeps a clan member's relation on his clan leader, so deltas
    /// are collected per relation owner (<see cref="LordRef.RelationId"/>), the strongest one wins, and each
    /// owner is charged once whatever order the hits come in, then one summary line reports how many
    /// owners were charged (none on a dedicated server). A Neutral victim is nobody's ally, so the
    /// alignment rule would charge even his own relation owner nothing; that owner always takes at least the
    /// base hit. Every other lord, whatever his side, stays unmoved, as in executions.
    /// </summary>
    public void ApplyRelations(LordRef victim, IEnumerable<KeyValuePair<LordRef, float>> hits)
    {
        var strongest = new Dictionary<string, int>(StringComparer.Ordinal);
        var executor = new ExecutionParticipant(_player.KingdomId ?? string.Empty, _player.CultureId ?? string.Empty);
        var victimParticipant = Participant(victim);
        foreach (var hit in hits)
        {
            var baseDelta = (int)Math.Round(hit.Value);
            var modified = _relations.GetRelationModifier(executor, victimParticipant, Participant(hit.Key), baseDelta, false)
                .RelationDelta;
            if (hit.Key.RelationId == victim.RelationId)
                modified = Math.Min(modified, baseDelta);
            if (modified == 0)
                continue;
            strongest[hit.Key.RelationId] = strongest.TryGetValue(hit.Key.RelationId, out var seen) ? Math.Min(seen, modified) : modified;
        }

        var charged = 0;
        foreach (var pair in strongest)
            if (_actions.ChangeRelation(pair.Key, pair.Value))
                charged++;
        if (charged > 0 && !_server.IsDedicatedServer)
            _actions.ShowRelationsHurt(charged);
    }

    /// <summary>Adds scaled trait experience (a loss when negative). The trait ids are "Honor" and "Mercy".</summary>
    public void ApplyTraitXp(LordRef victim, string traitId, float baseXp)
    {
        var scaled = (int)Math.Round(baseXp * TraitMultiplier(victim));
        if (scaled != 0)
            _traits.AddTraitXp(traitId, scaled);
    }

    /// <summary>
    /// Companions whose Mercy (or Honor) is above zero lose relation in proportion to it: the cap at the trait's
    /// top level (2), half of it at level 1. Returns the ids that lost any, in the order given.
    /// </summary>
    public IReadOnlyList<string> ApplyCompanionLosses(IReadOnlyList<CompanionRef> companions, bool byHonor, float cap)
    {
        var losers = new List<string>();
        foreach (var companion in companions)
        {
            var level = Math.Min(byHonor ? companion.HonorLevel : companion.MercyLevel, TraitMaxLevel);
            if (level <= 0)
                continue;
            var loss = (int)Math.Round(cap * level / TraitMaxLevel);
            if (loss >= 0)
                continue;
            if (_actions.ChangeRelation(companion.Id, loss))
                losers.Add(companion.Id);
        }
        return losers;
    }

    private float TraitMultiplier(LordRef victim)
    {
        var config = _config.GetConfig();
        var player = _alignment.ResolveSide(_player.KingdomId ?? string.Empty, _player.CultureId ?? string.Empty);
        var other = _alignment.ResolveSide(victim.KingdomId ?? string.Empty, victim.CultureId ?? string.Empty);
        if (player == FactionSide.Neutral || other == FactionSide.Neutral)
            return config.NeutralTraitMultiplier;
        return _alignment.AreEnemyAlignments(player, other) ? config.EnemySideTraitMultiplier : config.SameSideTraitMultiplier;
    }

    private static ExecutionParticipant Participant(LordRef lord) =>
        new(lord.KingdomId ?? string.Empty, lord.CultureId ?? string.Empty);
}
