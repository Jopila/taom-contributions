namespace TAOM.Features.FireAndTribute;

/// <summary>The four MCM switches of Fire and Tribute, read on every hourly tick while a host rides.</summary>
public interface IFireAndTributeSettings
{
    /// <summary>The master switch: off, no host rides and a live one is sent away (once its battle ends, if it is fighting).</summary>
    bool Enabled { get; }

    /// <summary>Hosts of the Free side: the outlaw bands of Gondor and Mirkwood.</summary>
    bool FreeHostsEnabled { get; }

    /// <summary>Hosts of the Neutral side: the Corsair Host of Umbar.</summary>
    bool NeutralHostsEnabled { get; }

    /// <summary>Hosts of the Evil side: Gundabad, Rhûn, Harad, Dunland and the Blacklocks of Erebor.</summary>
    bool EvilHostsEnabled { get; }
}
