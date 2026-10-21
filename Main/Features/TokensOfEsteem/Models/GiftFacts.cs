namespace TAOM.Features.TokensOfEsteem.Models;

/// <summary>
/// The player's side of a gift, as plain values read by <c>IGiftHeroAdapter</c>. Raw facts only: the service decides every
/// rule. <see cref="SideFactionId"/> and <see cref="SideCultureId"/> come from one entity, the player's map faction (the
/// kingdom, or the player's own clan <c>player_faction</c> without one) and that faction's own culture: the pair
/// <c>IAlignmentService.ResolveSide</c> requires. Either is empty when there is no faction. <see cref="Today"/> is the campaign
/// day number the cooldown counts in.
/// </summary>
public sealed record GiftPlayerFacts(string HeroId, string SideFactionId, string SideCultureId, int Gold, int Today);

/// <summary>
/// The hero who would receive a gift, as raw facts (nothing here is a verdict). <see cref="Relation"/> is the player's
/// relation with this hero, -100 to 100. <see cref="RelationOwnerId"/> is the hero whose relation with the player a change
/// to this hero really moves (<c>GetHeroesForEffectiveRelation</c>: his clan leader unless personal relations are on), which
/// is the key of the cooldown. <see cref="RaceName"/> is empty when the hero's race id is not a known race.
/// <see cref="CultureId"/> is the hero's own culture, read only for tastes. <see cref="SideFactionId"/> and
/// <see cref="SideCultureId"/> are the hero's map faction id and that faction's own culture, the pair the alignment gate
/// resolves (#957).
/// <see cref="IsInPlayerParty"/> is true when the hero travels in the player's own party.
/// </summary>
public sealed record GiftRecipientFacts(
    string HeroId,
    string RelationOwnerId,
    bool IsLord,
    bool IsChild,
    bool IsPrisoner,
    bool IsInPlayerClan,
    bool IsPlayerCompanion,
    bool IsInPlayerParty,
    bool IsAtWarWithPlayer,
    int Relation,
    string RaceName,
    string CultureId,
    string SideFactionId,
    string SideCultureId);

/// <summary>
/// One stack in the player's party inventory whose item category the catalog lists. <see cref="IsModified"/> is true for a
/// "Sharp" or "Battered" copy (an item modifier): the service never lists one, because the inventory adapter removes only
/// from the unmodified stack.
/// </summary>
public sealed record HeldGood(string ItemId, string Name, string CategoryId, int Value, int Count, bool IsModified);
