using SandBox.View.Map;

namespace TAOM.Features.HallOfFire;

/// <summary>
/// Builds and recognizes the TAOM wrapper around the engine's map conversation provider (ADR-013: the wrapper type is
/// client-only). Core registers NullMapConversationProviderWrapper; TAOM.Client replaces it.
/// </summary>
public interface IMapConversationProviderWrapper
{
    /// <summary>The wrapper around <paramref name="inner"/>, or null when this process has no client wrapper.</summary>
    IMapConversationDataProvider? Wrap(IMapConversationDataProvider inner);

    bool IsWrapper(IMapConversationDataProvider provider);
}
