using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.FieldCamp.Domain;

public enum CampType
{
    Field = 0,
    Fortified = 1,
    Ambush = 2,
    Lookout = 3,
}

/// <summary>
/// One pitched camp, keyed in the camp book by the owning party's StringId (in practice only the
/// main party pitches camps; the key shape is kept from the source so a future AI-camp feature
/// does not need a save break).
///
/// <para>Build progress is never stored: <see cref="EstablishedAt"/> + <see cref="BuildHours"/>
/// derive it, so a mid-build save resumes exactly.</para>
/// </summary>
public sealed class CampState
{
    [SaveableField(101)] public int Type;
    [SaveableField(102)] public CampaignTime EstablishedAt;
    [SaveableField(103)] public float BuildHours;
    [SaveableField(104)] public bool Foraging;
    [SaveableField(105)] public float ForageAccumulator;
    [SaveableField(106)] public int ForagedTotal;
    [SaveableField(107)] public bool VisualShown;

    // Camp work (#860). Progress is the hours this work's wait menu has ticked, saved here, never derived from a start time: the
    // engine gives the tick only the time that passed while the wait was active, so time in an interrupting encounter or on the
    // map does not count, and a start time would count it. A save from before the feature has none of these keys, so they read as
    // zero: no activity.
    [SaveableField(108)] public int Activity;
    [SaveableField(109)] public float ActivityHoursDone;
    [SaveableField(110)] public float ActivityTargetHours;
    [SaveableField(111)] public bool InjuryRolled;

    /// <summary>Clears the running work and its progress. Used by the finish, the stop, and a fault.</summary>
    public void ClearActivity()
    {
        Activity = 0;
        ActivityHoursDone = 0f;
        ActivityTargetHours = 0f;
        InjuryRolled = false;
    }

    public CampType TypeEnum
    {
        get => (CampType)Type;
        set => Type = (int)value;
    }

    /// <summary>0..1+ raise progress; degenerate build hours count as complete.</summary>
    public float BuildProgress()
    {
        if (BuildHours <= 0f)
            return 1f;
        return (float)EstablishedAt.ElapsedHoursUntilNow / BuildHours;
    }

    public bool IsReady => BuildProgress() >= 1f;
}
