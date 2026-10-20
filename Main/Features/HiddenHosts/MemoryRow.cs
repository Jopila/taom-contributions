namespace TAOM.Features.HiddenHosts;

/// <summary>Which table a remembered count belongs to (#846 phase 3).</summary>
internal enum MemoryKind
{
    Party,
    Settlement,
}

/// <summary>
/// One remembered count (#846 phase 3), plain data keyed by the party's or settlement's StringId in
/// <see cref="HiddenHostsMemory"/>. Hours are whole campaign hours.
/// </summary>
internal sealed class MemoryRow
{
    /// <summary>The counted healthy value: a party's nameplate number, or a settlement's defenders.</summary>
    internal int Count;

    /// <summary>The leading digits of <see cref="Count"/> still remembered; it falls as the memory fades.</summary>
    internal int Known;

    /// <summary>The campaign hour of the reading.</summary>
    internal int StampHour;

    /// <summary>-1: the subject is in sight, so the memory is fresh. 0 or more: the campaign hour the next digit is lost.</summary>
    internal int FadeHour = -1;

    /// <summary>The highest Scouting XP stage already paid on <see cref="XpDay"/> (0 to 3).</summary>
    internal int XpStage;

    /// <summary>The campaign day <see cref="XpStage"/> counts for; -1 when none was paid.</summary>
    internal int XpDay = -1;
}
