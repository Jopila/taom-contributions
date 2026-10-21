namespace TAOM.Adapters;

/// <summary>What the player reads and hears when a raiding host rides, turns home or breaks (#870).</summary>
public interface IFireAndTributeNoticeAdapter
{
    /// <summary>A red rumour naming the host and the town nearest to it, with the attack horn.</summary>
    void ShowSpawned(string keyedHostName, string? nearestTownId);

    /// <summary>A neutral line: the host has had its fill and turns home.</summary>
    void ShowGoingHome(string keyedHostName);

    /// <summary>A neutral line: the host reached home and disbanded.</summary>
    void ShowWentHome(string keyedHostName);

    /// <summary>A neutral line: the host was broken.</summary>
    void ShowBroken(string keyedHostName);
}
