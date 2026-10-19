using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>One settlement's identity and the building type ids its town holds. Engine-free.</summary>
public sealed record FiefSnapshot(string SettlementId, string? CultureId, IReadOnlyList<string> BuildingTypeIds);

/// <summary>Reads and extends a fief's building list by id, so services never touch <c>Town</c> or <c>Building</c>.</summary>
public interface ITownBuildingsAdapter
{
    /// <summary>Every town and castle, as a snapshot (<c>Town.AllFiefs</c> yields fortifications only).</summary>
    IReadOnlyList<FiefSnapshot> Fortifications();

    /// <summary>The highest level among the settlement's buildings whose type id is in <paramref name="typeIds"/>, 0 when none.</summary>
    int GetLevel(string settlementId, IReadOnlyCollection<string> typeIds);

    /// <summary>Adds a level-0 building of the named type to the settlement's town. No-op when the type or town is missing.</summary>
    void Add(string settlementId, string typeId);
}
