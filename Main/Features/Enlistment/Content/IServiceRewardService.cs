using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>
/// THE reward chokepoint: every duty/incident/merit/battle payout and the daily wage flow
/// through here (the donor inlined payouts at ~20 sites — 10 of them minting gold from
/// nothing). Bonuses are minted (army-coffers fiction, config-bounded); the daily wage is
/// commander-funded via WagePolicy with arrears.
/// </summary>
public interface IServiceRewardService
{
    /// <summary>Apply a reward spec: service XP + skill XP + minted gold + trust/relation/reputation.</summary>
    void Grant(RewardSpec reward, string sourceLabel);

    /// <summary>Commander-funded daily wage per WagePolicy; mutates arrears in the content record.</summary>
    WageDecision PayDailyWage();

    /// <summary>
    /// Pays a one-off bonus (#836) as a gift: from the commander's purse above the wage floor when wages are
    /// commander-funded (never more than he holds above it), minted in full when wages are. The unpaid part
    /// is not owed and arrears are untouched. Returns what reached the player.
    /// </summary>
    int PayBonusFromCommander(int amount, string reason);

    void AdjustTrust(int delta);

    void ApplyReputation(ReputationDomain domain, int amount);
}
