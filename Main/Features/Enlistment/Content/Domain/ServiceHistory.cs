using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Equipment;

namespace TAOM.Features.Enlistment.Content.Domain;

/// <summary>
/// One past contract (#837): the hero who served, whom he served, how it ended, and the kit tier that
/// stayed with him (null when the quartermaster took it all back).
/// </summary>
public sealed class ServiceHistoryEntry
{
    public ServiceHistoryEntry(
        string commanderHeroId, string clanId, string kingdomId, ServiceRank rank,
        DischargeReason reason, double endDay, EnlistmentKitTier? kitTier, string heroId)
    {
        CommanderHeroId = commanderHeroId ?? "";
        ClanId = clanId ?? "";
        KingdomId = kingdomId ?? "";
        Rank = rank;
        Reason = reason;
        EndDay = endDay;
        KitTier = kitTier;
        HeroId = heroId ?? "";
    }

    public string CommanderHeroId { get; }

    /// <summary>The commander's clan, or empty.</summary>
    public string ClanId { get; }

    /// <summary>The commander's kingdom, or empty when his clan was independent.</summary>
    public string KingdomId { get; }

    public ServiceRank Rank { get; }
    public DischargeReason Reason { get; }
    public double EndDay { get; }

    /// <summary>The highest kit tier that stayed with him, or null when none did.</summary>
    public EnlistmentKitTier? KitTier { get; }

    /// <summary>The hero who served. Only this hero's entries count at a later oath (a Player Switcher swap or an heir starts fresh).</summary>
    public string HeroId { get; }

    /// <summary>
    /// A desertion that ended no more than <paramref name="cooldownDays"/> ago (and not in the future).
    /// The one predicate the enlist gate and the prune both read.
    /// </summary>
    public bool IsCoolingDownDesertion(double nowDays, int cooldownDays) =>
        Reason == DischargeReason.Desertion && nowDays >= EndDay && nowDays - EndDay < cooldownDays;

    /// <summary>One saved row: fields joined by '|'. Ids that hold a separator, or an empty hero id, make the row unwritable (null).</summary>
    public string ToRow()
    {
        if (string.IsNullOrEmpty(HeroId))
            return null;
        foreach (var id in new[] { CommanderHeroId, ClanId, KingdomId, HeroId })
        {
            if (id.IndexOfAny(ServiceHistory.Separators) >= 0)
                return null;
        }
        var inv = CultureInfo.InvariantCulture;
        return string.Join("|", CommanderHeroId, ClanId, KingdomId, ((int)Rank).ToString(inv),
            ((int)Reason).ToString(inv), EndDay.ToString("R", inv),
            KitTier.HasValue ? ((int)KitTier.Value).ToString(inv) : "", HeroId);
    }

    public static ServiceHistoryEntry TryParseRow(string row)
    {
        if (string.IsNullOrEmpty(row))
            return null;
        var f = row.Split('|');
        if (f.Length != 8 || string.IsNullOrEmpty(f[0]) || string.IsNullOrEmpty(f[7]))
            return null;
        var inv = CultureInfo.InvariantCulture;
        if (!int.TryParse(f[3], NumberStyles.Integer, inv, out var rank) || !Enum.IsDefined(typeof(ServiceRank), rank))
            return null;
        if (!int.TryParse(f[4], NumberStyles.Integer, inv, out var reason) || !Enum.IsDefined(typeof(DischargeReason), reason))
            return null;
        if (!double.TryParse(f[5], NumberStyles.Float, inv, out var end) || double.IsNaN(end) || double.IsInfinity(end))
            return null;
        EnlistmentKitTier? tier = null;
        if (f[6].Length > 0)
        {
            if (!int.TryParse(f[6], NumberStyles.Integer, inv, out var t) || !Enum.IsDefined(typeof(EnlistmentKitTier), t))
                return null;
            tier = (EnlistmentKitTier)t;
        }
        return new ServiceHistoryEntry(f[0], f[1], f[2], (ServiceRank)rank, (DischargeReason)reason, end, tier, f[7]);
    }
}

/// <summary>
/// The saved list of past contracts (#837). Parse-hardened like the other records: a bad row is
/// dropped and the rest survive, and an absent section is an empty list. Pruned on write.
/// </summary>
public sealed class ServiceHistory
{
    public static readonly char[] Separators = { ';', '=', '|', '~' };

    public List<ServiceHistoryEntry> Entries { get; } = new List<ServiceHistoryEntry>();

    public string Serialize() => string.Join("~", Entries.Select(e => e.ToRow()).Where(r => r != null));

    public static ServiceHistory Parse(string value)
    {
        var history = new ServiceHistory();
        if (string.IsNullOrEmpty(value))
            return history;
        foreach (var row in value.Split('~'))
        {
            var entry = ServiceHistoryEntry.TryParseRow(row);
            if (entry != null)
                history.Entries.Add(entry);
        }
        return history;
    }

    /// <summary>
    /// Drops entries older than <paramref name="expiryDays"/> or dated after now, then the oldest beyond
    /// <paramref name="maxEntries"/>. A desertion still inside <paramref name="cooldownDays"/> is never
    /// removed, so the cap can be exceeded by cooling rows only.
    /// </summary>
    public void Prune(double nowDays, int expiryDays, int maxEntries, int cooldownDays)
    {
        Entries.RemoveAll(e => !e.IsCoolingDownDesertion(nowDays, cooldownDays)
            && (e.EndDay > nowDays || nowDays - e.EndDay > expiryDays));
        var excess = Entries.Count - Math.Max(1, maxEntries);
        if (excess <= 0)
            return;
        var drop = Entries
            .Where(e => !e.IsCoolingDownDesertion(nowDays, cooldownDays))
            .OrderBy(e => e.EndDay)
            .Take(excess)
            .ToList();
        foreach (var entry in drop)
            Entries.Remove(entry);
    }
}
