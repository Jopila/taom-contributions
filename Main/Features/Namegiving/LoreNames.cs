using System;
using System.Collections.Generic;
using TAOM.Features.Execution;

namespace TAOM.Features.Namegiving;

/// <summary>
/// The four lore names (docs/features/namegiving.md): a settlement takes its older or darker name while the side the name
/// belongs to holds it, and goes back to the map's name when the other side takes it. The key prefix doubles as the marker
/// <see cref="NamegivingService.Classify"/> reads to tell a lore name from a player's, so every key starts with
/// <see cref="KeyPrefix"/>. Each row spells its literal out in full, because the localization harvester reads literals. The ids are the live TAOM_Map settlement ids (NamegivingLiveDataTests).
/// Save contract: the engine saves a custom name as its raw <c>{=taom_namegiving_lore_*}English</c> text, so a save holds these
/// keys. Never rename <see cref="KeyPrefix"/> or a key, and never remove a row: a saved lore name would then read as a player's name
/// (or lose its row) and could never be restored.
/// </summary>
public static class LoreNames
{
    /// <summary>Every lore key starts with this; a custom name that does is a lore name. Saves store it: never rename it.</summary>
    public const string KeyPrefix = "taom_namegiving_lore_";

    public static IReadOnlyList<LoreRow> All { get; } = new[]
    {
        new LoreRow("town_ES2", "{=taom_namegiving_lore_minas_ithil}Minas Ithil", FactionSide.Free),
        new LoreRow("town_DG1", "{=taom_namegiving_lore_amon_lanc}Amon Lanc", FactionSide.Free),
        new LoreRow("town_AN2", "{=taom_namegiving_lore_deadmens_dike}Deadmen's Dike", FactionSide.Evil),
        new LoreRow("town_isengard", "{=taom_namegiving_lore_treegarth}Treegarth of Orthanc", FactionSide.Free),
    };

    /// <summary>The row for a settlement id, or null when it has no lore name.</summary>
    public static LoreRow? Find(string? settlementId)
    {
        foreach (var row in All)
            if (string.Equals(row.SettlementId, settlementId, StringComparison.Ordinal))
                return row;
        return null;
    }
}
