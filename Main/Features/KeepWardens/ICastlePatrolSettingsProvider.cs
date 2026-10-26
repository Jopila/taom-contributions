namespace TAOM.Features.KeepWardens;

/// <summary>The MCM "Fief Management/Keep Wardens" switch. It gates only new castle patrols, never a live patrol's size limit.</summary>
public interface ICastlePatrolSettingsProvider
{
    /// <summary>True when castles with a Guard House may field a patrol. Read for every settlement the patrol queue visits.</summary>
    bool Enabled { get; }

    /// <summary>True when the AI builds a Guard House ahead of a random pick (#889). Read when an AI fief picks its next building.</summary>
    bool BuildPriorityEnabled { get; }
}
