namespace TAOM.Features.Messengers.Domain;

/// <summary>
/// What the price of one messenger depends on, read at the boundary (#898): the straight-line distance from the player to
/// the target, the map diagonal, the in-game days a full crossing takes, and three situations that raise the price.
/// A NaN or negative <see cref="Distance"/> stands for a target whose position is unknown and counts as a full trip.
/// </summary>
public readonly record struct MessengerQuoteInput(
    float Distance,
    float MapDiagonal,
    int TravelDays,
    bool AtWar,
    bool NeverMet,
    bool Besieged);
