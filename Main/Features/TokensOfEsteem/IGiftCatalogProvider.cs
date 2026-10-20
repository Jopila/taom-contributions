using TAOM.Features.TokensOfEsteem.Models;

namespace TAOM.Features.TokensOfEsteem;

/// <summary>The validated gift catalog. Loaded once the game's item categories and races exist, then kept for the process.</summary>
public interface IGiftCatalogProvider
{
    /// <summary>The catalog, or <see cref="GiftCatalog.Empty"/> when it cannot be built yet or at all.</summary>
    GiftCatalog Get();
}
