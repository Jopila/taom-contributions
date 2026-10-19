namespace TAOM.Features.HiddenHosts;

/// <summary>The Hidden Hosts MCM knob (#846).</summary>
public interface IHiddenHostsSettings
{
    /// <summary>Off returns every hook at once, with no work, and leaves vanilla's numbers on screen.</summary>
    bool Enabled { get; }
}
