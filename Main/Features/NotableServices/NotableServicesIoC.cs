using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.NotableServices;

/// <summary>
/// Registers Notable Services (docs/features/notable-services.md) through its feature module. Everything is a singleton. The
/// map reveal adapter is registered by Hard Questions (<c>HardQuestionsIoC</c>), the alignment service by Execution, the relation
/// grant service by Relations, the messenger state by Messengers and the co-op providers by Co-op Interop; DryIoc resolves
/// lazily, so order does not matter, and this feature never registers any of them a second time.
/// </summary>
public static class NotableServicesIoC
{
    public static void RegisterNotableServicesFeature(IRegistrator container)
    {
        container.Register<INotableServicesConfigProvider, NotableServicesConfigProvider>(Reuse.Singleton);
        container.Register<INotableServicesSettingsProvider, NotableServicesSettingsProvider>(Reuse.Singleton);
        container.Register<INotableServicesAdapter, NotableServicesAdapter>(Reuse.Singleton);
        container.Register<NotableServicesService>(Reuse.Singleton);
    }
}
