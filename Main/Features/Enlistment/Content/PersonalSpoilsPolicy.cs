using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>One kill tried for a spoil, and the loot value it is aimed at.</summary>
public readonly struct SpoilsTarget
{
    public SpoilsTarget(string characterId, float targetValue)
    {
        CharacterId = characterId;
        TargetValue = targetValue;
    }

    public string CharacterId { get; }
    public float TargetValue { get; }
}

/// <summary>
/// Which of the player's own kills are tried for a spoil, in what order, and at what target value
/// (#834). Pure: the expected value per kill and the random source are injected. There is no cap here:
/// the cap counts items that LANDED, which only the payout can know, because the engine's roll often
/// returns nothing.
///
/// Order: highest expected value first, ties in kill order. Target =
/// <c>expected x (1 + scale x rank) x random(min..max)</c>. Empty ids and unreadable, non-finite and
/// non-positive expected values are skipped.
/// </summary>
public static class PersonalSpoilsPolicy
{
    public static IReadOnlyList<SpoilsTarget> Rank(
        IReadOnlyList<string> killedCharacterIds,
        Func<string, float?> expectedValue,
        int rankOrdinal,
        EnlistmentContentConfig config,
        Func<double> random01)
    {
        if (killedCharacterIds == null || killedCharacterIds.Count == 0 || config == null)
            return new List<SpoilsTarget>();

        var scale = 1.0 + config.SpoilsValueScalePerRank * Math.Max(0, rankOrdinal);
        return killedCharacterIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => new { Id = id, Value = expectedValue(id) })
            .Where(x => x.Value.HasValue && !float.IsNaN(x.Value.Value) && !float.IsInfinity(x.Value.Value) && x.Value.Value > 0f)
            .OrderByDescending(x => x.Value.Value)
            .Select(x =>
            {
                var roll = Math.Min(1.0, Math.Max(0.0, random01()));
                var factor = config.SpoilsRandomMin + roll * (config.SpoilsRandomMax - config.SpoilsRandomMin);
                return new SpoilsTarget(x.Id, (float)(x.Value.Value * scale * factor));
            })
            .ToList();
    }
}
