namespace TAOM.Features.NotableServices.Domain;

/// <summary>What the player has put on the screen's dummy side: regular troops and prisoners, heroes never counted.</summary>
public sealed record GiftTally(int Troops, int Prisoners)
{
    public static readonly GiftTally Empty = new(0, 0);

    public int Men => Troops + Prisoners;
}
