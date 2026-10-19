using System.Collections.Generic;
using TAOM.Features.TelcharsMark;

namespace TAOM.Adapters;

/// <summary>One open inventory screen as Telchar's Mark sees it: its list rows and the shown loadout.</summary>
public interface IUpgradeScreen
{
    /// <summary>The rows of both item lists (player and other side), never the equipment slots.</summary>
    IReadOnlyList<IUpgradeRow> GetRows();

    /// <summary>The equipped items of the shown character in the shown outfit.</summary>
    InventoryLoadout GetLoadout();

    /// <summary>Re-runs vanilla's per-row filter on both lists, so the "show only better items" hide follows.</summary>
    void ReapplyFilters();
}
