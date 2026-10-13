using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Ledger;
using TAOM.Features.WarOfTheRingMomentum;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// What a chronicle resolution writes (docs/features/war-chronicle.md "Events"): the outcome's timed
/// effects into the shared registry, its optional momentum award, and one <c>[WarLedger] ev=chronicle</c>
/// line. Lapsed and Skipped write the ledger line only.
///
/// <para><b>Targets.</b> <c>attacker</c> is every listed attacker, <c>defender</c> the defender, <c>free</c>
/// and <c>evil</c> every kingdom of that side (<see cref="IAlignmentService.ResolveSide"/> with the
/// kingdom's own culture), anything else a kingdom id. Only kingdoms alive now are written. Each row is its
/// own source, <c>chronicle:&lt;event&gt;:&lt;outcome&gt;:&lt;row&gt;</c>, so two rows reaching one kingdom
/// add up instead of replacing each other. The player rule holds at the consumers, which are passed the
/// owner's kingdom id and a player-clan flag (<see cref="VolunteerProductionService"/>,
/// <see cref="WarEscapeService"/>): the player's own clan never gains or loses.</para>
/// </summary>
public sealed class ChronicleOutcomeService
{
    private readonly IWarEffectService _effects;
    private readonly IAllianceAdapter _alliance;
    private readonly IAlignmentService _alignment;
    private readonly IMomentumAwardService _momentum;
    private readonly WarLedgerService _ledger;
    private readonly IChroniclePresenter _presenter;

    public ChronicleOutcomeService(
        IWarEffectService effects, IAllianceAdapter alliance, IAlignmentService alignment,
        IMomentumAwardService momentum, WarLedgerService ledger, IChroniclePresenter presenter)
    {
        _effects = effects;
        _alliance = alliance;
        _alignment = alignment;
        _momentum = momentum;
        _ledger = ledger;
        _presenter = presenter;
    }

    public void Apply(ChronicleEventDefinition definition, ChronicleEventState outcome, double nowHours)
    {
        var block = definition.OutcomeFor(outcome);
        if (block != null)
        {
            var live = new HashSet<string>(_alliance.GetAllKingdomIds() ?? new List<string>(), StringComparer.Ordinal);
            for (var row = 0; row < block.Effects.Count; row++)
            {
                var effect = block.Effects[row];
                var source = $"chronicle:{definition.Id}:{outcome}:{row}";
                foreach (var kingdom in Expand(definition, effect.Target, live))
                    _effects.Apply(new WarEffect(source, kingdom, effect.Kind, effect.Magnitude, nowHours + effect.DurationDays * 24d));
            }

            if (block.Momentum != null)
                _momentum.TryAwardScripted(block.Momentum.Side, block.Momentum.Units, _presenter.Resolve(definition.Title), nowHours);
        }

        _ledger.LogChronicleResolution(definition.Id, outcome.ToString());
    }

    /// <summary>The kingdom ids an effect target names, limited to the live ones.</summary>
    internal IEnumerable<string> Expand(ChronicleEventDefinition definition, string target, ICollection<string> live)
    {
        switch (target)
        {
            case ChronicleConfigValidator.TargetAttacker:
                foreach (var attacker in definition.Attackers)
                {
                    if (live.Contains(attacker))
                        yield return attacker;
                }

                yield break;

            case ChronicleConfigValidator.TargetDefender:
                if (definition.Defender != null && live.Contains(definition.Defender))
                    yield return definition.Defender;
                yield break;

            case ChronicleConfigValidator.TargetFree:
            case ChronicleConfigValidator.TargetEvil:
                var side = target == ChronicleConfigValidator.TargetFree ? FactionSide.Free : FactionSide.Evil;
                foreach (var kingdom in live)
                {
                    if (_alignment.ResolveSide(kingdom, _alliance.GetKingdomCultureId(kingdom)) == side)
                        yield return kingdom;
                }

                yield break;

            default:
                if (live.Contains(target))
                    yield return target;
                yield break;
        }
    }
}
