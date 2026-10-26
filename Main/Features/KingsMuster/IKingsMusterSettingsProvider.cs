namespace TAOM.Features.KingsMuster;

/// <summary>The King's Muster MCM switch (#874, "War of the Ring/Armies"). Read on the main thread, on the AI's hourly army think.</summary>
public interface IKingsMusterSettingsProvider
{
    /// <summary>True when an AI kingdom ruler calls hosts for the configured fraction of the influence.</summary>
    bool DiscountEnabled { get; }
}
