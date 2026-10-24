namespace TAOM.Features.UnlookedForAid;

/// <summary>The MCM "World/Help Unlooked-For" switch.</summary>
public interface IUnlookedForAidSettingsProvider
{
    /// <summary>True when a strong enough lord stops fleeing a siege. Read on the AI worker thread, so it only reads a bool.</summary>
    bool Enabled { get; }
}
