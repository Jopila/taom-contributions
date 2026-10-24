using TAOM.Features.Provender.Models;

namespace TAOM.Features.Provender;

/// <summary>The validated rations. Loaded once the game's items and cultures exist, then kept for the process.</summary>
public interface IRationCatalogProvider
{
    /// <summary>The catalog, or <see cref="RationCatalog.Empty"/> when it cannot be built yet or at all.</summary>
    RationCatalog Get();
}
