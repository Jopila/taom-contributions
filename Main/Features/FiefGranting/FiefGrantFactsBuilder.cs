using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace TAOM.Features.FiefGranting;

/// <summary>
/// Boundary conversion for the fief-grant election (#458): sealed TaleWorlds types in, primitives
/// out. Extracted from <see cref="TaomSettlementClaimantDecision"/> so that class stays a thin entry
/// point (ADR-002).
///
/// This deliberately does NOT live on <see cref="IFiefGrantPolicyService"/>. Moving the counting into
/// the service would mean passing <c>Clan</c> and <c>Settlement</c> across the service boundary,
/// which ADR-007 forbids outright. Counting is conversion, not policy, so it belongs here at the
/// boundary, and the service keeps taking primitives only.
/// </summary>
internal static class FiefGrantFactsBuilder
{
    /// <summary>
    /// Snapshot one candidate clan for <paramref name="contested"/>, the settlement under vote.
    /// </summary>
    public static FiefGrantCandidateFacts Build(
        Clan clan, Settlement contested, IFiefSiegeParticipationService participation)
    {
        if (clan == null) return default;

        var kingdom = clan.Kingdom;

        var isRulingClan = kingdom?.Leader != null
                           && clan.Leader != null
                           && clan.Leader == kingdom.Leader;

        // Participation comes from TAOM's own record of the winning assault (#565), never from
        // Town.LastCapturedBy: that stamp names the assault leader's clan only, is never cleared,
        // and vanilla's merit already pays it a flat +30 that this override leaves in place.
        var settlementId = contested?.StringId;
        var recorded = participation != null && participation.HasRecord(settlementId);
        var share = recorded ? participation.GetContributionShare(settlementId, clan.StringId) : 0f;

        var settlementCulture = contested?.Culture;
        var isCultureMatch = settlementCulture != null
                             && clan.Culture != null
                             && clan.Culture == settlementCulture;

        return new FiefGrantCandidateFacts(
            CountClanFortifications(clan, contested),
            isRulingClan,
            share,
            recorded,
            isCultureMatch,
            clan == Clan.PlayerClan);
    }

    /// <summary>Fortifications a clan holds, excluding the contested one. This is the scoring input <see cref="Build"/> uses.</summary>
    public static int CountClanFortifications(Clan clan, Settlement contested) =>
        CountFortifications(clan?.Settlements, contested);

    /// <summary>
    /// Names of the fortifications a clan holds, excluding the contested one (#866): the ballot's hover list. It shares
    /// <see cref="EnumerateFortifications"/> with <see cref="CountClanFortifications"/>, so the count the scoring uses and the
    /// list the player reads cannot drift apart.
    /// </summary>
    internal static IReadOnlyList<string> ClanFortificationNames(Clan clan, Settlement contested) =>
        EnumerateFortifications(clan?.Settlements, contested).Select(s => s.Name?.ToString() ?? "").ToList();

    /// <summary>Fortifications a kingdom holds, excluding the contested one.</summary>
    public static int CountKingdomFortifications(Kingdom kingdom, Settlement contested) =>
        CountFortifications(kingdom?.Settlements, contested);

    private static int CountFortifications(
        MBReadOnlyList<Settlement> settlements, Settlement contested) =>
        EnumerateFortifications(settlements, contested).Count();

    /// <summary>
    /// Both <c>Clan.Settlements</c> and <c>Kingdom.Settlements</c> include bound villages, so the
    /// <c>IsFortification</c> filter is load-bearing rather than defensive. The contested settlement
    /// is excluded on every call: vanilla's own balance divisor excludes it (<c>Settlement == item</c>),
    /// and after <c>ApplyBySiege</c> the king already holds it, so counting it would penalise him for
    /// a fief nobody has been granted yet. Excluding it on both sides of the King's Vote share keeps
    /// that ratio measuring fiefs already settled.
    /// </summary>
    private static IEnumerable<Settlement> EnumerateFortifications(
        MBReadOnlyList<Settlement> settlements, Settlement contested)
    {
        if (settlements == null) yield break;

        for (var i = 0; i < settlements.Count; i++)
        {
            var settlement = settlements[i];
            if (settlement != null && settlement.IsFortification && settlement != contested)
                yield return settlement;
        }
    }
}
