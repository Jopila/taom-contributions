using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.GoodDeeds.Models;

namespace TAOM.Adapters;

/// <summary>
/// The heroes, parties and settlements of Good Deeds, as plain facts (ADR-007). Every value is raw: whether a prisoner is a
/// lord, a spouse alive, a clan owned by the player is read here, and <c>GoodDeedsService</c> decides what each one means. Each
/// read answers null (or the all-false release source) when the campaign, the hero or the settlement cannot be found, or the
/// engine faults (logged by the adapter).
/// </summary>
public interface IGoodDeedsHeroAdapter
{
    /// <summary>
    /// The raw facts about the party a <c>HeroPrisonerReleased</c> event named (null for a release on the party screen): is it the
    /// player's own, is it no party with the player's party screen open, is it a settlement the player's clan owns. A read that
    /// cannot be made, or that faults (logged), answers <see cref="ReleaseSource.None"/>.
    /// </summary>
    ReleaseSource ReadReleaseSource(PartyBase? party);

    /// <summary>The released prisoner, the lord's spouse and clan leader, and the player's side.</summary>
    ReleaseFacts? ReadRelease(string prisonerId);

    /// <summary>The captive and the owner of the settlement the captive would be donated to.</summary>
    DonationFacts? ReadDonation(string heroId, string settlementId);

    /// <summary>The town of a finished tournament, by its settlement id.</summary>
    TournamentFacts? ReadTournament(string townSettlementId);
}
