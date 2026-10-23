using SandBox.View.Map;
using TAOM.Core.Logging;
using TAOM.Features.HallOfFire;

namespace TAOM.Client.Features.HallOfFire;

/// <summary>
/// Builds and recognizes <see cref="TaomMapConversationDataProvider"/>, the wrapper that gives TAOM settlements a
/// vanilla conversation backdrop (ADR-013: the provider type lives in TAOM.Client).
/// </summary>
internal sealed class HallOfFireConversationWrapper : IMapConversationProviderWrapper
{
    private readonly HallOfFireService _service;
    private readonly IModLogger _logger;

    public HallOfFireConversationWrapper(HallOfFireService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public IMapConversationDataProvider? Wrap(IMapConversationDataProvider inner) =>
        new TaomMapConversationDataProvider(inner, _service, _logger);

    public bool IsWrapper(IMapConversationDataProvider provider) => provider is TaomMapConversationDataProvider;
}
