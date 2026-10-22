using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CoopInterop;

/// <inheritdoc />
public sealed class DedicatedServerProvider : IDedicatedServerProvider
{
    // The engine's own build constant (the server DLL returns true, the client DLL false), read by the
    // engine before any submodule loads. TAOM.Dependencies reads the same constant; this class is the
    // test seam for the services.

    /// <inheritdoc />
    public bool IsDedicatedServer => GameNetwork.IsDedicatedServer;
}
