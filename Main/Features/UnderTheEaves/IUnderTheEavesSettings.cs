namespace TAOM.Features.UnderTheEaves;

/// <summary>The Under the Eaves MCM knob (#849).</summary>
public interface IUnderTheEavesSettings
{
    /// <summary>Off returns a zero factor from the contributor before any terrain is read; vanilla's sight stands.</summary>
    bool Enabled { get; }
}
