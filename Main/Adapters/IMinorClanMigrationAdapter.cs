using System.Collections.Generic;
using TAOM.Features.MinorClanMigration;

namespace TAOM.Adapters;

/// <summary>
/// Reads and writes the two saved clan fields the minor-clan load migration fixes: <c>Clan.Culture</c> and
/// <c>Clan.MinorFactionCharacterTemplates</c> (#957 part 2). Both are public members of the engine type, so no
/// reflection is involved.
/// </summary>
public interface IMinorClanMigrationAdapter
{
    /// <summary>The facts of every named clan the campaign holds; a clan the campaign does not hold is absent.</summary>
    IReadOnlyList<MinorClanFacts> Snapshot(IReadOnlyCollection<string> clanIds);

    /// <summary>True when a character with this StringId is registered.</summary>
    bool CharacterExists(string characterId);

    /// <summary>Sets the clan's culture. False, and nothing written, when the clan or the culture does not exist.</summary>
    bool SetCulture(string clanId, string cultureId);

    /// <summary>
    /// Replaces the clan's template list with these characters, in order. False, and the list untouched, when the
    /// clan or any one of the characters does not exist.
    /// </summary>
    bool ReplaceTemplates(string clanId, IReadOnlyList<string> templateIds);
}
