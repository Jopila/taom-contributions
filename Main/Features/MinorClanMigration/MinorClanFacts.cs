using System.Collections.Generic;

namespace TAOM.Features.MinorClanMigration;

/// <summary>The raw facts about one minor clan the migration decides from: its id, its culture id and its leader template ids.</summary>
/// <param name="ClanId">The clan's StringId.</param>
/// <param name="CultureId">StringId of <c>Clan.Culture</c>, empty when it has none.</param>
/// <param name="TemplateIds">StringIds of <c>Clan.MinorFactionCharacterTemplates</c>, in list order.</param>
public sealed record MinorClanFacts(string ClanId, string CultureId, IReadOnlyList<string> TemplateIds);
