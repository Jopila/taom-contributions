namespace TAOM.Features.WatchfulHolds;

/// <summary>The two MCM switches of Watchful Holds, read on every hourly party tick.</summary>
public interface IWatchfulHoldsSettings
{
    /// <summary>The master switch: off, no fort harasses anyone.</summary>
    bool Enabled { get; }

    /// <summary>Whether AI lords are disorganized too; the player is harassed either way.</summary>
    bool HarassAiLords { get; }
}
