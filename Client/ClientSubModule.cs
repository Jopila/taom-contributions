using TaleWorlds.MountAndBlade;
using TAOM.Composition;

namespace TAOM.Client;

/// <summary>
/// The TAOM module's client-only submodule (ADR-013). Its manifest tags (DedicatedServerType none,
/// IsNoRenderModeElement false) keep a dedicated server from loading it. The engine runs this
/// constructor before any submodule's OnSubModuleLoad, so it only hands core the client registrations
/// and does nothing that needs the game.
/// </summary>
public sealed class ClientSubModule : MBSubModuleBase
{
    public ClientSubModule()
    {
        ClientComposition.Current = new TaomClientComposition();
    }
}
