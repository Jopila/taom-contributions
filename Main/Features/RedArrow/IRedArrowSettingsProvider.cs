namespace TAOM.Features.RedArrow;

/// <summary>The MCM "War of the Ring/Armies" switch for the Red Arrow (#873).</summary>
public interface IRedArrowSettingsProvider
{
    /// <summary>True when the player may call and release army members during a siege they lead. Read on the main thread, from the Army Management screen.</summary>
    bool Enabled { get; }
}
