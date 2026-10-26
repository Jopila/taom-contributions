namespace TAOM.Features.NotableServices.Domain;

/// <summary>The introduction on offer: how many notables, what it costs, and whether the player can pay.</summary>
public sealed record IntroduceQuote(int Count, int Price, bool CanAfford);
