namespace TAOM.Features.HarvestRoad;

/// <summary>The MCM "Map UI/Settlement Nameplates" switch for Harvest Road (#912).</summary>
public interface IHarvestRoadSettingsProvider
{
    /// <summary>True when villager deliveries and AI food purchases raise the map's trade notice. Read on the main thread, per AI food purchase and per villager delivery.</summary>
    bool Enabled { get; }
}
