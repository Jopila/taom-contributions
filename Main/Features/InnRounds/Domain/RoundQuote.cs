namespace TAOM.Features.InnRounds.Domain;

/// <summary>
/// A round on offer: how many drinks, what one costs, the whole price, whether the player can pay it, and whether today's
/// rounds are used up (the line is then greyed with that reason, ahead of the price).
/// </summary>
public sealed record RoundQuote(int Heads, int PricePerHead, int Price, bool CanAfford, bool CapReached);
