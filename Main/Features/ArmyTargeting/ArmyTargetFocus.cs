using System.Collections.Generic;

namespace TAOM.Features.ArmyTargeting;

/// <summary>
/// One army focus window (#765, docs/features/army-targeting.md "Army focus"): while it stands, the
/// attackers' Besieger armies and the defender's Defender armies score <see cref="SettlementId"/> times
/// <see cref="Multiplier"/>. Written by the War Chronicle; <see cref="ArmyTargetFocusRegistry"/> copies and
/// validates it, so a caller may reuse its lists.
/// </summary>
public sealed class ArmyTargetFocus
{
    public ArmyTargetFocus(string? settlementId, IReadOnlyList<string>? attackerFactionIds, string? defenderFactionId, float multiplier)
    {
        SettlementId = settlementId;
        AttackerFactionIds = attackerFactionIds ?? new string[0];
        DefenderFactionId = defenderFactionId;
        Multiplier = multiplier;
    }

    /// <summary><c>Settlement.StringId</c> of the target.</summary>
    public string? SettlementId { get; }

    /// <summary><c>MapFaction.StringId</c> of each attacking kingdom.</summary>
    public IReadOnlyList<string> AttackerFactionIds { get; }

    /// <summary><c>MapFaction.StringId</c> of the defending kingdom; null for none.</summary>
    public string? DefenderFactionId { get; }

    /// <summary>The score multiplier, above 1. The registry skips anything else and clamps it to its cap.</summary>
    public float Multiplier { get; }
}
