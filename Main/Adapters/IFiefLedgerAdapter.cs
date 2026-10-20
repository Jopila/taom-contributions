using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.ReevesLedger;

namespace TAOM.Adapters;

/// <summary>
/// Reads one fief for the Reeve's Ledger as plain values (docs/features/reeves-ledger.md). Every method needs a running
/// campaign and writes nothing. The caller wraps each call; the snapshot methods run once per window refresh, the
/// tooltip method on hover.
/// </summary>
public interface IFiefLedgerAdapter
{
    /// <summary>
    /// The fief's daily numbers through the campaign's model slots, so TAOM's tariff feats apply. Never withdraws: the
    /// engine's withdrawing form takes a fifth of the town's accumulated trade tax per call (RevenueSmoothenFraction = 5).
    /// </summary>
    ProfitInputs ReadProfit(Settlement settlement);

    /// <summary>The stash, one entry per stack.</summary>
    IReadOnlyList<StashEntry> ReadStash(Settlement settlement);

    /// <summary>The prison roster, one entry per hero and per regular troop type.</summary>
    IReadOnlyList<PrisonerEntry> ReadPrisoners(Settlement settlement);

    /// <summary>Men in the town's patrol party, or 0 when it has none.</summary>
    int ReadPatrolMen(Settlement settlement);

    /// <summary>The engine's patrol tooltip text, or an empty string when the town has no patrol party and no patrol behaviour.</summary>
    string PatrolTooltip(Settlement settlement);
}
