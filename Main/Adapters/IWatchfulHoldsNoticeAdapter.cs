namespace TAOM.Adapters;

/// <summary>What the player sees and hears when a fort harries the march.</summary>
public interface IWatchfulHoldsNoticeAdapter
{
    /// <summary>One red message naming the fort (by settlement id) and the totals.</summary>
    void ShowHarried(string fortId, int killed, int wounded, int food);

    /// <summary>The attack horn; never throws.</summary>
    void PlayHorn();
}
