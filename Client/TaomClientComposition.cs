using DryIoc;
using TAOM.Composition;

namespace TAOM.Client;

/// <summary>
/// The client's registrations in core's container (ADR-013), applied by IoC.Configure after every core
/// registration with replace as the default. Empty until the type moves (B4a to B5) register the client
/// implementations of core's adapter interfaces here.
/// </summary>
internal sealed class TaomClientComposition : IClientComposition
{
    public void RegisterServices(IRegistrator registrator)
    {
    }
}
