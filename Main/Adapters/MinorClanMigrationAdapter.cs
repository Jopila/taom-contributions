using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.MinorClanMigration;

namespace TAOM.Adapters;

public sealed class MinorClanMigrationAdapter : IMinorClanMigrationAdapter
{
    private readonly IModLogger _logger;

    public MinorClanMigrationAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<MinorClanFacts> Snapshot(IReadOnlyCollection<string> clanIds)
    {
        var facts = new List<MinorClanFacts>();
        foreach (var clan in Clan.All)
        {
            if (clan == null || !clanIds.Contains(clan.StringId))
                continue;

            var templates = clan.MinorFactionCharacterTemplates?.Where(t => t != null).Select(t => t.StringId).ToList()
                            ?? new List<string>();
            facts.Add(new MinorClanFacts(clan.StringId, clan.Culture?.StringId ?? string.Empty, templates));
        }

        return facts;
    }

    public bool CharacterExists(string characterId)
        => !string.IsNullOrEmpty(characterId) && MBObjectManager.Instance?.GetObject<CharacterObject>(characterId) != null;

    public bool SetCulture(string clanId, string cultureId)
    {
        try
        {
            var clan = Find(clanId);
            var culture = MBObjectManager.Instance?.GetObject<CultureObject>(cultureId);
            if (clan == null || culture == null)
                return false;

            clan.Culture = culture;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[MinorClanMigration] SetCulture('{clanId}', '{cultureId}') failed: {ex.Message}");
            return false;
        }
    }

    public bool ReplaceTemplates(string clanId, IReadOnlyList<string> templateIds)
    {
        try
        {
            var clan = Find(clanId);
            if (clan?.MinorFactionCharacterTemplates == null)
                return false;

            // Resolve every id before touching the list, so a missing one leaves the list as it was.
            var resolved = new List<CharacterObject>(templateIds.Count);
            foreach (var id in templateIds)
            {
                var character = MBObjectManager.Instance?.GetObject<CharacterObject>(id);
                if (character == null)
                    return false;
                resolved.Add(character);
            }

            clan.MinorFactionCharacterTemplates.Clear();
            foreach (var character in resolved)
                clan.MinorFactionCharacterTemplates.Add(character);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[MinorClanMigration] ReplaceTemplates('{clanId}') failed: {ex.Message}");
            return false;
        }
    }

    private static Clan? Find(string clanId)
        => Clan.All.FirstOrDefault(c => c != null && c.StringId == clanId);
}
