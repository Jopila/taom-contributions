namespace TAOM.Features.WatchfulHolds;

public interface IWatchfulHoldsConfigProvider
{
    /// <summary>The validated config, loaded once per process; an edit to the file needs a full restart.</summary>
    WatchfulHoldsConfig GetConfig();
}
