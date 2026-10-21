using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.MinorClanMigration;

/// <summary>
/// Brings an old save's minor clans in line with the data the update ships (#957 part 2). A save keeps
/// <c>Clan.Culture</c> (a saved property) and <c>Clan._minorFactionCharacterTemplates</c> (a saved field), and the
/// campaign never reloads <c>spclans.xml</c> on a load, while the NPC characters always reload. So an old save has
/// retagged troops under a clan whose own culture and leader templates are still the vanilla ones: the Grey Company
/// (clan culture <c>empire</c>, Evil) would prefer Dunland contracts while its troops (now <c>arthedain</c>) desert
/// under Dunland.
///
/// <para>Two corrections, each decided from raw facts and applied through the adapter: the Grey Company's culture
/// becomes <c>arthedain</c> and the Corsair Blades' becomes <c>umbar</c>; and each of the four clans whose leader
/// templates <c>lords.xslt</c> clones swaps every <c>spc_*</c> template for its <c>taom_spc_*</c> clone when that
/// clone is registered. A clone that is not registered keeps its original, so a data problem never empties the list.
/// A new campaign already carries the right values, so the run changes nothing there. No RNG and no hero state is
/// read, so every co-op peer reaches the same result from the same data. Heroes already spawned keep the culture
/// they were created with: a hero's culture is saved with the hero.</para>
/// </summary>
public sealed class MinorClanMigrationService
{
    private const string OriginalPrefix = "spc_";
    private const string ClonePrefix = "taom_";

    /// <summary>The clans whose own culture the update changed, and the culture it is now.</summary>
    internal static readonly IReadOnlyDictionary<string, string> ClanCultures = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["legion_of_the_betrayed"] = "arthedain",
        ["company_of_the_boar"] = "umbar",
    };

    /// <summary>The clans whose leader templates <c>lords.xslt</c> clones.</summary>
    internal static readonly IReadOnlyList<string> ClanIds = new[]
    {
        "legion_of_the_betrayed",
        "company_of_the_boar",
        "eleftheroi",
        "brotherhood_of_woods",
    };

    private readonly IMinorClanMigrationAdapter _adapter;
    private readonly IModLogger _logger;

    public MinorClanMigrationService(IMinorClanMigrationAdapter adapter, IModLogger logger)
    {
        _adapter = adapter;
        _logger = logger;
    }

    /// <summary>Corrects every clan that needs it and returns how many clans changed.</summary>
    public int Migrate()
    {
        var changed = 0;
        foreach (var clan in _adapter.Snapshot(ClanIds.ToList()))
        {
            if (!ClanIds.Contains(clan.ClanId))
                continue;
            if (MigrateClan(clan))
                changed++;
        }

        return changed;
    }

    private bool MigrateClan(MinorClanFacts clan)
    {
        var parts = new List<string>();

        if (ClanCultures.TryGetValue(clan.ClanId, out var culture)
            && !string.Equals(clan.CultureId, culture, StringComparison.Ordinal))
        {
            if (_adapter.SetCulture(clan.ClanId, culture))
                parts.Add($"culture {clan.CultureId} -> {culture}");
            else
                _logger.LogWarning($"[MinorClanMigration] {clan.ClanId}: could not set the culture to {culture}");
        }

        var templates = clan.TemplateIds.Select(CloneOrSelf).ToList();
        var swapped = templates.Where((id, i) => !string.Equals(id, clan.TemplateIds[i], StringComparison.Ordinal)).Count();
        if (swapped > 0)
        {
            if (_adapter.ReplaceTemplates(clan.ClanId, templates))
                parts.Add($"{swapped} leader template(s) -> TAOM clones");
            else
                _logger.LogWarning($"[MinorClanMigration] {clan.ClanId}: could not replace the leader templates with their clones");
        }

        if (parts.Count == 0)
            return false;

        _logger.LogInfo($"[MinorClanMigration] {clan.ClanId}: {string.Join(", ", parts)}");
        return true;
    }

    private string CloneOrSelf(string templateId)
    {
        if (!templateId.StartsWith(OriginalPrefix, StringComparison.Ordinal))
            return templateId;

        var clone = ClonePrefix + templateId;
        return _adapter.CharacterExists(clone) ? clone : templateId;
    }
}
