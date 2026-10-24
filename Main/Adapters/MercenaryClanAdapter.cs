using System;
using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IMercenaryClanAdapter"/> over one engine <see cref="Clan"/>. <c>Clan.PlayerClan</c> is a
/// computed getter that throws when no campaign is live (adapters.md), so it is read only after <c>Campaign.Current</c> is
/// checked (<c>Campaign.PlayerDefaultFaction</c>, which it reads, is internal). The date is the engine's own saved <c>Clan.ShouldStayInKingdomUntil</c>; the remaining time is
/// its <c>RemainingDaysFromNow</c>, and a write sets it through <c>CampaignTime.DaysFromNow</c>.
/// </summary>
public sealed class MercenaryClanAdapter : IMercenaryClanAdapter
{
    private readonly Clan _clan;

    public MercenaryClanAdapter(Clan clan) => _clan = clan ?? throw new ArgumentNullException(nameof(clan));

    public bool IsPlayerClan => Campaign.Current != null && ReferenceEquals(_clan, Clan.PlayerClan);

    public float RemainingStayDays => _clan.ShouldStayInKingdomUntil.RemainingDaysFromNow;

    public void SetStayUntilDaysFromNow(int days) => _clan.ShouldStayInKingdomUntil = CampaignTime.DaysFromNow(days);
}
