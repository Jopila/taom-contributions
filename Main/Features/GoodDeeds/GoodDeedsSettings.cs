using TAOM.Features.Relations;

namespace TAOM.Features.GoodDeeds;

/// <summary>
/// The "Good Deeds" MCM group after clamping. The amounts are the relation asked for before the alignment multiplier. The
/// release amounts go to the lord, to the lord's living spouse and to the lord's clan leader; the donation amount is the bonus
/// added to vanilla's gain; the tournament amount goes to the town owner's clan leader and to each notable.
/// </summary>
public sealed record GoodDeedsSettings(
    bool Enabled,
    bool ReleaseEnabled,
    bool DonationEnabled,
    bool TournamentEnabled,
    int ReleaseLordAmount,
    int ReleaseSpouseAmount,
    int ReleaseLeaderAmount,
    int DonationAmount,
    int TournamentAmount,
    SideMultipliers Release,
    SideMultipliers Donation,
    SideMultipliers Tournament);
