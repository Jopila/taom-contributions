using System.Collections.Generic;
using TAOM.Adapters;

namespace TAOM.Features.FarWatch;

/// <summary>What a ride is for. The values are saved, so they never change.</summary>
internal enum ScoutKind
{
    None = 0,
    Surroundings = 1,
    Settlement = 2,
}

/// <summary>Why the camp option is greyed. The first that applies wins, in this order.</summary>
internal enum ScoutBlock
{
    None = 0,
    SharedCampaign,
    Busy,
    CampNotReady,
    NoScout,
    Wounded,
    Resting,
}

internal enum ScoutOfferStatus
{
    /// <summary>No option: the camp feature is off and no ride runs.</summary>
    Hidden,

    /// <summary>A ride runs: the option reads "Await your scout".</summary>
    Running,

    Blocked,

    Ready,
}

/// <summary>Everything that ends a ride early. A return on time is not an end, it is a completion.</summary>
internal enum ScoutEndReason
{
    CallBack,
    Battle,
    CampStruck,
    Lost,
    SharedCampaign,
}

internal enum ScoutComposition
{
    None,
    Foot,
    Archers,
    Horse,
    HorseArchers,
}

/// <summary>What the camp option shows now, and who would ride.</summary>
internal readonly struct ScoutOffer
{
    internal ScoutOffer(ScoutOfferStatus status, ScoutBlock block = ScoutBlock.None, string? scoutId = null, string? scoutName = null,
        int scoutSkill = 0, bool selfScout = false, int restHoursLeft = 0)
    {
        Status = status;
        Block = block;
        ScoutId = scoutId;
        ScoutName = scoutName;
        ScoutSkill = scoutSkill;
        SelfScout = selfScout;
        RestHoursLeft = restHoursLeft;
    }

    internal ScoutOfferStatus Status { get; }

    internal ScoutBlock Block { get; }

    internal string? ScoutId { get; }

    internal string? ScoutName { get; }

    internal int ScoutSkill { get; }

    /// <summary>The player rides out himself because the party is only him.</summary>
    internal bool SelfScout { get; }

    /// <summary>Whole hours of rest left, rounded up; only for <see cref="ScoutBlock.Resting"/>.</summary>
    internal int RestHoursLeft { get; }
}

/// <summary>The running ride, from its stored start. Cheap enough for the per-frame wait tick.</summary>
internal readonly struct ScoutProgress
{
    internal ScoutProgress(bool running, float fraction, double totalHours, ScoutKind kind, string? scoutName, string? targetName,
        bool selfScout = false)
    {
        SelfScout = selfScout;
        Running = running;
        Fraction = fraction;
        TotalHours = totalHours;
        Kind = kind;
        ScoutName = scoutName;
        TargetName = targetName;
    }

    internal bool Running { get; }

    /// <summary>0 to 1.</summary>
    internal float Fraction { get; }

    internal double TotalHours { get; }

    internal ScoutKind Kind { get; }

    internal string? ScoutName { get; }

    internal string? TargetName { get; }

    /// <summary>The player rides out himself (his party was only him when the ride started).</summary>
    internal bool SelfScout { get; }
}

/// <summary>What the hourly look decided about the running ride.</summary>
internal enum ScoutTurnKind
{
    Nothing,
    Ended,
    Due,
}

internal readonly struct ScoutTurn
{
    internal ScoutTurn(ScoutTurnKind kind, ScoutEndNotice? notice = null)
    {
        Kind = kind;
        Notice = notice;
    }

    internal ScoutTurnKind Kind { get; }

    /// <summary>What to tell the player when the ride ended (<see cref="ScoutTurnKind.Ended"/>); null otherwise.</summary>
    internal ScoutEndNotice? Notice { get; }
}

/// <summary>The one-line message of a ride that ended early.</summary>
internal readonly struct ScoutEndNotice
{
    internal ScoutEndNotice(ScoutEndReason reason, string? scoutName)
    {
        Reason = reason;
        ScoutName = scoutName;
    }

    internal ScoutEndReason Reason { get; }

    internal string? ScoutName { get; }
}

/// <summary>One host in the surroundings report: its name, the count as the scout now knows it, what it is made of, and where.</summary>
internal sealed class ScoutHostRow
{
    internal ScoutHostRow(string name, string count, ScoutComposition composition, int direction)
    {
        Name = name;
        Count = count;
        Composition = composition;
        Direction = direction;
    }

    internal string Name { get; }

    /// <summary>Hidden Hosts' mask text (digits known and "?"), or the exact number with the fog off.</summary>
    internal string Count { get; }

    internal ScoutComposition Composition { get; }

    /// <summary>The compass sector, 0 (north) to 7 (north-west).</summary>
    internal int Direction { get; }
}

/// <summary>One defender party in the settlement report.</summary>
internal sealed class ScoutDefenderRow
{
    internal ScoutDefenderRow(ScoutDefenderKind kind, string name, int count, ScoutComposition composition)
    {
        Kind = kind;
        Name = name;
        Count = count;
        Composition = composition;
    }

    internal ScoutDefenderKind Kind { get; }

    internal string Name { get; }

    internal int Count { get; }

    internal ScoutComposition Composition { get; }
}

/// <summary>The data of a returned ride; <c>FarWatchReportText</c> renders it. Plain values only.</summary>
internal sealed class ScoutReport
{
    internal ScoutReport(ScoutKind kind, string scoutName, bool lost)
    {
        Kind = kind;
        ScoutName = scoutName;
        Lost = lost;
    }

    internal ScoutKind Kind { get; }

    internal string ScoutName { get; }

    /// <summary>The hero was gone when the ride was due: no report, no XP.</summary>
    internal bool Lost { get; }

    /// <summary>The ride was ended instead of completed (Field Camp was switched off): tell the player this one line, show no report.</summary>
    internal ScoutEndNotice? Ended { get; init; }

    internal List<ScoutHostRow> Hosts { get; } = new();

    /// <summary>Whole hours the raised sight holds at full strength; 0 when this ride gave none.</summary>
    internal int SightHours { get; set; }

    internal string? SiteName { get; set; }

    /// <summary>The exact defenders of the settlement.</summary>
    internal int SiteTotal { get; set; }

    internal List<ScoutDefenderRow> Defenders { get; } = new();
}
