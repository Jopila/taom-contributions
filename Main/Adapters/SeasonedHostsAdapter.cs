using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.CoopInterop;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="ISeasonedHostsAdapter"/>. "Who drives it" comes from <see cref="IPlayerControlQuery"/>,
/// never from a comparison with <c>Clan.PlayerClan</c>, so a remote co-op player's clan and party read as human-driven. The
/// owner is <c>MobileParty.Owner</c> (<c>PartyBase.Owner</c> is banned by <c>PartyOwnerGetterBanTests</c>).
/// </summary>
public sealed class SeasonedHostsAdapter : ISeasonedHostsAdapter
{
    private readonly IPlayerControlQuery _control;

    public SeasonedHostsAdapter(IPlayerControlQuery control) => _control = control;

    public LordXpFacts Read(Hero? hero)
    {
        if (hero == null || !hero.IsLord)
            return default;

        if (hero == Hero.MainHero)
            return new LordXpFacts(IsLord: true, IsMainHero: true, ClanControl: PlayerControl.Player);

        return new LordXpFacts(IsLord: true, IsMainHero: false, ClanControl: _control.OfClan(hero.Clan));
    }

    public TrainingPartyFacts Read(MobileParty? party)
    {
        if (party == null || !party.IsLordParty)
            return default;

        if (party == MobileParty.MainParty)
            return new TrainingPartyFacts(IsLordParty: true, IsMainParty: true, PlayerControl.Player, PlayerControl.Player);

        return new TrainingPartyFacts(
            IsLordParty: true,
            IsMainParty: false,
            OwnerClanControl: _control.OfClan(party.Owner?.Clan),
            ArmyLeaderControl: _control.OfParty(party.Army?.LeaderParty));
    }
}
