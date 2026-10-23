using SandBox.View.Map;

namespace TAOM.Features.HallOfFire;

/// <summary>No client wrapper (a dedicated server, or a client where TAOM.Client did not load): nothing is wrapped.</summary>
public sealed class NullMapConversationProviderWrapper : IMapConversationProviderWrapper
{
    public IMapConversationDataProvider? Wrap(IMapConversationDataProvider inner) => null;

    public bool IsWrapper(IMapConversationDataProvider provider) => false;
}
