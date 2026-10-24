namespace TAOM.Features.Messengers;

public class MessengerConfig
{
    public float AccidentChancePerHour { get; set; } = 0.002f;
    public float TravelSpeedMultiplier { get; set; } = 1.0f;

    /// <summary>Denars added to the base price per in-game day of the trip. Range [0, 1000].</summary>
    public float GoldPerTravelDay { get; set; } = 25f;

    /// <summary>Price multiplier when the target is at war with the player. Range [1, 10].</summary>
    public float AtWarMultiplier { get; set; } = 2f;

    /// <summary>Price multiplier when the player has never met the target. Range [1, 10].</summary>
    public float NeverMetMultiplier { get; set; } = 2f;

    /// <summary>Price multiplier when the target sits in a besieged settlement. Range [1, 10].</summary>
    public float BesiegedMultiplier { get; set; } = 5f;
}
