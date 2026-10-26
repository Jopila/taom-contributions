using TAOM.Features.CoopInterop;

namespace TAOM.Adapters;

/// <summary>A hero as the skill XP decision sees it (#890), with no engine type in it. <c>default</c> is a non-lord.</summary>
/// <param name="IsLord">The hero is a lord (<c>Hero.IsLord</c>).</param>
/// <param name="IsMainHero">The hero is this peer's main hero.</param>
/// <param name="ClanControl">Who drives the hero's clan. Read only for a lord who is not the main hero.</param>
public readonly record struct LordXpFacts(bool IsLord, bool IsMainHero, PlayerControl ClanControl);

/// <summary>A party as the training decision sees it (#890), with no engine type in it. <c>default</c> is not a lord party.</summary>
/// <param name="IsLordParty">The party is a lord party (<c>MobileParty.IsLordParty</c>), which a caravan, garrison or patrol is not.</param>
/// <param name="IsMainParty">The party is this peer's main party.</param>
/// <param name="OwnerClanControl">Who drives the clan of the party's owner. Read only for a lord party.</param>
/// <param name="ArmyLeaderControl">Who drives the leader party of the army the party is in; not a player when it is in no army.</param>
public readonly record struct TrainingPartyFacts(
    bool IsLordParty,
    bool IsMainParty,
    PlayerControl OwnerClanControl,
    PlayerControl ArmyLeaderControl);
