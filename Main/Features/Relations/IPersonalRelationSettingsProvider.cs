namespace TAOM.Features.Relations;

/// <summary>The "Personal Relations" MCM group, clamped.</summary>
public interface IPersonalRelationSettingsProvider
{
    /// <summary>
    /// The master switch. Read on every relation read (main thread), so it is one read through the provider's cached settings
    /// reference: no container, no logging.
    /// </summary>
    bool Enabled { get; }

    /// <summary>The disband values. Allocates a record; for the rare reads (a disband cost).</summary>
    PersonalRelationSettings Get();
}
