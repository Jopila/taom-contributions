using System;
using TAOM.Adapters;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Features.WardensTally;

/// <summary>Warden's Tally (docs/features/wardens-tally.md, #903), the pure part: one <see cref="DungeonReading"/> becomes a <see cref="WardensTallyView"/>, with every number taken from <see cref="WarEscapeService"/>.</summary>
public sealed class WardensTallyService
{
    private readonly WarEscapeService _escape;

    public WardensTallyService(WarEscapeService escape) => _escape = escape;

    /// <summary>
    /// The tally, or null with no reading or no lord (vanilla's sentence then says "There are no prisoners here.", which a count
    /// of regulars under it would contradict). <paramref name="boostOf"/> gives a kingdom's escape multiplier; null means no boost.
    /// </summary>
    public WardensTallyView? Build(DungeonReading? reading, Func<string, float>? boostOf)
    {
        if (reading == null || reading.HeldRegulars < 0 || reading.HeldLords <= 0)
            return null;

        var view = new WardensTallyView
        {
            Held = (int)Math.Min(int.MaxValue, (long)reading.HeldRegulars + reading.HeldLords),
            Capacity = reading.Capacity,
            Lords = reading.HeldLords,
        };

        DungeonLord? shared = null;
        foreach (var lord in reading.Lords)
        {
            if (lord?.Snapshot == null)
                continue;

            if (!lord.Snapshot.CanBeReleased)
            {
                view.Vetoed.Add(lord.Name);
                continue;
            }

            shared ??= lord;
            AddBoost(view, lord, boostOf);
        }

        if (shared != null)
            view.Odds = Describe(shared);
        return view;
    }

    private void AddBoost(WardensTallyView view, DungeonLord lord, Func<string, float>? boostOf)
    {
        var kingdom = lord.Snapshot.KingdomId;
        if (boostOf == null || string.IsNullOrEmpty(kingdom))
            return;

        var extra = _escape.ExtraChance(lord.Snapshot, boostOf(kingdom));
        if (!(extra > 0f))
            return;

        var vanilla = _escape.VanillaChance(lord.Snapshot);
        var combined = 1f - (1f - vanilla) * (1f - extra);
        view.Boosts.Add(new TallyBoost(lord.Name, Math.Min(1f, Math.Max(0f, combined))));
    }

    private TallyOdds Describe(DungeonLord lord)
    {
        var shown = new TallyOdds
        {
            Chance = _escape.VanillaChance(lord.Snapshot),
            Base = _escape.BaseChanceOf(lord.Snapshot),
            Halved = lord.Snapshot.PlayerHeld,
        };
        shown.Terms.AddRange(lord.Terms);
        return shown;
    }
}
