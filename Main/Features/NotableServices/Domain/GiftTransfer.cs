using TAOM.Features.Execution;

namespace TAOM.Features.NotableServices.Domain;

/// <summary>
/// One question from the party screen: may this character move? <see cref="FromPlayer"/> is true when he stands on the
/// player's side (a gift), false when he is already on the gift side (taking him back). <see cref="MenGiven"/> is how many
/// men the gift side holds now.
/// </summary>
public sealed record GiftTransfer(bool IsHero, bool IsPrisoner, bool FromPlayer, SideIds Side, int MenGiven);
