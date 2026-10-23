using System;
using SandBox.View.Map;
using TAOM.Core.Logging;

namespace TAOM.Features.HallOfFire;

/// <summary>
/// Wraps the engine's map conversation provider and gives a TAOM settlement a vanilla backdrop name
/// (docs/features/hall-of-fire.md). <c>MapConversationTableau.FirstTimeInit</c> calls the provider once per conversation,
/// and again after the partner's equipment changes; <c>OnTick</c> retries <c>FirstTimeInit</c> every tick until it
/// completes, so a throw here would repeat every tick. It therefore never throws after the inner call: the inner call
/// stays outside the guard, so vanilla behaves as before, and any failure of the rewrite logs once and returns the
/// inner tag.
/// </summary>
public sealed class TaomMapConversationDataProvider : IMapConversationDataProvider
{
    private readonly HallOfFireService _service;
    private readonly IModLogger _logger;
    private bool _failureLogged;

    public TaomMapConversationDataProvider(IMapConversationDataProvider inner, HallOfFireService service, IModLogger logger)
    {
        Inner = inner;
        _service = service;
        _logger = logger;
    }

    public IMapConversationDataProvider Inner { get; }

    public string GetAtmosphereNameFromData(MapConversationTableauData data)
    {
        var tag = Inner.GetAtmosphereNameFromData(data);
        try
        {
            return _service.Resolve(tag, data.Settlement?.Culture?.StringId) ?? tag;
        }
        catch (Exception ex)
        {
            if (!_failureLogged)
            {
                _failureLogged = true;
                _logger.LogError($"TaomMapConversationDataProvider: the atmosphere rewrite failed, using the vanilla name: {ex.Message}");
            }
            return tag;
        }
    }
}
