using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.CoopInterop;

/// <summary>
/// "Is this party or clan driven by a human player?" Under co-op the engine's
/// <c>MobileParty.MainParty</c> / <c>Clan.PlayerClan</c> only name the local peer; a remote player's
/// party looks like AI. Ask this instead of comparing against those statics.
///
/// Without a live co-op session the answer is exactly the local-player comparison, so singleplayer
/// behaviour is unchanged. Read live; never cache an answer, since players join and leave.
///
/// Cost: in a live session a check is one compiled delegate call per non-local entity, with no
/// argument array; a registry method of another shape falls back to a reflection invoke (about
/// 0.3 microseconds and 217 bytes).
/// </summary>
public interface IPlayerControlQuery
{
    PlayerControl OfParty(MobileParty? party);
    PlayerControl OfClan(Clan? clan);

    /// <summary>Without a session a hero counts when it is <c>Hero.MainHero</c>; in a session only a registered co-op player's hero does.</summary>
    PlayerControl OfHero(Hero? hero);
}
