using System;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.HallOfFire;

/// <summary>
/// Puts <see cref="TaomMapConversationDataProvider"/> in the engine slot once per process, around whatever provider is
/// there. Skipped on a dedicated server (no map view), for an empty slot and when the wrapper is already installed.
/// A throwing slot (the SandBox.View module not loaded) is logged, never propagated into startup.
/// </summary>
public sealed class HallOfFireInstaller
{
    private readonly IMapConversationProviderSlot _slot;
    private readonly HallOfFireService _service;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly IModLogger _logger;

    public HallOfFireInstaller(IMapConversationProviderSlot slot, HallOfFireService service, IDedicatedServerProvider dedicated, IModLogger logger)
    {
        _slot = slot;
        _service = service;
        _dedicated = dedicated;
        _logger = logger;
    }

    public void Install()
    {
        if (_dedicated.IsDedicatedServer)
            return;

        try
        {
            var current = _slot.Current;
            if (current == null)
            {
                _logger.LogWarning("HallOfFireInstaller: the map conversation provider slot is empty, not installing");
                return;
            }
            if (current is TaomMapConversationDataProvider)
                return;

            _slot.Set(new TaomMapConversationDataProvider(current, _service, _logger));
            _logger.LogInfo($"HallOfFireInstaller: wrapped {current.GetType().Name} so TAOM settlements borrow vanilla conversation backdrops");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"HallOfFireInstaller: could not install the conversation backdrop wrapper: {ex.Message}");
        }
    }
}
