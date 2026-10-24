namespace TAOM.Features.SwornAnew;

/// <summary>The MCM "World/Sworn Anew" switch. Read live at each defection question, none per frame.</summary>
public interface ISwornAnewSettingsProvider
{
    /// <summary>True when a captured lord can be asked to join the player's kingdom.</summary>
    bool Enabled { get; }
}
